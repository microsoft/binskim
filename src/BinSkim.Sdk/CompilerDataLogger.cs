// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading.Tasks;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class CompilerDataLogger
    {
        private const string CompilerEventName = "CompilerInformation";
        private const string CommandLineEventName = "CommandLineInformation";
        private const string AssemblyReferencesEventName = "AssemblyReferencesInformation";
        private const string SummaryEventName = "AnalysisSummary";

        private readonly int ChunkSize = 8192;
        private readonly bool appInsightsRegistered;
        private readonly string sha256;
        private readonly string relativeFilePath;

        private static string s_sessionId;
        private static bool s_printHeader = true;
        private static TelemetryClient s_telemetryClient;
        private static TelemetryConfiguration s_telemetryConfiguration;

        public static bool TelemetryEnabled => s_telemetryClient != null;

        public CompilerDataLogger(IAnalysisContext analysisContext,
                                  IEnumerable<string> targetFileSpecifiers,
                                  TelemetryConfiguration telemetryConfiguration = null,
                                  TelemetryClient telemetryClient = null,
                                  int chunkSize = 8192)
        {
            s_telemetryConfiguration = telemetryConfiguration;
            s_telemetryClient = telemetryClient;
            s_sessionId = TelemetryEnabled ? Guid.NewGuid().ToString() : null;
            ChunkSize = chunkSize;

            if (!TelemetryEnabled)
            {
                try
                {

                    string appInsightsKey = RetrieveAppInsightsKey();
                    if (!string.IsNullOrEmpty(appInsightsKey) && Guid.TryParse(appInsightsKey, out _))
                    {
                        Initialize(appInsightsKey);
                        this.appInsightsRegistered = true;
                    }
                }
                catch (SecurityException)
                {
                    // User does not have access to retrieve information from environment variables.
                }
            }

            this.sha256 = analysisContext?.Hashes?.Sha256 ?? string.Empty;
            this.relativeFilePath = analysisContext?.TargetUri?.LocalPath ?? string.Empty;

            foreach (string path in targetFileSpecifiers)
            {
                // We must get directory name because there are cases where the targetFilePath is
                // c:\path\*.dll
                string directoryName = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directoryName))
                {
                    this.relativeFilePath = this.relativeFilePath.Replace(directoryName, string.Empty);
                }
            }
        }

        public static void Initialize(string instrumentationKey)
        {
            if (s_telemetryConfiguration == null && s_telemetryClient == null)
            {
                s_sessionId = Guid.NewGuid().ToString();
                s_telemetryConfiguration = new TelemetryConfiguration(instrumentationKey);
                s_telemetryClient = new TelemetryClient(s_telemetryConfiguration);
            }
        }

        public static string RetrieveAppInsightsKey()
        {
            string appInsightsKey = string.Empty;

            try
            {
                appInsightsKey = Environment.GetEnvironmentVariable("BinskimAppInsightsKey");
                if (string.IsNullOrEmpty(appInsightsKey))
                {
                    appInsightsKey = Environment.GetEnvironmentVariable("BinskimAppInsightsKey", EnvironmentVariableTarget.User);
                    if (string.IsNullOrEmpty(appInsightsKey))
                    {
                        appInsightsKey = Environment.GetEnvironmentVariable("BinskimAppInsightsKey", EnvironmentVariableTarget.Machine);
                    }
                }
            }
            catch (SecurityException)
            {
                // User does not have access to retrieve information from environment variables.
            }

            return appInsightsKey;
        }

        public static void Flush()
        {
            if (TelemetryEnabled)
            {
                s_telemetryClient.Flush();

                // flush is not blocking when not using InMemoryChannel so wait a bit. There is an active issue regarding the need for `Sleep`/`Delay`
                // which is tracked here: https://github.com/microsoft/ApplicationInsights-dotnet/issues/407
                Task.Delay(5000).Wait();
            }
        }

        public void PrintHeader()
        {
            if (!this.appInsightsRegistered)
            {
                if (s_printHeader)
                {
                    Console.WriteLine("Target,Compiler Name,Compiler BackEnd Version,Compiler FrontEnd Version,File Version,Binary Type,Language,Debugging FileName,Debugging FileGuid,Command Line,Dialect,Module Name,Module Library,Hash,Error");
                    s_printHeader = false;
                }
            }
        }

        public void Write(CompilerData compilerData)
        {
            if (TelemetryEnabled)
            {
                string commandLineId = string.Empty;
                string assemblyReferencesId = string.Empty;
                var properties = new Dictionary<string, string>
                {
                    { "target", this.relativeFilePath },
                    { "compilerName", compilerData.CompilerName },
                    { "compilerBackEndVersion", compilerData.CompilerBackEndVersion },
                    { "compilerFrontEndVersion", compilerData.CompilerFrontEndVersion },
                    { "fileVersion", compilerData.FileVersion ?? string.Empty },
                    { "binaryType", compilerData.BinaryType },
                    { "language", compilerData.Language },
                    { "debuggingFileName", compilerData.DebuggingFileName ?? string.Empty },
                    { "debuggingGuid", compilerData.DebuggingFileGuid ?? string.Empty },
                    { "dialect", compilerData.Dialect },
                    { "moduleName", compilerData.ModuleName ?? string.Empty },
                    { "moduleLibrary", (compilerData.ModuleName == compilerData.ModuleLibrary ? string.Empty : compilerData.ModuleLibrary ?? string.Empty) },
                    { "sessionId", s_sessionId },
                    { "hash", this.sha256 },
                    { "error", string.Empty }
                };

                if (!string.IsNullOrWhiteSpace(compilerData.CommandLine))
                {
                    commandLineId = Guid.NewGuid().ToString();
                    properties.Add("commandLineId", commandLineId);
                }

                if (!string.IsNullOrWhiteSpace(compilerData.AssemblyReferences))
                {
                    assemblyReferencesId = Guid.NewGuid().ToString();
                    properties.Add("assemblyReferencesId", assemblyReferencesId);
                }

                s_telemetryClient.TrackEvent(CompilerEventName, properties: properties);

                // send big size content in chunked pieces
                if (!string.IsNullOrWhiteSpace(commandLineId))
                {
                    SendChunkedContent(CommandLineEventName, commandLineId, "commandLine", compilerData.CommandLine);
                }

                if (!string.IsNullOrWhiteSpace(assemblyReferencesId))
                {
                    SendChunkedContent(AssemblyReferencesEventName, assemblyReferencesId, "assemblyReferences", compilerData.AssemblyReferences);
                }
            }
            else
            {
                string log = $"{this.relativeFilePath},{compilerData},{this.sha256},";
                Console.WriteLine(log);
            }
        }

        public void WriteException(string errorMessage)
        {
            if (TelemetryEnabled)
            {
                s_telemetryClient.TrackEvent(CompilerEventName, properties: new Dictionary<string, string>
                {
                    { "target", this.relativeFilePath },
                    { "compilerName", string.Empty },
                    { "commandLine", string.Empty },
                    { "compilerBackEndVersion", string.Empty },
                    { "compilerFrontEndVersion", string.Empty },
                    { "fileVersion", string.Empty },
                    { "binaryType", string.Empty },
                    { "language", string.Empty },
                    { "debuggingFileName", string.Empty },
                    { "debuggingGuid", string.Empty },
                    { "dialect", string.Empty },
                    { "moduleName", string.Empty },
                    { "moduleLibrary", string.Empty },
                    { "sessionId", s_sessionId },
                    { "hash", this.sha256 },
                    { "error", errorMessage },
                });
            }
            else
            {
                string log = $"{this.relativeFilePath},,,,,,,,,,,,,{this.sha256},{errorMessage}";
                Console.WriteLine(log);
            }
        }

        public static void WriteException(ExecutionException exception, AnalysisSummary summary)
        {
            if (TelemetryEnabled)
            {
                s_telemetryClient.TrackException(exception, new Dictionary<string, string>
                {
                    { "toolName", summary.ToolName },
                    { "toolVersion", summary.ToolVersion },
                    { "sessionId", s_sessionId },
                });
            }
            else
            {
                Console.WriteLine(exception.ToString());
            }
        }

        public static void Summarize(AnalysisSummary summary)
        {
            if (TelemetryEnabled)
            {
                s_telemetryClient.TrackEvent(SummaryEventName, properties: new Dictionary<string, string>
                {
                    { "toolName", summary.ToolName },
                    { "toolVersion", summary.ToolVersion },
                    { "sessionId", s_sessionId },
                    { "normalizedPath", summary.NormalizedPath },
                    { "symbolPath", summary.SymbolPath },
                    { "numberOfBinaryAnalyzed", summary.FileAnalyzed.ToString() },
                    { "analysisStartTime", summary.StartTimeUtc.ToString() },
                    { "analysisEndTime", summary.EndTimeUtc.ToString() },
                    { "timeConsumed", summary.TimeConsumed.ToString() },
                    { "buildDefinitionId", summary.BuildDefinitionId },
                    { "buildDefinitionName", summary.BuildDefinitionName },
                    { "buildRunId", summary.BuildRunId },
                });
            }
            else
            {
                Console.WriteLine(summary.ToString());
            }
        }

        private void SendChunkedContent(string eventName, string contentId, string contentName, string content)
        {
            int j = 1;
            int size = (int)Math.Ceiling(1.0 * content.Length / ChunkSize);
            for (int i = 0; i < content.Length; i += ChunkSize)
            {
                string chunckedContent = content.Substring(i, Math.Min(ChunkSize, content.Length - i));

                s_telemetryClient.TrackEvent(eventName, properties: new Dictionary<string, string>
                {
                    { "sessionId", s_sessionId },
                    { $"{contentName}Id", contentId },
                    { "orderNumber", j.ToString() },
                    { "totalNumber", size.ToString() },
                    { $"chunked{contentName}", chunckedContent },
                });
                j++;
            }
        }
    }
}
