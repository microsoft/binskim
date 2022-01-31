// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using FluentAssertions;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

using Moq;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class CompilerDataLoggerTests
    {
        private const string SarifPath = @"C:\example.sarif";

        [Fact]
        public void CompilerDataLogger_Write_ShouldSendAssemblyReferencesInChunks_WhenTelemetryIsEnabled()
        {
            var context = new BinaryAnalyzerContext() { TargetUri = new Uri(@"c:\file.dll") };
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, out CompilerDataLogger logger);

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
            var context = new BinaryAnalyzerContext() { TargetUri = new Uri(@"c:\file.dll") };
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, out CompilerDataLogger logger);

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = null });
            sendItems.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldThrowException_WhenForceIsDisabledAndCsvIsEnabled()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

            var context = new BinaryAnalyzerContext() { TargetUri = new Uri(@"c:\file.dll") };
            var compilerOptions = new PropertiesDictionary
            {
                { "CsvOutputPath", @"C:\temp\" }
            };

            context.Policy = new PropertiesDictionary
            {
                { "CompilerTelemetry.Options", compilerOptions }
            };

            Assert.Throws<InvalidOperationException>(() => new CompilerDataLogger(SarifPath, context, fileSystem.Object));
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldNotThrowException_WhenForceAndCsvAreEnabled()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

            var context = new BinaryAnalyzerContext() { TargetUri = new Uri(@"c:\file.dll"), ForceOverwrite = true };
            var compilerOptions = new PropertiesDictionary
            {
                { "CsvOutputPath", @$"C:\temp\{Guid.NewGuid()}.sarif" }
            };

            context.Policy = new PropertiesDictionary
            {
                { "CompilerTelemetry.Options", compilerOptions }
            };

            var compilerDataLogger = new CompilerDataLogger(SarifPath, context, fileSystem.Object);
            compilerDataLogger.writer.Should().NotBeNull();
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldThrowException_WhenTelemetryIsEnabledAndSarifDoesNotExist()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

            var context = new BinaryAnalyzerContext() { TargetUri = new Uri(@"c:\file.dll"), ForceOverwrite = true };
            var compilerOptions = new PropertiesDictionary
            {
                { "CsvOutputPath", @$"C:\temp\{Guid.NewGuid()}.sarif" }
            };

            context.Policy = new PropertiesDictionary
            {
                { "CompilerTelemetry.Options", compilerOptions }
            };

            Assert.Throws<InvalidOperationException>(() => new CompilerDataLogger(sarifOutputFilePath: string.Empty, context, fileSystem.Object));
        }

        private List<ITelemetry> TestSetup(string sarifLogFilePath, BinaryAnalyzerContext context, out CompilerDataLogger logger)
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

            context.Policy = new Sarif.PropertiesDictionary();
            logger = new CompilerDataLogger(sarifLogFilePath, context ?? new BinaryAnalyzerContext());

            return sendItems;
        }
    }
}

