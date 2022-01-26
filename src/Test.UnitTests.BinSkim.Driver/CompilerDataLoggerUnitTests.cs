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
            var context = new BinaryAnalyzerContext() { TargetUri = new Uri("file.dll") };
            List<ITelemetry> sendItems = TestSetup(context, out CompilerDataLogger logger);

            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";

            CompilerDataLogger.s_chunkSize = 10;
            int chunksize = CompilerDataLogger.s_chunkSize;
            int chunkNumber = (assemblies.Length + chunksize - 1) / chunksize;

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });
            sendItems.Count.Should().Be(chunkNumber + 1);
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfNoAssemblyReferences()
        {
            var context = new BinaryAnalyzerContext() { TargetUri = new Uri("file.dll") };
            List<ITelemetry> sendItems = TestSetup(context: null, out CompilerDataLogger logger);

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = null });
            sendItems.Count.Should().Be(1);
        }    
    /*
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
            List<ITelemetry> sendItems = TestSetup(context: null, out CompilerDataLogger logger);

            logger.Write(new CompilerData { CompilerName = ".NET Compiler" });

            ValidateTelemetry(sendItems, shouldExist: false);

            context.Hashes = new Sarif.HashData(null, null, "some-content");
            logger.Write(new CompilerData { CompilerName = ".NET Compiler" });

            ValidateTelemetry(setup.Item3, shouldExist: true);
        }
    */
        private List<ITelemetry> TestSetup(BinaryAnalyzerContext context, out CompilerDataLogger logger)
        {
            List<ITelemetry> sendItems = null;
            TelemetryClient telemetryClient;
            TelemetryConfiguration telemetryConfiguration;

            sendItems = new List<ITelemetry>();

            telemetryConfiguration = new TelemetryConfiguration();
            telemetryConfiguration.InstrumentationKey = Guid.NewGuid().ToString();
            telemetryConfiguration.TelemetryChannel = new StubTelemetryChannel { OnSend = item => sendItems.Add(item) };
            telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());

            telemetryClient = new TelemetryClient(telemetryConfiguration);

            CompilerDataLogger.s_injectedTelemetryClient = telemetryClient;
            CompilerDataLogger.s_injectedTelemetryConfiguration = telemetryConfiguration;

            logger = new CompilerDataLogger(context ?? new BinaryAnalyzerContext());

            return sendItems;
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

