// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using FluentAssertions;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

using Moq;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class CompilerDataLoggerTests
    {
        private const string SarifPath = @"C:\example.sarif";
        private const string SampleSarifPath = "Native_x86_VS2019_SDL_Enabled.exe.sarif";
        private const string ExpectedFolder = "Expected";
        private const string TargetUriPath = @"c:\file.dll";

        [Fact]
        public void CompilerDataLogger_Write_ShouldSendAssemblyReferencesInChunks_WhenTelemetryIsEnabled()
        {
            BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger logger);

            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";

            CompilerDataLogger.s_chunkSize = 10;
            int chunksize = CompilerDataLogger.s_chunkSize;
            int chunkNumber = (assemblies.Length + chunksize - 1) / chunksize;

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });
            sendItems.Count.Should().Be(chunkNumber + 1);
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldSendCommandLineDataInChunks_WhenTelemetryIsEnabled()
        {
            BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger logger);

            string commandLine = "TestCommandLine";

            CompilerDataLogger.s_chunkSize = 10;
            int chunksize = CompilerDataLogger.s_chunkSize;

            int chunkNumber = (commandLine.Length + chunksize - 1) / chunksize;

            CompilerData compilerData = new CompilerData { CompilerName = ".NET Compiler", CommandLine = commandLine };

            logger.Write(context, compilerData);
            sendItems.Count.Should().Be(chunkNumber + 1);
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfNoAssemblyReferences()
        {
            BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger logger);

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = null });
            sendItems.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfTelemetryClientDoesNotExist()
        {
            BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger logger, null, true);

            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";

            CompilerDataLogger.s_chunkSize = 10;
            int chunksize = CompilerDataLogger.s_chunkSize;
            int chunkNumber = (assemblies.Length + chunksize - 1) / chunksize;

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });
            sendItems.Count.Should().Be(0);
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldThrowException_WhenForceIsDisabledAndCsvIsEnabled()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

            var context = new BinaryAnalyzerContext() { TargetUri = new Uri(TargetUriPath) };
            var compilerOptions = new PropertiesDictionary
            {
                { "CsvOutputPath", @"C:\temp\" }
            };

            context.Policy = new PropertiesDictionary
            {
                { "CompilerTelemetry.Options", compilerOptions }
            };

            Assert.Throws<InvalidOperationException>(() => new CompilerDataLogger(SarifPath, Sarif.SarifVersion.Current, context, fileSystem.Object));
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldNotLogEvents_WhenForceAndCsvAreEnabled()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            BinaryAnalyzerContext context = CreateTestContext();
            var compilerDataLogger = new CompilerDataLogger(SarifPath, Sarif.SarifVersion.Current, context, fileSystem.Object);
            compilerDataLogger.writer.Should().NotBeNull();
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldThrowException_WhenTelemetryIsEnabledAndSarifDoesNotExist()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            BinaryAnalyzerContext context = CreateTestContext();

            Assert.Throws<InvalidOperationException>(() => new CompilerDataLogger(sarifOutputFilePath: string.Empty, Sarif.SarifVersion.Current, context, fileSystem.Object));
        }

        [Fact]
        public void CompilerDataLogger_Dispose_ShouldReadSarifV2()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            BinaryAnalyzerContext context = CreateTestContext();

            List<ITelemetry> sendItems = TestSetup(sarifLogPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.Dispose();

            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Never);
            sendItems.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_Dispose_ShouldReadSarifV1()
        {
            string testDirectory = GetTestDirectory("Test.UnitTests.BinSkim.Driver");
            string sarifLogPath = Path.Combine(testDirectory, "Samples", "Native_x86_VS2019_SDL_Enabled_Sarif.v1.0.0.sarif");
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            BinaryAnalyzerContext context = CreateTestContext();

            fileSystem
                .Setup(f => f.FileOpenRead(It.IsAny<string>()))
                .Returns(new MemoryStream(byteArray));

            List<ITelemetry> sendItems = TestSetup(sarifLogPath, context, Sarif.SarifVersion.OneZeroZero, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.Dispose();

            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Once);
            sendItems.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_Summarize_ShouldLogSummaryEvent()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            var fileSystem = new Mock<IFileSystem>();
            BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(sarifLogPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            SarifLog sarifLog = SarifLog.Load(sarifLogPath);
            ExecutionException exception = AnalysisSummaryExtractor.ExtractExceptionData(sarifLog).FirstOrDefault();
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifLog, "", null);

            compilerDataLogger.Summarize(summary);

            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Never);
            sendItems.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_Dispose_ShouldNotLogSummaryIfDisabled()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            BinaryAnalyzerContext context = CreateTestContext();

            List<ITelemetry> sendItems = TestSetup(sarifLogPath,
                                                   context,
                                                   Sarif.SarifVersion.Current,
                                                   out CompilerDataLogger compilerDataLogger,
                                                   fileSystem.Object,
                                                   true);

            // Intentionally disable the logger by removing the TelemetryClient and Writer.
            CompilerDataLogger.s_injectedTelemetryClient = null;
            compilerDataLogger.writer = null;
            compilerDataLogger.Dispose();

            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Never);
            sendItems.Count.Should().Be(0);
        }

        [Fact]
        public void CompilerDataLogger_ShouldInitializeFromEnvironmentVariable()
        {
            CompilerDataLogger.s_injectedTelemetryClient = null;
            CompilerDataLogger.s_injectedTelemetryConfiguration = null;
            BinaryAnalyzerContext context = CreateTestContext();
            var fileSystem = new Mock<IFileSystem>();
            string randomString = Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable("BinskimCompilerDataAppInsightsKey", randomString);
            var sendItems = new List<ITelemetry>();
            context.Policy = new Sarif.PropertiesDictionary();
            var logger = new CompilerDataLogger(SarifPath, Sarif.SarifVersion.Current, context, fileSystem.Object);

            logger.Should().NotBeNull();
            Environment.SetEnvironmentVariable("BinskimCompilerDataAppInsightsKey", null);
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExceptionMessageString()
        {
            var fileSystem = new Mock<IFileSystem>();
            BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.WriteException(context, "testException");
            sendItems.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldNotLogExceptionMessage_WhenTelemetryClientIsNull()
        {
            var fileSystem = new Mock<IFileSystem>();
            BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object, true);
            compilerDataLogger.WriteException(context, "testException");
            sendItems.Count.Should().Be(0);
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExecutionExceptionFromBaseLineTestSarif()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            var fileSystem = new Mock<IFileSystem>();
            BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(sarifLogPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            SarifLog sarifLog = SarifLog.Load(sarifLogPath);
            ExecutionException exception = AnalysisSummaryExtractor.ExtractExceptionData(sarifLog).FirstOrDefault();
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifLog, "", null);
            compilerDataLogger.WriteException(exception, summary);
            sendItems.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExecutionException()
        {
            var fileSystem = new Mock<IFileSystem>();
            BinaryAnalyzerContext context = CreateTestContext();
            ExecutionException exception = new ExecutionException(type: "test", message: "testExecutionException", stackTrace: "testStackTrace", innerException: new Exception());
            AnalysisSummary summary = new AnalysisSummary();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.WriteException(exception, summary);
            sendItems.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_CreateScvOutputFile_ShouldSkipIfPathIsNull()
        {
            BinaryAnalyzerContext context = CreateTestContext();
            var fileSystem = new Mock<IFileSystem>();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.CreateCsvOutputFile(null, false);
            sendItems.Count.Should().Be(0);
        }

        private List<ITelemetry> TestSetup(string sarifLogFilePath, BinaryAnalyzerContext context, Sarif.SarifVersion sarifVersion, out CompilerDataLogger logger, IFileSystem fileSystem = null, bool isDisabledLogger = false)
        {
            List<ITelemetry> sendItems = null;
            sendItems = new List<ITelemetry>();

            if (isDisabledLogger)
            {
                logger = new CompilerDataLogger(sarifLogFilePath, sarifVersion, context ?? new BinaryAnalyzerContext(), fileSystem);
            }
            else
            {
                TelemetryClient telemetryClient;
                TelemetryConfiguration telemetryConfiguration;
                telemetryConfiguration = new TelemetryConfiguration();
                telemetryConfiguration.InstrumentationKey = Guid.NewGuid().ToString();
                telemetryConfiguration.TelemetryChannel = new StubTelemetryChannel { OnSend = item => sendItems.Add(item) };
                telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());

                telemetryClient = new TelemetryClient(telemetryConfiguration);

                CompilerDataLogger.s_injectedTelemetryClient = telemetryClient;
                CompilerDataLogger.s_injectedTelemetryConfiguration = telemetryConfiguration;

                context.Policy = new Sarif.PropertiesDictionary();
                logger = new CompilerDataLogger(sarifLogFilePath, sarifVersion, context ?? new BinaryAnalyzerContext(), fileSystem);
            }
            
            return sendItems;
        }

        private BinaryAnalyzerContext CreateTestContext(string outputPath = null)
        {
            string csvOutputPath = outputPath ?? @$"C:\temp\{Guid.NewGuid()}.sarif";
            var context = new BinaryAnalyzerContext() { TargetUri = new Uri(TargetUriPath), ForceOverwrite = true };
            var compilerOptions = new PropertiesDictionary
            {
                { "CsvOutputPath", csvOutputPath }
            };

            context.Policy = new PropertiesDictionary
            {
                { "CompilerTelemetry.Options", compilerOptions }
            };

            return context;
        }

        internal static string GetTestDirectory(string relativeDirectory)
        {
            var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            string codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
            string dirPath = Path.GetDirectoryName(codeBasePath);
            dirPath = Path.Combine(dirPath, string.Format(@"..{0}..{0}..{0}..{0}src{0}", Path.DirectorySeparatorChar));
            dirPath = Path.GetFullPath(dirPath);
            return Path.Combine(dirPath, relativeDirectory);
        }
    }
}

