// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.IL.Sdk;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class CompilerDataLoggerUnitTests
    {
        [Fact]
        public void CompilerDataLogger_Write_ShouldSendAssemblyReferencesInChunks_WhenTelemetryIsEnabled()
        {
            (TelemetryConfiguration, TelemetryClient, List<ITelemetry>) setup = TestSetup();

            var context = new BinaryAnalyzerContext();
            string[] targetFileSpecifier = new[] { @"E:\applications\Tool\*.exe" };
            int chunksize = 10;
            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";
            int chunkNumber = (assemblies.Length + chunksize - 1) / chunksize;

            var logger = new CompilerDataLogger(context, targetFileSpecifier, setup.Item1, setup.Item2, chunksize);
            logger.Write(new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });

            setup.Item3.Count.Should().Be(chunkNumber + 1); // first item should be CompilerInformation
            CompilerDataLogger.Reset();
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfNoAssemblyReferences()
        {
            (TelemetryConfiguration, TelemetryClient, List<ITelemetry>) setup = TestSetup();

            var context = new BinaryAnalyzerContext();
            string[] targetFileSpecifier = new[] { @"E:\applications\Tool\*.exe" };
            int chunksize = 10;
            string assemblies = null;

            var logger = new CompilerDataLogger(context, targetFileSpecifier, setup.Item1, setup.Item2, chunksize);
            logger.Write(new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });

            setup.Item3.Count.Should().Be(1); // first item should be CompilerInformation
            CompilerDataLogger.Reset();
        }

        [Fact]
        public void CompilerDataLogger_ShouldGenerateOnlyOneSessionPerRun()
        {
            string sessionId = string.Empty;
            int numberOfCompilerDataLoggers = 1000;
            var context = new BinaryAnalyzerContext();
            string[] targetFileSpecifier = new[] { @"E:\applications\Tool\*.exe" };

            Environment.SetEnvironmentVariable("BinskimAppInsightsKey", Guid.NewGuid().ToString());

            Parallel.For(0, numberOfCompilerDataLoggers, _ =>
            {
                var compilerDataLogger = new CompilerDataLogger(context, targetFileSpecifier);
                if (string.IsNullOrEmpty(sessionId))
                {
                    sessionId = CompilerDataLogger.s_sessionId;
                }

                sessionId.Should().Be(CompilerDataLogger.s_sessionId);
            });

            CompilerDataLogger.Reset();
        }

        [Fact]
        public void CompilerDataLogger_SessionIdShouldNotBeReplacedWhenValid()
        {
            var context = new BinaryAnalyzerContext();
            string[] targetFileSpecifier = new[] { @"E:\applications\Tool\*.exe" };

            _ = new CompilerDataLogger(context, targetFileSpecifier);
            string previousSession = CompilerDataLogger.s_sessionId;
            CompilerDataLogger.s_sessionId.Should().NotBeNullOrWhiteSpace();

            string currentGuid = Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable("BinskimAppInsightsKey", currentGuid);

            // SessionId will be created when BinskimAppInsightsKey is retrieved.
            _ = new CompilerDataLogger(context, targetFileSpecifier);
            string currentSession = CompilerDataLogger.s_sessionId;
            CompilerDataLogger.s_sessionId.Should().NotBe(previousSession);

            // Since sessionId is not null, no new session should be created.
            _ = new CompilerDataLogger(context, targetFileSpecifier);
            CompilerDataLogger.s_sessionId.Should().Be(currentSession);
            CompilerDataLogger.s_sessionId.Should().NotBe(previousSession);

            CompilerDataLogger.Reset();
        }

        [Fact]
        public void CompilerDataLogger_ShouldLogHashIfValid()
        {
            (TelemetryConfiguration, TelemetryClient, List<ITelemetry>) setup = TestSetup();

            var context = new BinaryAnalyzerContext();
            string[] targetFileSpecifier = new[] { @"E:\applications\Tool\*.exe" };

            var logger = new CompilerDataLogger(context, targetFileSpecifier, setup.Item1, setup.Item2);
            logger.Write(new CompilerData { CompilerName = ".NET Compiler" });

            ValidateTelemetry(setup.Item3, shouldExist: false);
            context.Hashes = new Sarif.HashData(null, null, "some-content");
            logger.Write(new CompilerData { CompilerName = ".NET Compiler" });

            ValidateTelemetry(setup.Item3, shouldExist: true);

            CompilerDataLogger.Reset();
        }

        private (TelemetryConfiguration, TelemetryClient, List<ITelemetry>) TestSetup()
        {
            List<ITelemetry> sendItems = null;
            TelemetryClient telemetryClient = null;
            TelemetryConfiguration telemetryConfiguration = null;

            if (telemetryConfiguration == null && telemetryClient == null)
            {
                telemetryConfiguration = new TelemetryConfiguration();
                sendItems = new List<ITelemetry>();
                telemetryConfiguration.TelemetryChannel = new StubTelemetryChannel { OnSend = item => sendItems.Add(item) };
                telemetryConfiguration.InstrumentationKey = Guid.NewGuid().ToString();
                telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
                telemetryClient = new TelemetryClient(telemetryConfiguration);
            }

            return (telemetryConfiguration, telemetryClient, sendItems);
        }

        private void ValidateTelemetry(List<ITelemetry> telemetries, bool shouldExist)
        {
            foreach (EventTelemetry telemetry in telemetries)
            {
                if (telemetry.Name == "CompilerInformation")
                {
                    if (telemetry.Properties.TryGetValue("hash", out string hash))
                    {
                        if (shouldExist)
                        {
                            hash.Should().NotBeNullOrWhiteSpace();
                        }
                        else
                        {
                            hash.Should().BeNullOrWhiteSpace();
                        }
                    }
                }
            }
            telemetries.Clear();
        }
    }
}
