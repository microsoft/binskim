// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;

using Newtonsoft.Json;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class SourceLinkAttributionTests
    {
        [Fact]
        public void Calculate_ShouldMatchBuildRepositoryByRepositoryId()
        {
            string repositoryId = "75fff74e-7c15-446c-ae4f-65564a4847d9";
            string sourceLinkJson = CreateSourceLink(
                (@"C:\__w\1\s\*",
                 $"https://dev.azure.com/msazure/One/_apis/git/repositories/{repositoryId}/items?path=/*&versionType=Commit&version=33f4d26994bfa98af3d5c93aac1bece69cf02346&api-version=7.0"));
            var buildContext = new AnalysisSummary
            {
                OrganizationName = "msazure",
                ProjectName = "One",
                RepositoryName = "Networking-deployment",
                RepositoryId = repositoryId,
            };

            SourceLinkAttributionData result = SourceLinkAttributionCalculator.Calculate(
                sourceLinkJson,
                new[] { @"C:\__w\1\s\src\a.cpp", @"C:\__w\1\s\src\b.cpp" },
                buildContext);

            result.Classification.Should().Be("BuildRepositoryOnly");
            result.ClassificationReason.Should().Be("SourceRepositoryIdMatchesBuildRepositoryId");
            result.ConfidenceRank.Should().Be(4);
            result.ProcessedDocumentCount.Should().Be(2);
            result.IsComplete.Should().BeTrue();
            result.MatchedDocumentCount.Should().Be(2);
            result.UnmatchedDocumentCount.Should().Be(0);
            result.Repositories.Should().ContainSingle();
            result.Repositories[0].DocumentCount.Should().Be(2);
            result.Repositories[0].CommonSourcePath.Should().Be("src");
            result.Repositories[0].SourceFile.Should().BeEmpty();
            result.Repositories[0].SamplePaths.Should().Contain(new[] { "src/a.cpp", "src/b.cpp" });
        }

        [Fact]
        public void MergeSourceLinkJsonDocuments_ShouldCombineNativePdbStreams()
        {
            string first = CreateSourceLink(
                (@"C:\src\first\*",
                 "https://raw.githubusercontent.com/example/first/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/*"));
            string second = CreateSourceLink(
                (@"C:\src\second\*",
                 "https://raw.githubusercontent.com/example/second/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/*"));

            string merged = SourceLinkAttributionCalculator.MergeSourceLinkJsonDocuments(new[] { first, second });
            SourceLinkAttributionData result = SourceLinkAttributionCalculator.Calculate(
                merged,
                new[] { @"C:\src\first\a.cpp", @"C:\src\second\b.cpp" },
                new AnalysisSummary());

            result.IsComplete.Should().BeTrue();
            result.MatchedDocumentCount.Should().Be(2);
            result.Repositories.Should().HaveCount(2);
        }

        [Fact]
        public void Calculate_ShouldUseMostSpecificMappingAndClassifyMixedRepositories()
        {
            string buildRepositoryId = "11111111-1111-1111-1111-111111111111";
            string dependencyRepositoryId = "22222222-2222-2222-2222-222222222222";
            string sourceLinkJson = CreateSourceLink(
                (@"C:\src\*",
                 $"https://dev.azure.com/example/Project/_apis/git/repositories/{buildRepositoryId}/items?path=/*&versionType=Commit&version=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                (@"C:\src\dependency\*",
                 $"https://dev.azure.com/example/Project/_apis/git/repositories/{dependencyRepositoryId}/items?path=/*&versionType=Commit&version=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
            var buildContext = new AnalysisSummary
            {
                OrganizationName = "example",
                ProjectName = "Project",
                RepositoryName = "Product",
                RepositoryId = buildRepositoryId,
            };

            SourceLinkAttributionData result = SourceLinkAttributionCalculator.Calculate(
                sourceLinkJson,
                new[] { @"C:\src\product.cpp", @"C:\src\dependency\dependency.cpp" },
                buildContext);

            result.Classification.Should().Be("MixedRepositories");
            result.ClassificationReason.Should().Be("SourceLinkDocumentsMatchBuildAndOtherRepositories");
            result.Repositories.Should().HaveCount(2);
            result.Repositories.Should().Contain(repository =>
                repository.RepositoryId == buildRepositoryId &&
                repository.BuildMatchRank == 4 &&
                repository.DocumentCount == 1);
            result.Repositories.Should().Contain(repository =>
                repository.RepositoryId == dependencyRepositoryId &&
                repository.BuildMatchRank == 0 &&
                repository.DocumentCount == 1 &&
                repository.SamplePaths.Contains("dependency.cpp"));
        }

        [Fact]
        public void Calculate_ShouldClassifyOnlyOtherRepositories()
        {
            string sourceLinkJson = CreateSourceLink(
                ("/_/*",
                 "https://raw.githubusercontent.com/xunit/xunit/82543a6df6f5f13b5b70f8a9f9ccb41cd676084f/*"));
            var buildContext = new AnalysisSummary
            {
                OrganizationName = "office",
                ProjectName = "ISS",
                RepositoryName = "augloop-workflows",
                RepositoryId = "33333333-3333-3333-3333-333333333333",
            };

            SourceLinkAttributionData result = SourceLinkAttributionCalculator.Calculate(
                sourceLinkJson,
                new[] { "/_/src/xunit.core/Sdk/ITest.cs" },
                buildContext);

            result.Classification.Should().Be("OnlyOtherRepositories");
            result.ClassificationReason.Should().Be("NoSourceRepositoryMatchesBuildRepository");
            result.Repositories.Should().ContainSingle();
            result.Repositories[0].Host.Should().Be("github.com");
            result.Repositories[0].Organization.Should().Be("xunit");
            result.Repositories[0].Repository.Should().Be("xunit");
            result.Repositories[0].Commits.Should().ContainSingle()
                .Which.Should().Be("82543a6df6f5f13b5b70f8a9f9ccb41cd676084f");
            result.Repositories[0].SourceFile.Should().Be("src/xunit.core/Sdk/ITest.cs");
            result.Repositories[0].CommonSourcePath.Should().Be("src/xunit.core/Sdk");
        }

        [Fact]
        public void Calculate_ShouldParseLegacyVisualStudioRepositoryUrl()
        {
            string sourceLinkJson = CreateSourceLink(
                (@"F:\dbs\el\csdb\*",
                 "https://msdata.visualstudio.com/DefaultCollection/CosmosDB/_apis/git/repositories/CosmosDB/items?path=/*&versionType=commit&version=a07f192c6ab0f715b59c1f33235ec8f64fa522b9"));
            var buildContext = new AnalysisSummary
            {
                OrganizationName = "https://msdata.visualstudio.com/",
                ProjectName = "CosmosDB",
                RepositoryName = "CosmosDB",
            };

            SourceLinkAttributionData result = SourceLinkAttributionCalculator.Calculate(
                sourceLinkJson,
                new[] { @"F:\dbs\el\csdb\Product\service.cpp" },
                buildContext);

            result.Classification.Should().Be("BuildRepositoryOnly");
            result.ConfidenceRank.Should().Be(3);
            result.Repositories.Should().ContainSingle();
            result.Repositories[0].Organization.Should().Be("msdata");
            result.Repositories[0].Project.Should().Be("CosmosDB");
            result.Repositories[0].Repository.Should().Be("CosmosDB");
        }

        [Fact]
        public void Calculate_ShouldReportUnmatchedDocuments()
        {
            string sourceLinkJson = CreateSourceLink(
                (@"C:\src\*",
                 "https://raw.githubusercontent.com/example/product/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/*"));

            SourceLinkAttributionData result = SourceLinkAttributionCalculator.Calculate(
                sourceLinkJson,
                new[] { @"C:\src\matched.cpp", @"D:\other\unmatched.cpp" },
                new AnalysisSummary());

            result.Classification.Should().Be("SingleRepository");
            result.ClassificationReason.Should().Be("BuildRepositoryIdentityUnavailable");
            result.ProcessedDocumentCount.Should().Be(2);
            result.MatchedDocumentCount.Should().Be(1);
            result.UnmatchedDocumentCount.Should().Be(1);
        }

        [Fact]
        public void Calculate_ShouldReturnIncomplete_WhenDocumentLimitIsExceeded()
        {
            string sourceLinkJson = CreateSourceLink(
                (@"C:\src\*",
                 "https://raw.githubusercontent.com/example/product/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/*"));
            IEnumerable<string> documents = Enumerable.Range(0, SourceLinkAttributionCalculator.MaxPdbDocumentCount + 1)
                .Select(index => $@"C:\src\file{index}.cpp");

            SourceLinkAttributionData result = SourceLinkAttributionCalculator.Calculate(
                sourceLinkJson,
                documents,
                new AnalysisSummary());

            result.Classification.Should().Be("Incomplete");
            result.ClassificationReason.Should().Be("PdbDocumentLimitExceeded");
            result.ConfidenceRank.Should().Be(0);
            result.IsComplete.Should().BeFalse();
            result.DocumentLimit.Should().Be(SourceLinkAttributionCalculator.MaxPdbDocumentCount);
            result.ProcessedDocumentCount.Should().Be(SourceLinkAttributionCalculator.MaxPdbDocumentCount);
        }

        private static string CreateSourceLink(params (string DocumentPattern, string SourceUrlPattern)[] mappings)
        {
            var documents = new Dictionary<string, string>();
            foreach ((string documentPattern, string sourceUrlPattern) in mappings)
            {
                documents.Add(documentPattern, sourceUrlPattern);
            }

            return JsonConvert.SerializeObject(new { documents });
        }
    }
}
