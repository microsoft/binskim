// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;

using Microsoft.CodeAnalysis.IL.Sdk;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal static class SourceLinkAttributionCalculator
    {
        internal const int CurrentSchemaVersion = 1;
        internal const int MaxPdbDocumentCount = 100000;
        private const int MaxSamplesPerRepository = 3;
        private const string WildcardPlaceholder = "__binskim_sourcelink_wildcard__";

        internal static string MergeSourceLinkJsonDocuments(IEnumerable<string> sourceLinkJsonDocuments)
        {
            var mergedDocuments = new JObject();
            var documentPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string sourceLinkJson in sourceLinkJsonDocuments ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(sourceLinkJson))
                {
                    continue;
                }

                JObject sourceLink = JObject.Parse(sourceLinkJson);
                if (!(sourceLink["documents"] is JObject documents))
                {
                    continue;
                }

                foreach (JProperty document in documents.Properties())
                {
                    if (documentPatterns.Add(document.Name))
                    {
                        mergedDocuments.Add(document.Name, document.Value.DeepClone());
                    }
                }
            }

            return mergedDocuments.Count == 0
                ? null
                : new JObject(new JProperty("documents", mergedDocuments)).ToString(Formatting.None);
        }

        internal static SourceLinkAttributionData Calculate(
            string sourceLinkJson,
            IEnumerable<string> pdbDocumentPaths,
            AnalysisSummary buildContext)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new SourceLinkAttributionData
            {
                SchemaVersion = CurrentSchemaVersion,
                Classification = "Unknown",
                ClassificationReason = "NoSourceLink",
                ClassificationScope = "MatchedPdbDocuments",
                DocumentLimit = MaxPdbDocumentCount,
                IsComplete = true,
                Repositories = new List<SourceRepositoryAttributionData>(),
            };

            if (string.IsNullOrWhiteSpace(sourceLinkJson))
            {
                Complete(result, stopwatch);
                return result;
            }

            IReadOnlyList<SourceLinkMapping> mappings;
            try
            {
                mappings = ParseMappings(sourceLinkJson);
            }
            catch (JsonException)
            {
                result.ClassificationReason = "InvalidSourceLinkJson";
                Complete(result, stopwatch);
                return result;
            }

            if (mappings.Count == 0)
            {
                result.ClassificationReason = "NoSourceLinkMappings";
                Complete(result, stopwatch);
                return result;
            }

            var exactMappings = new Dictionary<string, SourceLinkMapping>(StringComparer.OrdinalIgnoreCase);
            var wildcardMappings = new List<SourceLinkMapping>();

            foreach (SourceLinkMapping mapping in mappings)
            {
                if (mapping.HasWildcard)
                {
                    wildcardMappings.Add(mapping);
                }
                else if (!exactMappings.ContainsKey(mapping.DocumentPattern))
                {
                    exactMappings.Add(mapping.DocumentPattern, mapping);
                }
            }

            wildcardMappings = wildcardMappings
                .OrderByDescending(mapping => mapping.DocumentPrefix.Length)
                .ToList();

            var documents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var repositories = new Dictionary<string, SourceRepositoryAccumulator>(StringComparer.OrdinalIgnoreCase);

            foreach (string documentPath in pdbDocumentPaths ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(documentPath) || !documents.Add(documentPath))
                {
                    continue;
                }

                if (result.ProcessedDocumentCount >= MaxPdbDocumentCount)
                {
                    result.IsComplete = false;
                    break;
                }

                result.ProcessedDocumentCount++;

                SourceLinkMapping mapping = FindMapping(documentPath, exactMappings, wildcardMappings);
                if (mapping == null)
                {
                    result.UnmatchedDocumentCount++;
                    continue;
                }

                result.MatchedDocumentCount++;

                SourceRepositoryIdentity repository = mapping.Repository;
                string repositoryKey = repository.GetKey();
                if (!repositories.TryGetValue(repositoryKey, out SourceRepositoryAccumulator accumulator))
                {
                    accumulator = new SourceRepositoryAccumulator(repository);
                    repositories.Add(repositoryKey, accumulator);
                }

                accumulator.AddDocument(
                    mapping.GetRepositoryRelativePath(documentPath),
                    mapping.SourceUrlPattern,
                    mapping.Repository.Commit);
            }

            foreach (SourceRepositoryAccumulator accumulator in repositories.Values)
            {
                result.Repositories.Add(accumulator.ToData(buildContext));
            }

            result.Repositories = result.Repositories
                .OrderByDescending(repository => repository.DocumentCount)
                .ThenBy(repository => repository.Host, StringComparer.OrdinalIgnoreCase)
                .ThenBy(repository => repository.Organization, StringComparer.OrdinalIgnoreCase)
                .ThenBy(repository => repository.Repository, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Classify(result, buildContext);
            Complete(result, stopwatch);
            return result;
        }

        private static IReadOnlyList<SourceLinkMapping> ParseMappings(string sourceLinkJson)
        {
            JObject sourceLink = JObject.Parse(sourceLinkJson);
            if (!(sourceLink["documents"] is JObject documents))
            {
                return Array.Empty<SourceLinkMapping>();
            }

            var mappings = new List<SourceLinkMapping>();
            foreach (JProperty document in documents.Properties())
            {
                string sourceUrlPattern = document.Value.Type == JTokenType.String
                    ? document.Value.Value<string>()
                    : null;

                if (!string.IsNullOrWhiteSpace(document.Name) &&
                    !string.IsNullOrWhiteSpace(sourceUrlPattern))
                {
                    mappings.Add(new SourceLinkMapping(document.Name, sourceUrlPattern));
                }
            }

            return mappings;
        }

        private static SourceLinkMapping FindMapping(
            string documentPath,
            IDictionary<string, SourceLinkMapping> exactMappings,
            IEnumerable<SourceLinkMapping> wildcardMappings)
        {
            if (exactMappings.TryGetValue(documentPath, out SourceLinkMapping exactMapping))
            {
                return exactMapping;
            }

            foreach (SourceLinkMapping wildcardMapping in wildcardMappings)
            {
                if (documentPath.StartsWith(wildcardMapping.DocumentPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return wildcardMapping;
                }
            }

            return null;
        }

        private static void Classify(SourceLinkAttributionData result, AnalysisSummary buildContext)
        {
            if (!result.IsComplete)
            {
                result.Classification = "Incomplete";
                result.ClassificationReason = "PdbDocumentLimitExceeded";
                result.ConfidenceRank = 0;
                return;
            }

            if (result.ProcessedDocumentCount == 0)
            {
                result.ClassificationReason = "NoPdbDocuments";
                return;
            }

            if (result.MatchedDocumentCount == 0)
            {
                result.ClassificationReason = "NoPdbDocumentsMatchedSourceLink";
                return;
            }

            if (result.Repositories.Count == 0)
            {
                result.ClassificationReason = "NoRepositoryIdentity";
                return;
            }

            bool hasBuildRepositoryIdentity =
                !string.IsNullOrWhiteSpace(buildContext?.RepositoryId) ||
                !string.IsNullOrWhiteSpace(buildContext?.RepositoryName);

            if (!hasBuildRepositoryIdentity)
            {
                result.Classification = result.Repositories.Count == 1
                    ? "SingleRepository"
                    : "MultipleRepositories";
                result.ClassificationReason = "BuildRepositoryIdentityUnavailable";
                return;
            }

            bool matchedBuildRepository = result.Repositories.Any(repository => repository.BuildMatchRank > 0);
            bool matchedOtherRepository = result.Repositories.Any(repository => repository.BuildMatchRank == 0);
            result.ConfidenceRank = result.Repositories.Max(repository => repository.BuildMatchRank);

            if (matchedBuildRepository && matchedOtherRepository)
            {
                result.Classification = "MixedRepositories";
                result.ClassificationReason = "SourceLinkDocumentsMatchBuildAndOtherRepositories";
            }
            else if (matchedBuildRepository)
            {
                result.Classification = "BuildRepositoryOnly";
                result.ClassificationReason = GetBuildMatchReason(result.ConfidenceRank);
            }
            else
            {
                result.Classification = "OnlyOtherRepositories";
                result.ClassificationReason = "NoSourceRepositoryMatchesBuildRepository";
            }
        }

        private static string GetBuildMatchReason(int confidenceRank)
        {
            switch (confidenceRank)
            {
                case 4:
                    return "SourceRepositoryIdMatchesBuildRepositoryId";
                case 3:
                    return "SourceOrganizationProjectRepositoryMatchesBuildRepository";
                case 2:
                    return "SourceOrganizationRepositoryMatchesBuildRepository";
                case 1:
                    return "SourceRepositoryNameMatchesBuildRepository";
                default:
                    return "NoSourceRepositoryMatchesBuildRepository";
            }
        }

        private static void Complete(SourceLinkAttributionData result, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            result.ProcessingMilliseconds = stopwatch.ElapsedMilliseconds;
        }

        private sealed class SourceLinkMapping
        {
            internal SourceLinkMapping(string documentPattern, string sourceUrlPattern)
            {
                this.DocumentPattern = documentPattern;
                this.SourceUrlPattern = sourceUrlPattern;
                this.HasWildcard = documentPattern.EndsWith("*", StringComparison.Ordinal) &&
                                   sourceUrlPattern.Contains("*", StringComparison.Ordinal);
                this.DocumentPrefix = this.HasWildcard
                    ? documentPattern.Substring(0, documentPattern.Length - 1)
                    : documentPattern;
                this.Repository = SourceRepositoryIdentity.Create(sourceUrlPattern);
            }

            internal string DocumentPattern { get; }

            internal string DocumentPrefix { get; }

            internal bool HasWildcard { get; }

            internal SourceRepositoryIdentity Repository { get; }

            internal string SourceUrlPattern { get; }

            internal string GetRepositoryRelativePath(string documentPath)
            {
                if (!this.HasWildcard)
                {
                    return this.Repository.RepositoryPathTemplate;
                }

                string relativePath = documentPath.Substring(this.DocumentPrefix.Length)
                    .Replace('\\', '/')
                    .TrimStart('/');

                return this.Repository.RepositoryPathTemplate
                    .Replace(WildcardPlaceholder, relativePath, StringComparison.Ordinal)
                    .TrimStart('/');
            }
        }

        private sealed class SourceRepositoryIdentity
        {
            internal string Host { get; private set; } = string.Empty;

            internal string Organization { get; private set; } = string.Empty;

            internal string Project { get; private set; } = string.Empty;

            internal string Repository { get; private set; } = string.Empty;

            internal string RepositoryId { get; private set; } = string.Empty;

            internal string Commit { get; private set; } = string.Empty;

            internal string RepositoryPathTemplate { get; private set; } = string.Empty;

            internal string SourceUrlPattern { get; private set; } = string.Empty;

            internal static SourceRepositoryIdentity Create(string sourceUrlPattern)
            {
                var result = new SourceRepositoryIdentity { SourceUrlPattern = sourceUrlPattern };
                string parseableUrl = WebUtility.HtmlDecode(sourceUrlPattern)
                    .Replace("*", WildcardPlaceholder, StringComparison.Ordinal);

                if (!Uri.TryCreate(parseableUrl, UriKind.Absolute, out Uri uri))
                {
                    return result;
                }

                string[] segments = uri.AbsolutePath
                    .Trim('/')
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.UnescapeDataString)
                    .ToArray();
                IDictionary<string, string> query = ParseQuery(uri.Query);
                string host = uri.Host.ToLowerInvariant();

                result.Host = host;

                if (host == "raw.githubusercontent.com" && segments.Length >= 3)
                {
                    result.Host = "github.com";
                    result.Organization = segments[0];
                    result.Repository = segments[1];
                    result.Commit = segments[2];
                    result.RepositoryPathTemplate = string.Join("/", segments.Skip(3));
                    return result;
                }

                if (host == "github.com" && segments.Length >= 4 &&
                    (segments[2].Equals("blob", StringComparison.OrdinalIgnoreCase) ||
                     segments[2].Equals("raw", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Organization = segments[0];
                    result.Repository = segments[1];
                    result.Commit = segments[3];
                    result.RepositoryPathTemplate = string.Join("/", segments.Skip(4));
                    return result;
                }

                if (host == "dev.azure.com" && segments.Length >= 2)
                {
                    result.Organization = segments[0];
                    PopulateAzureReposIdentity(result, segments, fallbackProjectIndex: 1, query);
                    return result;
                }

                if (host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase) && segments.Length >= 1)
                {
                    result.Host = "visualstudio.com";
                    result.Organization = host.Substring(0, host.Length - ".visualstudio.com".Length);
                    PopulateAzureReposIdentity(result, segments, fallbackProjectIndex: 0, query);
                    return result;
                }

                result.RepositoryPathTemplate = string.Join("/", segments);
                return result;
            }

            internal string GetKey()
            {
                string repositoryIdentity = !string.IsNullOrWhiteSpace(this.RepositoryId) ||
                                            !string.IsNullOrWhiteSpace(this.Repository)
                    ? string.Join("|", this.Host, this.Organization, this.Project, this.Repository, this.RepositoryId)
                    : this.SourceUrlPattern;

                return repositoryIdentity.ToLowerInvariant();
            }

            internal int GetBuildMatchRank(AnalysisSummary buildContext)
            {
                if (buildContext == null)
                {
                    return 0;
                }

                string buildRepositoryId = Normalize(buildContext.RepositoryId);
                string buildRepository = NormalizeRepositoryName(buildContext.RepositoryName);
                string buildProject = Normalize(buildContext.ProjectName);
                string buildOrganization = NormalizeOrganization(buildContext.OrganizationName);

                if (!string.IsNullOrEmpty(buildRepositoryId) &&
                    !string.IsNullOrEmpty(this.RepositoryId) &&
                    buildRepositoryId == Normalize(this.RepositoryId))
                {
                    return 4;
                }

                string sourceRepository = Normalize(this.Repository);
                string sourceProject = Normalize(this.Project);
                string sourceOrganization = Normalize(this.Organization);

                if (!string.IsNullOrEmpty(buildOrganization) &&
                    !string.IsNullOrEmpty(buildProject) &&
                    !string.IsNullOrEmpty(buildRepository) &&
                    buildOrganization == sourceOrganization &&
                    buildProject == sourceProject &&
                    buildRepository == sourceRepository)
                {
                    return 3;
                }

                if (!string.IsNullOrEmpty(buildOrganization) &&
                    !string.IsNullOrEmpty(buildRepository) &&
                    buildOrganization == sourceOrganization &&
                    buildRepository == sourceRepository)
                {
                    return 2;
                }

                return !string.IsNullOrEmpty(buildRepository) &&
                       buildRepository == sourceRepository
                    ? 1
                    : 0;
            }

            private static void PopulateAzureReposIdentity(
                SourceRepositoryIdentity result,
                string[] segments,
                int fallbackProjectIndex,
                IDictionary<string, string> query)
            {
                int gitIndex = Array.FindIndex(segments, segment => segment.Equals("_git", StringComparison.OrdinalIgnoreCase));
                int apiIndex = Array.FindIndex(segments, segment => segment.Equals("_apis", StringComparison.OrdinalIgnoreCase));
                int markerIndex = gitIndex >= 0 ? gitIndex : apiIndex;
                int projectIndex = markerIndex > 0 ? markerIndex - 1 : fallbackProjectIndex;
                result.Project = segments.Length > projectIndex ? segments[projectIndex] : string.Empty;

                if (gitIndex >= 0 && segments.Length > gitIndex + 1)
                {
                    result.Repository = segments[gitIndex + 1];
                }

                int repositoriesIndex = Array.FindIndex(
                    segments,
                    segment => segment.Equals("repositories", StringComparison.OrdinalIgnoreCase));
                if (repositoriesIndex >= 0 && segments.Length > repositoriesIndex + 1)
                {
                    result.RepositoryId = segments[repositoriesIndex + 1];
                    if (!Guid.TryParse(result.RepositoryId, out _))
                    {
                        result.Repository = result.RepositoryId;
                    }
                }

                result.Commit = GetFirstValue(query, "version", "versionDescriptor.version");
                result.RepositoryPathTemplate = GetFirstValue(query, "path", "scopePath").TrimStart('/');
            }

            private static IDictionary<string, string> ParseQuery(string query)
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string pair in query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = pair.Split(new[] { '=' }, 2);
                    string name = Uri.UnescapeDataString(parts[0]);
                    string value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

                    if (!result.ContainsKey(name))
                    {
                        result.Add(name, value);
                    }
                }

                return result;
            }

            private static string GetFirstValue(IDictionary<string, string> values, params string[] names)
            {
                foreach (string name in names)
                {
                    if (values.TryGetValue(name, out string value))
                    {
                        return value ?? string.Empty;
                    }
                }

                return string.Empty;
            }

            private static string NormalizeOrganization(string organization)
            {
                if (string.IsNullOrWhiteSpace(organization))
                {
                    return string.Empty;
                }

                if (Uri.TryCreate(organization, UriKind.Absolute, out Uri uri))
                {
                    if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return Normalize(uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault());
                    }

                    if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return Normalize(uri.Host.Substring(0, uri.Host.Length - ".visualstudio.com".Length));
                    }
                }

                return Normalize(organization);
            }

            private static string NormalizeRepositoryName(string repository)
            {
                if (string.IsNullOrWhiteSpace(repository))
                {
                    return string.Empty;
                }

                if (Uri.TryCreate(repository, UriKind.Absolute, out _))
                {
                    SourceRepositoryIdentity identity = Create(repository);
                    if (!string.IsNullOrWhiteSpace(identity.Repository))
                    {
                        return Normalize(identity.Repository);
                    }

                    var repositoryUri = new Uri(repository);
                    string lastSegment = repositoryUri.AbsolutePath
                        .Trim('/')
                        .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                        .LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(lastSegment))
                    {
                        return Normalize(lastSegment);
                    }
                }

                return Normalize(repository);
            }

            private static string Normalize(string value)
            {
                return value?.Trim().Trim('/').ToLowerInvariant() ?? string.Empty;
            }
        }

        private sealed class SourceRepositoryAccumulator
        {
            private readonly SourceRepositoryIdentity repository;
            private readonly HashSet<string> commits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> sourceUrlPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> samplePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private string commonSourcePath;
            private string sourceFile;

            internal SourceRepositoryAccumulator(SourceRepositoryIdentity repository)
            {
                this.repository = repository;
                if (!string.IsNullOrWhiteSpace(repository.Commit))
                {
                    this.commits.Add(repository.Commit);
                }
            }

            internal int DocumentCount { get; private set; }

            internal void AddDocument(string repositoryRelativePath, string sourceUrlPattern, string commit)
            {
                this.DocumentCount++;

                if (!string.IsNullOrWhiteSpace(commit))
                {
                    this.commits.Add(commit);
                }

                if (this.sourceUrlPatterns.Count < MaxSamplesPerRepository)
                {
                    this.sourceUrlPatterns.Add(sourceUrlPattern);
                }

                if (!string.IsNullOrWhiteSpace(repositoryRelativePath) &&
                    this.samplePaths.Count < MaxSamplesPerRepository)
                {
                    this.samplePaths.Add(repositoryRelativePath);
                }

                if (!string.IsNullOrWhiteSpace(repositoryRelativePath))
                {
                    string normalizedPath = repositoryRelativePath.Replace('\\', '/').Trim('/');
                    string sourceDirectory = GetDirectoryName(normalizedPath);

                    if (this.DocumentCount == 1)
                    {
                        this.sourceFile = normalizedPath;
                        this.commonSourcePath = sourceDirectory;
                    }
                    else
                    {
                        this.sourceFile = string.Empty;
                        this.commonSourcePath = GetCommonPath(this.commonSourcePath, sourceDirectory);
                    }
                }
                else if (this.DocumentCount > 1)
                {
                    this.sourceFile = string.Empty;
                    this.commonSourcePath = string.Empty;
                }
            }

            internal SourceRepositoryAttributionData ToData(AnalysisSummary buildContext)
            {
                return new SourceRepositoryAttributionData
                {
                    Host = this.repository.Host,
                    Organization = this.repository.Organization,
                    Project = this.repository.Project,
                    Repository = this.repository.Repository,
                    RepositoryId = this.repository.RepositoryId,
                    Commits = this.commits.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                    DocumentCount = this.DocumentCount,
                    BuildMatchRank = this.repository.GetBuildMatchRank(buildContext),
                    CommonSourcePath = this.commonSourcePath ?? string.Empty,
                    SourceFile = this.sourceFile ?? string.Empty,
                    SamplePaths = this.samplePaths.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                    SourceUrlPatterns = this.sourceUrlPatterns.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                };
            }

            private static string GetDirectoryName(string path)
            {
                int separatorIndex = path.LastIndexOf('/');
                return separatorIndex > 0 ? path.Substring(0, separatorIndex) : string.Empty;
            }

            private static string GetCommonPath(string firstPath, string secondPath)
            {
                if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
                {
                    return string.Empty;
                }

                string[] firstSegments = firstPath.Split('/');
                string[] secondSegments = secondPath.Split('/');
                int commonSegmentCount = 0;

                while (commonSegmentCount < firstSegments.Length &&
                       commonSegmentCount < secondSegments.Length &&
                       firstSegments[commonSegmentCount].Equals(
                           secondSegments[commonSegmentCount],
                           StringComparison.OrdinalIgnoreCase))
                {
                    commonSegmentCount++;
                }

                return string.Join("/", firstSegments.Take(commonSegmentCount));
            }
        }
    }

    internal sealed class SourceLinkAttributionData
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("classification")]
        public string Classification { get; set; }

        [JsonProperty("classificationReason")]
        public string ClassificationReason { get; set; }

        [JsonProperty("classificationScope")]
        public string ClassificationScope { get; set; }

        [JsonProperty("confidenceRank")]
        public int ConfidenceRank { get; set; }

        [JsonProperty("processedDocumentCount")]
        public int ProcessedDocumentCount { get; set; }

        [JsonProperty("documentLimit")]
        public int DocumentLimit { get; set; }

        [JsonProperty("isComplete")]
        public bool IsComplete { get; set; }

        [JsonProperty("matchedDocumentCount")]
        public int MatchedDocumentCount { get; set; }

        [JsonProperty("unmatchedDocumentCount")]
        public int UnmatchedDocumentCount { get; set; }

        [JsonProperty("processingMilliseconds")]
        public long ProcessingMilliseconds { get; set; }

        [JsonProperty("repositories")]
        public IList<SourceRepositoryAttributionData> Repositories { get; set; }
    }

    internal sealed class SourceRepositoryAttributionData
    {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("organization")]
        public string Organization { get; set; }

        [JsonProperty("project")]
        public string Project { get; set; }

        [JsonProperty("repository")]
        public string Repository { get; set; }

        [JsonProperty("repositoryId")]
        public string RepositoryId { get; set; }

        [JsonProperty("commits")]
        public IList<string> Commits { get; set; }

        [JsonProperty("documentCount")]
        public int DocumentCount { get; set; }

        [JsonProperty("buildMatchRank")]
        public int BuildMatchRank { get; set; }

        [JsonProperty("commonSourcePath")]
        public string CommonSourcePath { get; set; }

        [JsonProperty("sourceFile")]
        public string SourceFile { get; set; }

        [JsonProperty("samplePaths")]
        public IList<string> SamplePaths { get; set; }

        [JsonProperty("sourceUrlPatterns")]
        public IList<string> SourceUrlPatterns { get; set; }
    }
}
