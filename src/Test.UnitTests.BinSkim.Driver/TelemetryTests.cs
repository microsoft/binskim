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
    }
}
