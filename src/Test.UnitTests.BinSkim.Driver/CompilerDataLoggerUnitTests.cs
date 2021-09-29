// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using FluentAssertions;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.IL.Sdk;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class CompilerDataLoggerUnitTests
    {
        private TelemetryConfiguration telemetryConfiguration;
        private TelemetryClient telemetryClient;
        private List<ITelemetry> sendItems;

        private void TestSetup()
        {
            if (this.telemetryConfiguration == null && this.telemetryClient == null)
            {
                this.telemetryConfiguration = new TelemetryConfiguration();
                this.sendItems = new List<ITelemetry>();
                this.telemetryConfiguration.TelemetryChannel = new StubTelemetryChannel { OnSend = item => this.sendItems.Add(item) };
                this.telemetryConfiguration.InstrumentationKey = Guid.NewGuid().ToString();
                this.telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
                this.telemetryClient = new TelemetryClient(this.telemetryConfiguration);
            }
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldSendAssemblyReferencesInChunks()
        {
            this.TestSetup();

            BinaryAnalyzerContext context = new BinaryAnalyzerContext { };
            string[] targetFileSpecifier = new[] { @"E:\applications\Tool\*.exe" };
            int chunksize = 10;
            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";
            int chunkNumber = (assemblies.Length + chunksize - 1) / chunksize;

            CompilerDataLogger logger = new CompilerDataLogger(context, targetFileSpecifier, this.telemetryConfiguration, this.telemetryClient, chunksize);
            logger.Write(new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });

            this.sendItems.Count.Should().Be(chunkNumber + 1); // first item should be CompilerInformation
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfNoAssemblyReferences()
        {
            this.TestSetup();

            BinaryAnalyzerContext context = new BinaryAnalyzerContext { };
            string[] targetFileSpecifier = new[] { @"E:\applications\Tool\*.exe" };
            int chunksize = 10;
            string assemblies = null;

            CompilerDataLogger logger = new CompilerDataLogger(context, targetFileSpecifier, this.telemetryConfiguration, this.telemetryClient, chunksize);
            logger.Write(new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });

            this.sendItems.Count.Should().Be(1); // first item should be CompilerInformation
        }
    }
}
