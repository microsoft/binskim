// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FluentAssertions;
using FluentAssertions.Execution;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class TelemetryTests
    {
        [Fact]
        public void Telemetry_TelemetryClientShouldBeNullIfEnvironmentVariablesNotSet()
        {
            using var telemetry = new IL.Sdk.Telemetry((TelemetryConfiguration)null);
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
            string pathToTestFile = Path.Combine(TestData, "PE", "Native_x64_VS2019_CPlusPlus_DEBUG_DEFAULT.dll");
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Telemetry_ReportElfOrMachoCompilerData_ShouldHonorDisableTelemetry(bool disableTelemetry)
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var assertionScope = new AssertionScope();

            string sarifFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.sarif");
            var telemetryEvents = new List<ITelemetry>();
            var telemetryConfiguration = new TelemetryConfiguration
            {
                ConnectionString = $"InstrumentationKey={Guid.NewGuid()}",
                TelemetryChannel = new StubTelemetryChannel { OnSend = telemetryEvents.Add },
            };
            using var telemetry = new IL.Sdk.Telemetry(telemetryConfiguration);
            string pathToTestFile = Path.Combine(TestData, "Dwarf", "hello-dwarf5-o2");
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[] {
                    pathToTestFile
                },
                OutputFilePath = sarifFile,
                OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                DataToInsert = new[] { OptionallyEmittedData.Hashes },
                DisableTelemetry = disableTelemetry,
            };

            try
            {
                var command = new MultithreadedAnalyzeCommand(telemetry);
                command.Run(options);

                SarifLog.Load(sarifFile).Runs.Should().NotBeEmpty();
                if (disableTelemetry)
                {
                    telemetryEvents.Should().BeEmpty();
                }
                else
                {
                    List<EventTelemetry> events = telemetryEvents.OfType<EventTelemetry>().ToList();
                    events.Should().Contain(e => e.Name == CompilerDataLogger.CompilerEventName);
                    string commandLine = string.Concat(events
                        .Where(e => e.Name == CompilerDataLogger.CommandLineEventName)
                        .Select(e => e.Properties[CompilerDataLogger.ChunkedCommandLine]));
                    commandLine.Should().Contain("-gdwarf-5");
                }
            }
            finally
            {
                File.Delete(sarifFile);
            }
        }
    }
}
