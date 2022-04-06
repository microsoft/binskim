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
using Microsoft.ApplicationInsights.DataContracts;
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
        private const string ExpectedFolder = "Expected";
        private const string TargetUriPath = @"c:\file.dll";
        private const string SarifPath = @"C:\example.sarif";
        private const string SampleSarifPath = "Native_x86_VS2019_SDL_Enabled.exe.sarif";

        [Fact]
        public void CompilerDataLogger_Write_ShouldSendAssemblyReferencesInChunks_WhenTelemetryIsEnabled()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger logger);

            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";

            CompilerDataLogger.s_chunkSize = 10;
            int chunksize = CompilerDataLogger.s_chunkSize;
            int chunkNumber = (assemblies.Length + chunksize - 1) / chunksize;

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });
            sendItems.Count.Should().Be(chunkNumber + 1);
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldSendCommandLineDataInChunks_WhenTelemetryIsEnabled()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger logger);

            string commandLine = "TestCommandLine";

            CompilerDataLogger.s_chunkSize = 10;
            int chunksize = CompilerDataLogger.s_chunkSize;

            int chunkNumber = (commandLine.Length + chunksize - 1) / chunksize;

            CompilerData compilerData = new CompilerData { CompilerName = ".NET Compiler", CommandLine = commandLine };

            logger.Write(context, compilerData);
            sendItems.Count.Should().Be(chunkNumber + 1);
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfNoAssemblyReferences()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger logger);

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = null });
            sendItems.Count.Should().Be(1);
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfTelemetryClientDoesNotExist()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger logger, null, true);

            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";

            CompilerDataLogger.s_chunkSize = 10;
            int chunksize = CompilerDataLogger.s_chunkSize;
            int chunkNumber = (assemblies.Length + chunksize - 1) / chunksize;

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });
            sendItems.Count.Should().Be(0);
            context.Dispose();
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
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldNotThrowException_WhenForceAndCsvAreEnabled()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            using BinaryAnalyzerContext context = CreateTestContext(forceOverwrite: true, targetUriPath: @"C:\temp\");
            using var compilerDataLogger = new CompilerDataLogger(GetExampleSarifPath(), Sarif.SarifVersion.Current, context, fileSystem.Object);
            compilerDataLogger.writer.Should().NotBeNull();
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldThrowException_WhenTelemetryIsEnabledAndSarifDoesNotExist()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            using BinaryAnalyzerContext context = CreateTestContext();

            Assert.Throws<InvalidOperationException>(() => new CompilerDataLogger(sarifOutputFilePath: string.Empty, Sarif.SarifVersion.Current, context, fileSystem.Object));
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_Dispose_ShouldReadSarifV2()
        {
            string sarifLogPath = GetExampleSarifPath();
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            using BinaryAnalyzerContext context = CreateTestContext();

            List<ITelemetry> sendItems = TestSetup(sarifLogPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.Dispose();

            // fileSystem will only be used with SARIF V1.
            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Never);
            StringBuilder sb = ValidateSummaryEvent(csvOutputPath: string.Empty, telemetryGeneratedEvents: sendItems, sarifLogPath: sarifLogPath);
            sb.ToString().Should().BeNullOrEmpty();
        }

        [Fact]
        public void CompilerDataLogger_Dispose_ShouldReadSarifV1()
        {
            string testDirectory = GetTestDirectory("Test.UnitTests.BinSkim.Driver");
            string sarifLogPath = Path.Combine(testDirectory, "Samples", "Native_x86_VS2019_SDL_Enabled_Sarif.v1.0.0.sarif");
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            using BinaryAnalyzerContext context = CreateTestContext();

            fileSystem
                .Setup(f => f.FileOpenRead(It.IsAny<string>()))
                .Returns(new MemoryStream(byteArray));

            List<ITelemetry> sendItems = TestSetup(sarifLogPath, context, Sarif.SarifVersion.OneZeroZero, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.Dispose();

            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Once);
            sendItems.Count.Should().Be(1);
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_Summarize_ShouldLogSummaryEvent()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            var fileSystem = new Mock<IFileSystem>();
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(sarifLogPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            SarifLog sarifLog = SarifLog.Load(sarifLogPath);
            ExecutionException exception = AnalysisSummaryExtractor.ExtractExceptionData(sarifLog).FirstOrDefault();
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifLog, "", null);

            compilerDataLogger.Summarize(summary);

            StringBuilder sb = ValidateSummaryEvent(csvOutputPath: string.Empty, telemetryGeneratedEvents: sendItems, sarifLogPath: sarifLogPath);
            sb.ToString().Should().BeNullOrEmpty();
        }

        [Fact]
        public void CompilerDataLogger_Dispose_ShouldNotLogSummaryIfDisabled()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            using BinaryAnalyzerContext context = CreateTestContext();

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
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_ShouldInitializeFromEnvironmentVariable()
        {
            CompilerDataLogger.s_injectedTelemetryClient = null;
            CompilerDataLogger.s_injectedTelemetryConfiguration = null;
            using BinaryAnalyzerContext context = CreateTestContext();
            var fileSystem = new Mock<IFileSystem>();
            string randomString = Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable("BinskimCompilerDataAppInsightsKey", randomString);
            var sendItems = new List<ITelemetry>();
            context.Policy = new Sarif.PropertiesDictionary();
            var logger = new CompilerDataLogger(SarifPath, Sarif.SarifVersion.Current, context, fileSystem.Object);

            logger.Should().NotBeNull();
            Environment.SetEnvironmentVariable("BinskimCompilerDataAppInsightsKey", null);
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExceptionMessageString()
        {
            var fileSystem = new Mock<IFileSystem>();
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.WriteException(context, "testException");
            sendItems.Count.Should().Be(1);
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldNotLogExceptionMessage_WhenTelemetryClientIsNull()
        {
            var fileSystem = new Mock<IFileSystem>();
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object, true);
            compilerDataLogger.WriteException(context, "testException");
            sendItems.Count.Should().Be(0);
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExecutionExceptionFromBaseLineTestSarif()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            var fileSystem = new Mock<IFileSystem>();
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(sarifLogPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            SarifLog sarifLog = SarifLog.Load(sarifLogPath);
            List<ExecutionException> exceptionList = AnalysisSummaryExtractor.ExtractExceptionData(sarifLog).ToList();
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifLog, "", null);
            LogExceptionEvents(exceptionList, compilerDataLogger, summary);
            StringBuilder sb = ValidateExceptionEvents(csvOutputPath: string.Empty, telemetryGeneratedEvents: sendItems, exceptionList: exceptionList);

            sb.ToString().Should().BeNullOrEmpty();
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExecutionException()
        {
            var fileSystem = new Mock<IFileSystem>();
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            List<ExecutionException> exceptionList = new List<ExecutionException>();
            exceptionList.Add(new ExecutionException(type: "test", message: "testExecutionException", stackTrace: "testStackTrace", innerException: new Exception()));

            LogExceptionEvents(exceptionList, compilerDataLogger);

            StringBuilder sb = ValidateExceptionEvents(string.Empty, sendItems, exceptionList);

            sb.ToString().Should().BeNullOrEmpty();
            context.Dispose();
        }

        [Fact]
        public void CompilerDataLogger_CreateScvOutputFile_ShouldSkipIfPathIsNull()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            var fileSystem = new Mock<IFileSystem>();
            List<ITelemetry> sendItems = TestSetup(SarifPath, context, Sarif.SarifVersion.Current, out CompilerDataLogger compilerDataLogger, fileSystem.Object);
            compilerDataLogger.CreateCsvOutputFile(null, false);
            sendItems.Count.Should().Be(0);
            context.Dispose();
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

        private BinaryAnalyzerContext CreateTestContext(bool forceOverwrite = false, string outputPath = null, string targetUriPath = null)
        {
            string csvOutputPath = outputPath ?? @$"C:\temp\{Guid.NewGuid()}.csv";
            targetUriPath = targetUriPath ?? TargetUriPath;

            var context = new BinaryAnalyzerContext() { TargetUri = new Uri(targetUriPath), ForceOverwrite = forceOverwrite };

            context.Policy.SetProperty(CompilerDataLogger.CsvOutputPath, csvOutputPath);
            return context;
        }

        private void LogExceptionEvents(List<ExecutionException> exceptions, CompilerDataLogger logger, AnalysisSummary summary = null)
        {
            summary = summary ?? new AnalysisSummary();

            foreach (ExecutionException exception in exceptions)
            {
                logger.WriteException(exception, summary);
            }
        }

        private StringBuilder ValidateSummaryEvent(string csvOutputPath,
                                                   List<ITelemetry> telemetryGeneratedEvents,
                                                   string sarifLogPath,
                                                   AnalysisSummary summary = null)
        {
            StringBuilder sb = new StringBuilder();
            SarifLog sarifLog = SarifLog.Load(sarifLogPath);
            summary = summary ?? AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifLog: sarifLog,
                                                                                 serializedFileSpecifiers: string.Empty,
                                                                                 symbolPath: null);

            int expectedSummaryEventCount =
                (!string.IsNullOrEmpty(csvOutputPath) || telemetryGeneratedEvents != null)
                    ? 1
                    : 0;

            if (!string.IsNullOrEmpty(csvOutputPath))
            {
                // test
            }
            if (telemetryGeneratedEvents != null)
            {
                List<EventTelemetry> events = telemetryGeneratedEvents.OfType<EventTelemetry>().ToList();

                List<EventTelemetry> summaryEvents = events.Where(e => e.Name == CompilerDataLogger.SummaryEventName)
                    .OfType<EventTelemetry>().ToList();

                if (summaryEvents.Count != expectedSummaryEventCount)
                {
                    sb.AppendLine(string.Format("Expected {0} summary events, but found {1}",
                                                expectedSummaryEventCount,
                                                summaryEvents.Count));

                    EventTelemetry eventTelemetry = summaryEvents.First();

                    if (eventTelemetry.Properties["toolName"] != summary.ToolName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.ToolName),
                            summary.ToolName,
                            eventTelemetry.Properties["toolName"]));
                    }
                    if (eventTelemetry.Properties["toolVersion"] != summary.ToolVersion)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.ToolVersion),
                            summary.ToolVersion,
                            eventTelemetry.Properties["toolVersion"]));
                    }
                    if (eventTelemetry.Properties["numberOfBinaryAnalyzed"] != summary.FileAnalyzed.ToString())
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.FileAnalyzed),
                            summary.FileAnalyzed,
                            eventTelemetry.Properties["numberOfBinaryAnalyzed"]));
                    }
                    if (eventTelemetry.Properties["analysisStartTime"] != summary.StartTimeUtc.ToString())
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.StartTimeUtc),
                            summary.StartTimeUtc,
                            eventTelemetry.Properties["analysisStartTime"]));
                    }
                    if (eventTelemetry.Properties["analysisEndTime"] != summary.EndTimeUtc.ToString())
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.EndTimeUtc),
                            summary.EndTimeUtc,
                            eventTelemetry.Properties["analysisEndTime"]));
                    }
                    if (eventTelemetry.Properties["timeConsumed"] != summary.TimeConsumed.ToString())
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.TimeConsumed),
                            summary.TimeConsumed,
                            eventTelemetry.Properties["timeConsumed"]));
                    }

                    if (eventTelemetry.Properties["buildDefinitionId"] != summary.BuildDefinitionId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.BuildDefinitionId),
                            summary.BuildDefinitionId,
                            eventTelemetry.Properties["buildDefinitionId"]));
                    }
                    if (eventTelemetry.Properties["buildDefinitionName"] != summary.BuildDefinitionName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.BuildDefinitionName),
                            summary.BuildDefinitionName,
                            eventTelemetry.Properties["buildDefinitionName"]));
                    }
                    if (eventTelemetry.Properties["buildRunId"] != summary.BuildRunId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.BuildRunId),
                            summary.BuildRunId,
                            eventTelemetry.Properties["buildRunId"]));
                    }
                    if (eventTelemetry.Properties["projectName"] != summary.ProjectName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.ProjectName),
                            summary.ProjectName,
                            eventTelemetry.Properties["projectName"]));
                    }
                    if (eventTelemetry.Properties["organizationId"] != summary.OrganizationId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.OrganizationId),
                            summary.OrganizationId,
                            eventTelemetry.Properties["organizationId"]));
                    }
                    if (eventTelemetry.Properties["organizationName"] != summary.OrganizationName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.OrganizationName),
                            summary.OrganizationName,
                            eventTelemetry.Properties["organizationName"]));
                    }
                    if (eventTelemetry.Properties["projectId"] != summary.ProjectId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.ProjectId),
                            summary.ProjectId,
                            eventTelemetry.Properties["projectId"]));
                    }
                    if (eventTelemetry.Properties["repositoryName"] != summary.RepositoryName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.RepositoryName),
                            summary.RepositoryName,
                            eventTelemetry.Properties["repositoryName"]));
                    }
                    if (eventTelemetry.Properties["repositoryId"] != summary.RepositoryId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.RepositoryId),
                            summary.RepositoryId,
                            eventTelemetry.Properties["repositoryId"]));
                    }
                }
            }

            return sb;
        }

        private StringBuilder ValidateExceptionEvents(string csvOutputPath,
                                                      List<ITelemetry> telemetryGeneratedEvents,
                                                      List<ExecutionException> exceptionList)
        {
            StringBuilder sb = new StringBuilder();

            int expectedExceptionEventCount = exceptionList?.Count ?? 0;

            if (!string.IsNullOrEmpty(csvOutputPath))
            {
                // test
            }
            if (telemetryGeneratedEvents != null)
            {
                List<ExceptionTelemetry> events = telemetryGeneratedEvents.OfType<ExceptionTelemetry>().ToList();

                if (events.Count != expectedExceptionEventCount)
                {
                    sb.AppendLine(string.Format("Expected {0} event(s), but found {1}",
                                                expectedExceptionEventCount,
                                                events.Count));
                }

                foreach (ExceptionTelemetry ev in events)
                {
                    ExecutionException exceptionMatch = exceptionList.Where(ex => ex == ev.Exception).FirstOrDefault();
                    if (exceptionMatch == null)
                    {
                        sb.Append(string.Format(
                            "Expected an exception telemetry event with 'Message': {0} but no match found.",
                            ev.Message));
                    }
                }
            }

            return sb;
        }

        public static string GetExampleSarifPath()
        {
            return Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
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
