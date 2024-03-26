// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;

using Microsoft.ApplicationInsights;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Microsoft.CodeAnalysis.Sarif.VersionOne;
using Microsoft.CodeAnalysis.Sarif.Visitors;

using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    /// <summary>
    /// This class sends compiler data to AppInsights and/or persists the information
    /// to a CSV file. This information is used to produce manifests of compiler 
    /// versions and other data that are useful to verify the provenance of
    /// code compiled into a project or service.
    /// 
    /// This code is intended to be thread-safe.
    /// </summary>
    public class CompilerDataLogger : IDisposable
    {
        internal static int s_chunkSize = 8192;

        // Constant values sent to AppInsights telemetry stream.
        internal const string SummaryEventName = "AnalysisSummary";
        internal const string CompilerEventName = "CompilerInformation";
        internal const string CommandLineEventName = "CommandLineInformation";
        internal const string AssemblyReferencesEventName = "AssemblyReferencesInformation";

        internal const string ToolName = "toolName";
        internal const string ProjectId = "projectId";
        internal const string SymbolPath = "symbolPath";
        internal const string BuildRunId = "buildRunId";
        internal const string CommandLine = "commandLine";
        internal const string ProjectName = "projectName";
        internal const string ToolVersion = "toolVersion";
        internal const string TimeConsumed = "timeConsumed";
        internal const string RepositoryId = "repositoryId";
        internal const string CommandLineId = "commandLineId";
        internal const string RepositoryName = "repositoryName";
        internal const string OrganizationId = "organizationId";
        internal const string NormalizedPath = "normalizedPath";
        internal const string AnalysisEndTime = "analysisEndTime";
        internal const string OrganizationName = "organizationName";
        internal const string AnalysisStartTime = "analysisStartTime";
        internal const string BuildDefinitionId = "buildDefinitionId";
        internal const string AssemblyReferences = "assemblyReferences";
        internal const string ChunkedCommandLine = "chunkedcommandLine";
        internal const string BuildDefinitionName = "buildDefinitionName";
        internal const string AssemblyReferencesId = "assemblyReferencesId";
        internal const string NumberOfBinaryAnalyzed = "numberOfBinaryAnalyzed";
        internal const string ChunkedAssemblyReferences = "chunkedassemblyReferences";

        // This object is required to synchronize multi-threaded writes
        // to the CSV writer only. The AppInsights client is already
        // thread-safe.
        private readonly object syncRoot;

        // Data for persisting telemetry to AppInsights and/or a CSV file.
        internal StreamWriter writer;
        private readonly TelemetryClient telemetryClient;

        // We currently generate telemetry (such as exceptions that occurred during
        // analysis) that is extracted from the SARIF log file. We currently therefore
        // require that the scan is configured to produce a disk-based report.
        private readonly string sarifOutputFilePath;
        private readonly Sarif.SarifVersion sarifVersion;
        private readonly IFileSystem fileSystem;
        private readonly string symbolPath;

        public bool Enabled => this.telemetryClient != null || this.writer != null;

        public string RootPathToElide { get; set; }

        // We retain the hash-code of the context that was used to initialize this
        // instance. This data is subsequently used to determine what analysis
        // context has the right to dispose of this instance (which is a shared
        // object across many analysis contexts). This ownership mechanism depends
        // on the context GetHashCode() value remaining stable during analysis.
        public int OwningContextHashCode { get; internal set; }

        public static PerLanguageOption<string> CsvOutputPath { get; } =
            new PerLanguageOption<string>(
                "CompilerTelemetry", nameof(CsvOutputPath), defaultValue: () => string.Empty,
                "An output path to which compiler data will be persisted as CSV, e.g., 'c:\\telemetry.csv'.");

        public static PerLanguageOption<string> RootPathToElideProperty { get; } =
            new PerLanguageOption<string>(
                "CompilerTelemetry", nameof(RootPathToElide), defaultValue: () => string.Empty,
                "A non-deterministic file path root that should be elided from paths in telemetry, e.g., 'c:\\Users\\SomeUser\\'.");

        public CompilerDataLogger(string sarifOutputFilePath, Sarif.SarifVersion sarifVersion, BinaryAnalyzerContext context, IFileSystem fileSystem = null, Telemetry telemetry = null)
        {
            this.syncRoot = new object();
            this.fileSystem = fileSystem ?? new FileSystem();

            this.sarifOutputFilePath = sarifOutputFilePath;
            this.sarifVersion = sarifVersion;
            this.RootPathToElide = context.Policy.GetProperty(RootPathToElideProperty);
            this.OwningContextHashCode = context.GetHashCode();
            this.symbolPath = context.SymbolPath;
            this.telemetryClient = telemetry?.TelemetryClient;

            if (!context.DisableTelemetry)
            {
                bool forceOverwrite = context.ForceOverwrite;
                string csvFilePath = context.Policy.GetProperty(CsvOutputPath);
                CreateCsvOutputFile(csvFilePath, forceOverwrite);
            }

            // If the user has configured compiler telemetry collection, then we require analysis results
            // are persisted to a disk-based log file (to produce the telemetry 'summary' data persisted
            // via WriteSummaryData below. Ideally, we wouldn't require this, i.e., why isn't it valid
            // to simply run the tool with console reporting to collect telemetry? The gap here is that
            // we haven't figured out how to collect/summarize all published data, but this is a 
            // solvable problem.
            if (Enabled && string.IsNullOrEmpty(sarifOutputFilePath))
            {
                throw new InvalidOperationException(
                    "Analysis results must currently be persisted to a log file (using " +
                    "the --output argument) to generate compiler telemetry data.");
            }
        }

        internal void CreateCsvOutputFile(string csvFilePath, bool overwriteExistingCsv)
        {
            if (string.IsNullOrWhiteSpace(csvFilePath))
            {
                return;
            }

            if (fileSystem.FileExists(csvFilePath))
            {
                if (!overwriteExistingCsv)
                {
                    throw new InvalidOperationException($"Output file exists and force overwrite was not specified: {csvFilePath}");
                }

                fileSystem.FileDelete(csvFilePath);
            }

            string directoryName = Path.GetDirectoryName(csvFilePath);

            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            this.writer = new StreamWriter(new FileStream(csvFilePath, FileMode.OpenOrCreate));
            PrintHeader();
        }

        public static string RetrieveEnvironmentVariable(string name)
        {
            try
            {
                var targets = new EnvironmentVariableTarget[]
                {
                    EnvironmentVariableTarget.Process,
                    EnvironmentVariableTarget.User,
                    EnvironmentVariableTarget.Machine
                };

                foreach (EnvironmentVariableTarget target in targets)
                {
                    string value = Environment.GetEnvironmentVariable(name, target);
                    if (!string.IsNullOrEmpty(value)) { return value; }
                }
            }
            catch (SecurityException)
            {
                // User does not have access to retrieve information from environment variables.
            }

            return string.Empty;
        }

        private void PrintHeader()
        {
            string header = "" +
                "Target,Compiler Name,Compiler BackEnd Version,Compiler FrontEnd Version," +
                "File Version,Binary Type,Language,Debugging FileName,Debugging FileGuid," +
                "Debugging FileLastModifiedDateUTC, Target LastModifiedDateUTC, Command Line," +
                "Dialect,Module Name,Module Library,Hash,Error";

            WriteToCsv(header);
        }

        private void WriteToCsv(string line)
        {
            if (this.writer == null) { return; }

            lock (this.syncRoot)
            {
                this.writer.WriteLine(line);
            }
        }

        public void Write(BinaryAnalyzerContext context, CompilerData compilerData)
        {
            string fileHash = context.Hashes?.Sha256;
            string filePath = string.IsNullOrWhiteSpace(RootPathToElide)
                ? context.CurrentTarget.Uri?.LocalPath
                : context.CurrentTarget.Uri?.LocalPath.Replace(RootPathToElide, string.Empty);

            WriteToCsv($"{filePath},{compilerData},{fileHash},");

            if (this.telemetryClient == null)
            {
                return;
            }

            var properties = new Dictionary<string, string>
            {
                { "target", filePath },
                { "compilerName", compilerData.CompilerName },
                { "compilerBackEndVersion", compilerData.CompilerBackEndVersion },
                { "compilerFrontEndVersion", compilerData.CompilerFrontEndVersion },
                { "fileVersion", compilerData.FileVersion ?? string.Empty },
                { "binaryType", compilerData.BinaryType },
                { "language", compilerData.Language },
                { "debuggingFileName", compilerData.DebuggingFileName ?? string.Empty },
                { "debuggingGuid", compilerData.DebuggingFileGuid ?? string.Empty },
                { "debuggingFileLastModifiedDateUTC", compilerData.DebuggingFileLastModifiedDateUtc ?? string.Empty },
                { "targetLastModifiedDateUTC", compilerData.TargetLastModifiedDateUtc ?? string.Empty },
                { "dialect", compilerData.Dialect },
                { "moduleName", compilerData.ModuleName ?? string.Empty },
                { "moduleLibrary", (compilerData.ModuleName == compilerData.ModuleLibrary ? string.Empty : compilerData.ModuleLibrary ?? string.Empty) },
                { "hash", fileHash },
                { "error", string.Empty }
            };

            if (!string.IsNullOrWhiteSpace(compilerData.CommandLine))
            {
                string commandLineId = Guid.NewGuid().ToString();
                properties.Add(CommandLineId, commandLineId);

                SendChunkedContent(CommandLineEventName,
                                   commandLineId,
                                   CommandLine,
                                   compilerData.CommandLine);
            }

            if (!string.IsNullOrWhiteSpace(compilerData.AssemblyReferences))
            {
                string assemblyReferencesId = Guid.NewGuid().ToString();
                properties.Add(AssemblyReferencesId, assemblyReferencesId);

                SendChunkedContent(AssemblyReferencesEventName,
                                   assemblyReferencesId,
                                   AssemblyReferences,
                                   compilerData.AssemblyReferences);
            }

            // To prevent loss of relationship between compiler and assembly/commandline data
            // this event cannot be sent before AssemblyReferences or CommandLine.
            this.telemetryClient.TrackEvent(CompilerEventName, properties: properties);
        }

        public void WriteException(BinaryAnalyzerContext context, string errorMessage)
        {
            string fileHash = context.Hashes?.Sha256;

            string filePath = RootPathToElide == null
                ? context.CurrentTarget.Uri?.LocalPath.Replace(RootPathToElide, string.Empty)
                : context.CurrentTarget.Uri?.LocalPath;

            WriteToCsv($"{filePath},,,,,,,,,,,,,{fileHash},{errorMessage}");

            if (this.telemetryClient == null)
            {
                return;
            }

            this.telemetryClient.TrackEvent(CompilerEventName, properties: new Dictionary<string, string>
            {
                { "target", filePath },
                { "compilerName", string.Empty },
                { "commandLine", string.Empty },
                { "compilerBackEndVersion", string.Empty },
                { "compilerFrontEndVersion", string.Empty },
                { "fileVersion", string.Empty },
                { "binaryType", string.Empty },
                { "language", string.Empty },
                { "debuggingFileName", string.Empty },
                { "debuggingGuid", string.Empty },
                { "debuggingFileLastModifiedDateUTC", string.Empty },
                { "targetLastModifiedDateUTC", string.Empty },
                { "dialect", string.Empty },
                { "moduleName", string.Empty },
                { "moduleLibrary", string.Empty },
                { "hash", fileHash },
                { "error", errorMessage },
            });
        }

        public void WriteException(ExecutionException exception, AnalysisSummary summary)
        {
            this.writer?.WriteLine(exception.ToString());

            if (this.telemetryClient != null)
            {
                this.telemetryClient.TrackException(exception, new Dictionary<string, string>
                {
                    { "toolName", summary.ToolName },
                    { "toolVersion", summary.ToolVersion },
                });
            }
        }

        internal void Summarize(AnalysisSummary summary)
        {
            WriteToCsv(summary.ToString());

            if (this.telemetryClient == null)
            {
                return;
            }

            this.telemetryClient.TrackEvent(SummaryEventName, properties: new Dictionary<string, string>
            {
                { "toolName", summary.ToolName },
                { "toolVersion", summary.ToolVersion },
                { "normalizedPath", summary.NormalizedPath },
                { "symbolPath", summary.SymbolPath },
                { "numberOfBinaryAnalyzed", summary.FileAnalyzed.ToString() },
                { "analysisStartTime", summary.StartTimeUtc.ToString() },
                { "analysisEndTime", summary.EndTimeUtc.ToString() },
                { "timeConsumed", summary.TimeConsumed.ToString() },
                { "buildDefinitionId", summary.BuildDefinitionId },
                { "buildDefinitionName", summary.BuildDefinitionName },
                { "buildRunId", summary.BuildRunId },
                { "repositoryId", summary.RepositoryId },
                { "repositoryName", summary.RepositoryName },
                { "organizationId", summary.OrganizationId },
                { "organizationName", summary.OrganizationName },
                { "projectId", summary.ProjectId },
                { "projectName", summary.ProjectName },
            });
        }

        internal void SendChunkedContent(string eventName, string contentId, string contentName, string content)
        {
            int size = CalculateChunkedContentSize(content.Length);
            for (int i = 0; i < content.Length; i += s_chunkSize)
            {
                string chunkedContent = content.Substring(i, Math.Min(s_chunkSize, content.Length - i));

                this.telemetryClient.TrackEvent(eventName, properties: new Dictionary<string, string>
                {
                    { $"{contentName}Id", contentId },
                    { "orderNumber", (i + 1).ToString() },
                    { "totalNumber", size.ToString() },
                    { $"chunked{contentName}", chunkedContent },
                });
            }
        }

        internal int CalculateChunkedContentSize(int contentLength)
        {
            return (int)Math.Ceiling(1.0 * contentLength / s_chunkSize);
        }

        private void WriteSummaryData()
        {
            Debug.Assert(Enabled);

            SarifLog sarifLog = LoadVersionAgnosticSarifFile();
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifLog,
                                                                                      RootPathToElide,
                                                                                      this.symbolPath);
            Summarize(summary);

            foreach (ExecutionException ex in AnalysisSummaryExtractor.ExtractExceptionData(sarifLog))
            {
                WriteException(ex, summary);
            }
        }

        private SarifLog LoadVersionAgnosticSarifFile()
        {
            SarifLog sarifLog;

            if (this.sarifVersion == Sarif.SarifVersion.Current)
            {
                sarifLog = SarifLog.Load(sarifOutputFilePath);
            }
            else
            {
                SarifLogVersionOne actualLog;

                var serializer = new JsonSerializer() { ContractResolver = SarifContractResolverVersionOne.Instance };

                using (var reader = new JsonTextReader(new StreamReader(fileSystem.FileOpenRead(sarifOutputFilePath))))
                {
                    actualLog = serializer.Deserialize<SarifLogVersionOne>(reader);
                }

                var visitor = new SarifVersionOneToCurrentVisitor();
                visitor.VisitSarifLogVersionOne(actualLog);
                sarifLog = visitor.SarifLog;
            }

            return sarifLog;
        }

        public void Dispose()
        {
            if (!Enabled)
            {
                return;
            }

            // Generate the last of our published telemetry data.
            WriteSummaryData();

            // Flush and close output to our csv file, if present.
            this.writer?.Dispose();
            this.writer = null;
        }
    }
}
