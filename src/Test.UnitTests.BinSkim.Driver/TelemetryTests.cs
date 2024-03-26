// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using FluentAssertions;
using FluentAssertions.Execution;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class TelemetryTests
    {
        [Fact]
        public void Telemetry_TelemetryClientShouldBeNullIfEnvironmentVariablesNotSet()
        {
            using var telemetry = new IL.Sdk.Telemetry();
            telemetry.TelemetryClient.Should().BeNull();
        }

        [Fact]
        public void Telemetry_ShouldInitializeFromIKeyEnvironmentVariable()
        {
            try
            {
                string iKey = Guid.NewGuid().ToString();
                Environment.SetEnvironmentVariable(IL.Sdk.Telemetry.AppInsightsInstrumentationKeyEnvVar, iKey);

                using var telemetry = new IL.Sdk.Telemetry();
                telemetry.TelemetryClient.Should().NotBeNull();
            }
            finally
            {
                // Clear environment variable set for this test.
                Environment.SetEnvironmentVariable(IL.Sdk.Telemetry.AppInsightsInstrumentationKeyEnvVar, null);
            }
        }

        [Fact]
        public void Telemetry_ShouldInitializeFromConnectionStringEnvironmentVariable()
        {
            try
            {
                string connectionString = "InstrumentationKey=" + Guid.NewGuid().ToString();
                Environment.SetEnvironmentVariable(IL.Sdk.Telemetry.AppInsightsConnectionStringEnvVar, connectionString);

                using var telemetry = new IL.Sdk.Telemetry();
                telemetry.TelemetryClient.Should().NotBeNull();
            }
            finally
            {
                // Clear environment variable set for this test.
                Environment.SetEnvironmentVariable(IL.Sdk.Telemetry.AppInsightsConnectionStringEnvVar, null);
            }
        }

        [Fact]
        public void Telemetry_ShouldDisposeTelemetryConfigurationInDispose()
        {
            var telemetryConfiguration = new TelemetryConfiguration
            {
                ConnectionString = "InstrumentationKey=" + Guid.NewGuid().ToString()
            };

            using (var telemetry = new IL.Sdk.Telemetry(telemetryConfiguration))
            {
                telemetryConfiguration.TelemetryChannel.Should().NotBeNull();
            }

            telemetryConfiguration.TelemetryChannel.Should().BeNull();
        }

        [Fact]
        public void Telemetry_ShouldHaveConsistentResultsEnabledOrDisabled()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            WindowsBinaryAndPdbSkimmerBase.s_PdbExceptions.Clear();
            string fileName = Path.Combine(Path.GetTempPath(), "AnalyzeCommand_TelemetryEnableDisableTest.sarif");
            string pathToTestFile = Path.Combine(PEBinaryTests.TestData, "PE", "Native_x64_VS2019_CPlusPlus_DEBUG_DEFAULT.dll");
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[] {
                    pathToTestFile
                },
                Recurse = true,
                OutputFilePath = fileName,
                OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                IgnorePdbLoadError = false,
                DataToInsert = new[] { OptionallyEmittedData.Hashes }
            };

            var command = new MultithreadedAnalyzeCommand();
            options.DisableTelemetry = null;
            command.Run(options);
            var logWithDisableTelemetryNull = SarifLog.Load(fileName);

            options.DisableTelemetry = true;
            command.Run(options);
            var logWithDisableTelemetryTrue = SarifLog.Load(fileName);

            options.DisableTelemetry = false;
            command.Run(options);
            var logWithDisableTelemetryFalse = SarifLog.Load(fileName);

            using (new AssertionScope())
            {
                logWithDisableTelemetryTrue.Runs[0].Results.Should().BeEquivalentTo(logWithDisableTelemetryFalse.Runs[0].Results,
                    "Whether DisableTelemetry is true or false, the results should be the same.");
                logWithDisableTelemetryFalse.Runs[0].Results.Should().BeEquivalentTo(logWithDisableTelemetryNull.Runs[0].Results,
                    "Whether DisableTelemetry is explicitly set to false or not, the results should be the same.");
            }
        }

        [Fact]
        public void Telemetry_ReportElfOrMachoCompilerData_EnabledAndWorkingByDefault()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var assertionScope = new AssertionScope();

            string sarifFile = Path.Combine(Path.GetTempPath(), "AnalyzeCommand_Telemetry_ReportElfOrMachoCompilerData_EnabledByDefault.sarif");
            string csvFile = Path.Combine(Path.GetTempPath(), "AnalyzeCommand_Telemetry_ReportElfOrMachoCompilerData_EnabledByDefault.csv");
            string configFile = Path.Combine(Path.GetTempPath(), "AnalyzeCommand_Telemetry_ReportElfOrMachoCompilerData_EnabledByDefault.xml");

            File.Delete(sarifFile);
            File.Delete(csvFile);
            File.Delete(configFile);

            string configFileContent = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Properties><Properties Key=\"CompilerTelemetry.Options\"><Property Key=\"CsvOutputPath\" Value=\"{csvFile}\"/></Properties></Properties>";
            File.WriteAllText(configFile, configFileContent);

            string pathToTestFile = Path.Combine(PEBinaryTests.TestData, "Dwarf", "hello-dwarf5-o2");
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[] {
                    pathToTestFile
                },
                OutputFilePath = sarifFile,
                OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                DataToInsert = new[] { OptionallyEmittedData.Hashes },
                ConfigurationFilePath = configFile,
            };

            var command = new MultithreadedAnalyzeCommand();
            command.Run(options);

            bool fileExists = File.Exists(csvFile);
            fileExists.Should().BeTrue("because the telemetry is enabled");

            if (fileExists)
            {
                string fileContent = File.ReadAllText(csvFile);
                fileContent.Should().Contain("-gdwarf-5", "because the rule ReportElfOrMachoCompilerData is enabled by default");
            }

            File.Delete(csvFile);
            options.DisableTelemetry = true;
            command.Run(options);
            fileExists = File.Exists(csvFile);
            fileExists.Should().BeFalse("because the telemetry is disabled");
        }
    }
}
