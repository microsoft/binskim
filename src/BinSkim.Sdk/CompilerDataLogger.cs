// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class CompilerDataLogger : ICompilerDataLogger
    {
        private const string CompilerEventName = "CompilerInformation";

        private readonly bool appInsightsRegistered;

        private readonly string sha256;
        private readonly string localPath;

        private static TelemetryClient s_telemetryClient;
        private static TelemetryConfiguration s_telemetryConfiguration;

        public CompilerDataLogger(IAnalysisContext analysisContext)
        {
            if (analysisContext.Policy != null && analysisContext.Policy.TryGetValue("ApplicationInsights", out object kv))
            {
                string instrumentationKey = (kv as PropertiesDictionary)["InstrumentationKey"].ToString();
                this.appInsightsRegistered = !string.IsNullOrEmpty(instrumentationKey);

                if (s_telemetryClient == null && Guid.TryParse(instrumentationKey, out _))
                {
                    Initialize(instrumentationKey);
                }
            }

            this.sha256 = analysisContext?.Hashes?.Sha256;
            this.localPath = analysisContext?.TargetUri?.LocalPath;
        }

        public static void Initialize(string instrumentationKey)
        {
            s_telemetryConfiguration = new TelemetryConfiguration(instrumentationKey);
            s_telemetryClient = new TelemetryClient(s_telemetryConfiguration);
        }

        public void PrintHeader()
        {
            if (!appInsightsRegistered)
            {
                Console.WriteLine("Target,Compiler Name,Compiler BackEnd Version,Compiler FrontEnd Version,Language,Module Name,Module Library,Hash,Error");
            }
        }

        public void Write(string compilerData, ObjectModuleDetails omDetails)
        {
            string name = omDetails.Name?.Replace(",", "_");
            string library = omDetails.Library?.Replace(",", ";");
            if (appInsightsRegistered)
            {
                string[] compilerDataParts = compilerData.Split(',');

                s_telemetryClient.TrackEvent(CompilerEventName, properties: new Dictionary<string, string>
                {
                    { "target", this.localPath },
                    { "compilerName", compilerDataParts[0] },
                    { "compilerBackEndVersion", compilerDataParts[1] },
                    { "compilerFrontEndVersion", compilerDataParts[2] },
                    { "language", compilerDataParts[3] },
                    { "moduleName", name },
                    { "moduleLibrary", (name == library ? string.Empty : library) },
                    { "hash", this.sha256 },
                    { "error", string.Empty }
                });
            }
            else
            {
                Console.Write($"{this.localPath},");
                Console.Write($"{compilerData},");
                Console.Write($"{name},");
                Console.Write($"{(name == library ? string.Empty : library)},");
                Console.Write($"{this.sha256},");
                Console.WriteLine();
            }
        }

        public void Write(string compilerName, string version, string language, string file)
        {
            if (appInsightsRegistered)
            {
                s_telemetryClient.TrackEvent(CompilerEventName, properties: new Dictionary<string, string>
                {
                    { "target", this.localPath },
                    { "compilerName", compilerName },
                    { "compilerBackEndVersion", version },
                    { "compilerFrontEndVersion", version },
                    { "language", language },
                    { "moduleName", file },
                    { "moduleLibrary", string.Empty },
                    { "hash", this.sha256 },
                    { "error", string.Empty }
                });
            }
            else
            {
                Console.Write($"{this.localPath},");
                Console.Write($"{compilerName},");
                Console.Write($"{version},");
                Console.Write($"{version},");
                Console.Write($"{language},");
                Console.Write($"{file},");
                Console.Write(",");
                Console.Write($"{this.sha256},");
                Console.WriteLine();
            }
        }

        public void WriteException(string errorMessage)
        {
            if (appInsightsRegistered)
            {
                s_telemetryClient.TrackEvent(CompilerEventName, properties: new Dictionary<string, string>
                {
                    { "target", this.localPath },
                    { "compilerName", string.Empty },
                    { "compilerBackEndVersion", string.Empty },
                    { "compilerFrontEndVersion", string.Empty },
                    { "language", string.Empty },
                    { "moduleName", string.Empty },
                    { "moduleLibrary", string.Empty },
                    { "hash", this.sha256 },
                    { "error", errorMessage }
                });
            }
            else
            {
                Console.Write(this.localPath + ",");
                Console.WriteLine($",,,,,,{this.sha256},{errorMessage}");
            }
        }
    }
}
