// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class CompilerDataLogger
    {
        private const string CompilerEventName = "CompilerInformation";

        private readonly bool appInsightsRegistered;

        private readonly string sha256;
        private readonly string localPath;
        private readonly string repositoryUri;
        private readonly string pipelineName;

        private static TelemetryClient s_telemetryClient;
        private static TelemetryConfiguration s_telemetryConfiguration;

        public CompilerDataLogger(IAnalysisContext analysisContext, string repositoryUri, string pipelineName)
        {
            try
            {
                string appInsightsKey = Environment.GetEnvironmentVariable("BinskimAppInsightsKey");
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

            if (string.IsNullOrEmpty(pipelineName))
            {
                throw new ArgumentNullException(nameof(pipelineName));
            }

            if (string.IsNullOrEmpty(repositoryUri))
            {
                throw new ArgumentNullException(nameof(repositoryUri));
            }

            this.pipelineName = pipelineName;
            this.repositoryUri = repositoryUri;
            this.localPath = analysisContext?.TargetUri?.LocalPath;
            this.sha256 = analysisContext?.Hashes?.Sha256 ?? string.Empty;
        }

        public static void Initialize(string instrumentationKey)
        {
            s_telemetryConfiguration = new TelemetryConfiguration(instrumentationKey);
            s_telemetryClient = new TelemetryClient(s_telemetryConfiguration);
        }

        public static void Flush()
        {
            s_telemetryClient?.Flush();
        }

        public void PrintHeader()
        {
            if (!this.appInsightsRegistered)
            {
                Console.WriteLine("RepositoryUri,PipelineName,Target,Compiler Name,Compiler BackEnd Version,Compiler FrontEnd Version,Language,Dialect,Module Name,Module Library,Hash,Error");
            }
        }

        public void Write(string compilerData, ObjectModuleDetails omDetails)
        {
            string name = omDetails?.Name?.Replace(",", "_");
            string library = omDetails?.Library?.Replace(",", ";");
            if (this.appInsightsRegistered)
            {
                string[] compilerDataParts = compilerData.Split(',');

                s_telemetryClient.TrackEvent(CompilerEventName, properties: new Dictionary<string, string>
                {
                    { "repositoryUri", this.repositoryUri },
                    { "pipelineName", this.pipelineName },
                    { "target", this.localPath },
                    { "compilerName", compilerDataParts[0] },
                    { "compilerBackEndVersion", compilerDataParts[1] },
                    { "compilerFrontEndVersion", compilerDataParts[2] },
                    { "language", compilerDataParts[3] },
                    { "dialect", string.Empty },
                    { "moduleName", name ?? string.Empty },
                    { "moduleLibrary", (name == library ? string.Empty : library) },
                    { "hash", this.sha256 ?? string.Empty },
                    { "error", string.Empty }
                });
            }
            else
            {
                string log = $"{this.repositoryUri},{this.pipelineName},{this.localPath}," +
                    $"{compilerData},,{name},{(name == library ? string.Empty : library)},{this.sha256},";
                Console.WriteLine(log);
            }
        }

        public void Write(string compilerName, string version, string language, string file)
        {
            if (this.appInsightsRegistered)
            {
                s_telemetryClient.TrackEvent(CompilerEventName, properties: new Dictionary<string, string>
                {
                    { "repositoryUri", this.repositoryUri },
                    { "pipelineName", this.pipelineName },
                    { "target", this.localPath },
                    { "compilerName", compilerName ?? string.Empty },
                    { "compilerBackEndVersion", version ?? string.Empty },
                    { "compilerFrontEndVersion", version ?? string.Empty },
                    { "language", language ?? string.Empty },
                    { "dialect", string.Empty },
                    { "moduleName", file ?? string.Empty },
                    { "moduleLibrary", string.Empty },
                    { "hash", this.sha256 ?? string.Empty },
                    { "error", string.Empty }
                });
            }
            else
            {
                string log = $"{this.repositoryUri},{this.pipelineName},{this.localPath}," +
                    $"{compilerName},{version},{version},{language},,{file},,{this.sha256},";
                Console.WriteLine(log);
            }
        }

        public void WriteException(string errorMessage)
        {
            if (this.appInsightsRegistered)
            {
                s_telemetryClient.TrackEvent(CompilerEventName, properties: new Dictionary<string, string>
                {
                    { "repositoryUri", this.repositoryUri },
                    { "pipelineName", this.pipelineName },
                    { "target", this.localPath },
                    { "compilerName", string.Empty },
                    { "compilerBackEndVersion", string.Empty },
                    { "compilerFrontEndVersion", string.Empty },
                    { "language", string.Empty },
                    { "dialect", string.Empty },
                    { "moduleName", string.Empty },
                    { "moduleLibrary", string.Empty },
                    { "hash", this.sha256 ?? string.Empty },
                    { "error", errorMessage }
                });
            }
            else
            {
                string log = $"{this.repositoryUri},{this.pipelineName},{this.localPath},,,,,,,,{this.sha256 ?? string.Empty},{errorMessage}";
                Console.WriteLine(log);
            }
        }
    }
}
