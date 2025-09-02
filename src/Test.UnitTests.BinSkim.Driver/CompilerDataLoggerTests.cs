// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using FluentAssertions;

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
        private const string SampleSarifPath = "Native_x86_VS2019_SDL_Enabled.exe.sarif";
        private const int SmallChunkSize = 10;
        private const string TestExceptionMessage = "TestException";

        [Fact]
        public void CompilerDataLogger_Write_ShouldSendAssemblyReferencesInChunks_WhenTelemetryIsEnabled()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger logger);

            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";
            CompilerDataLogger.s_chunkSize = SmallChunkSize;
            int expectedChunkCount = logger.CalculateChunkedContentSize(assemblies.Length);
            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });

            StringBuilder sb = ValidateChunkedEvents(
                expectedEventName: CompilerDataLogger.AssemblyReferencesEventName,
                telemetryGeneratedEvents: telemetryEventOutput,
                expectedChunkedContent: assemblies,
                chunkCount: expectedChunkCount);

            Assert.Equal(string.Empty, sb.ToString());
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldSendCommandLineDataInChunks_WhenTelemetryIsEnabled()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger logger);

            string commandLine = "TestCommandLine";
            CompilerDataLogger.s_chunkSize = SmallChunkSize;
            int chunkNumber = logger.CalculateChunkedContentSize(commandLine.Length);
            CompilerData compilerData = new CompilerData { CompilerName = ".NET Compiler", CommandLine = commandLine };
            logger.Write(context, compilerData);

            StringBuilder sb = ValidateChunkedEvents(CompilerDataLogger.CommandLineEventName, telemetryEventOutput, commandLine, chunkNumber);

            Assert.Equal(string.Empty, sb.ToString());
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfNoAssemblyReferences()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger logger);

            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = null });
            List<EventTelemetry> events = telemetryEventOutput.OfType<EventTelemetry>().ToList();
            List<EventTelemetry> assemblyEvents = events.Where(e => e.Name == CompilerDataLogger.AssemblyReferencesEventName)
                    .OfType<EventTelemetry>().ToList();

            assemblyEvents.Count.Should().Be(0);
        }

        [Fact]
        public void CompilerDataLogger_Write_ShouldNotSend_IfTelemetryClientDoesNotExist()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger logger,
                                                              fileSystem: null,
                                                              isTelemetryOutputEnabled: false,
                                                              isCsvOutputEnabled: false);

            string assemblies = "Microsoft.DiaSymReader (1.3.0);Newtonsoft.Json (13.0.1)";
            CompilerDataLogger.s_chunkSize = 10;
            int chunksize = CompilerDataLogger.s_chunkSize;
            int chunkNumber = (assemblies.Length + chunksize - 1) / chunksize;
            logger.Write(context, new CompilerData { CompilerName = ".NET Compiler", AssemblyReferences = assemblies });
            telemetryEventOutput.Count.Should().Be(0);
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldThrowException_WhenForceIsDisabledAndCsvIsEnabled()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var context = new BinaryAnalyzerContext()
            {
                CurrentTarget = new EnumeratedArtifact(fileSystem.Object)
                {
                    Uri = new Uri(TargetUriPath)
                }
            };
            var compilerOptions = new PropertiesDictionary
            {
                { "CsvOutputPath", @"C:\temp\" }
            };

            context.Policy = new PropertiesDictionary
            {
                { "CompilerTelemetry.Options", compilerOptions }
            };

            Assert.Throws<InvalidOperationException>(() => new CompilerDataLogger(GetExampleSarifPath(Sarif.SarifVersion.Current),
                                                                                  Sarif.SarifVersion.Current,
                                                                                  context,
                                                                                  fileSystem.Object));
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldNotThrowException_WhenForceAndCsvAreEnabled()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            using BinaryAnalyzerContext context = CreateTestContext(forceOverwrite: true, targetUriPath: @"C:\temp\");
            using var compilerDataLogger = new CompilerDataLogger(GetExampleSarifPath(Sarif.SarifVersion.Current),
                                                                  Sarif.SarifVersion.Current,
                                                                  context,
                                                                  fileSystem.Object);

            compilerDataLogger.writer.Should().NotBeNull();
        }

        [Fact]
        public void CompilerDataLogger_Constructor_ShouldThrowException_WhenTelemetryIsEnabledAndSarifDoesNotExist()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            using BinaryAnalyzerContext context = CreateTestContext();

            Assert.Throws<InvalidOperationException>(() => new CompilerDataLogger(sarifOutputFilePath: string.Empty,
                                                                                  Sarif.SarifVersion.Current,
                                                                                  context,
                                                                                  fileSystem.Object));
        }

        [Fact]
        public void CompilerDataLogger_Dispose_ShouldReadSarifV2()
        {
            string sarifLogPath = GetExampleSarifPath(Sarif.SarifVersion.Current);
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            using BinaryAnalyzerContext context = CreateTestContext();

            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger compilerDataLogger,
                                                              fileSystem: fileSystem.Object);
            compilerDataLogger.Dispose();

            // fileSystem will only be used with SARIF V1.
            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Never);
            StringBuilder sb = ValidateSummaryEvent(csvOutputPath: string.Empty,
                                                    telemetryGeneratedEvents: telemetryEventOutput,
                                                    sarifLogPath: sarifLogPath);

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

            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.OneZeroZero,
                                                              logger: out CompilerDataLogger compilerDataLogger,
                                                              fileSystem: fileSystem.Object);
            compilerDataLogger.Dispose();

            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Once);
            telemetryEventOutput.Count.Should().Be(1);
        }

        [Fact]
        public void CompilerDataLogger_Summarize_ShouldLogSummaryEvent()
        {
            string sarifLogPath = GetExampleSarifPath(Sarif.SarifVersion.Current);
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger compilerDataLogger);

            SarifLog sarifLog = SarifLog.Load(sarifLogPath);
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifLog, string.Empty, null);

            compilerDataLogger.Summarize(summary);

            StringBuilder sb = ValidateSummaryEvent(csvOutputPath: string.Empty,
                                                    telemetryGeneratedEvents: telemetryEventOutput,
                                                    sarifLogPath: sarifLogPath);
            sb.ToString().Should().BeNullOrEmpty();
        }

        [Fact]
        public void CompilerDataLogger_Dispose_ShouldNotLogSummaryIfDisabled()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            var fileSystem = new Mock<IFileSystem>();
            using BinaryAnalyzerContext context = CreateTestContext();

            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger compilerDataLogger,
                                                              fileSystem: fileSystem.Object,
                                                              isTelemetryOutputEnabled: false,
                                                              isCsvOutputEnabled: false);

            // Intentionally disable the logger by removing the Writer.
            compilerDataLogger.writer?.Dispose();
            compilerDataLogger.writer = null;
            compilerDataLogger.Dispose();

            fileSystem.Verify(fileSystem => fileSystem.FileOpenRead(sarifLogPath), Times.Never);
            telemetryEventOutput.Count.Should().Be(0);
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExceptionMessageString()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger compilerDataLogger);

            compilerDataLogger.WriteException(context: context, errorMessage: TestExceptionMessage);
            telemetryEventOutput.Count.Should().Be(1);

            foreach (EventTelemetry telemetryEvent in telemetryEventOutput)
            {
                telemetryEvent.Name.Should().Be(CompilerDataLogger.CompilerEventName);
                telemetryEvent.Properties["error"].Should().Be(TestExceptionMessage);
            }
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldNotLogExceptionMessage_WhenTelemetryClientIsNull()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger compilerDataLogger,
                                                              isTelemetryOutputEnabled: false,
                                                              isCsvOutputEnabled: false);

            compilerDataLogger.WriteException(context: context, errorMessage: TestExceptionMessage);
            telemetryEventOutput.Count.Should().Be(0);
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExecutionExceptions()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath);
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger compilerDataLogger);

            SarifLog sarifLog = SarifLog.Load(sarifLogPath);
            List<ExecutionException> exceptionList = AnalysisSummaryExtractor.ExtractExceptionData(sarifLog).ToList();
            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifLog, string.Empty, null);
            LogExceptionEvents(exceptionList, compilerDataLogger, summary);
            StringBuilder sb = ValidateExceptionEvents(csvOutputPath: string.Empty,
                                                       telemetryGeneratedEvents: telemetryEventOutput,
                                                       exceptionList: exceptionList);


            sb.ToString().Should().BeNullOrEmpty();
        }

        [Fact]
        public void CompilerDataLogger_WriteException_ShouldLogExecutionException()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger compilerDataLogger);

            List<ExecutionException> exceptionList = new List<ExecutionException>();
            exceptionList.Add(new ExecutionException(type: "test",
                                                     message: "testExecutionException",
                                                     stackTrace: "testStackTrace",
                                                     innerException: new Exception()));

            LogExceptionEvents(exceptions: exceptionList, logger: compilerDataLogger);

            StringBuilder sb = ValidateExceptionEvents(csvOutputPath: string.Empty,
                                                       telemetryGeneratedEvents: telemetryEventOutput,
                                                       exceptionList: exceptionList);

            sb.ToString().Should().BeNullOrEmpty();
        }

        [Fact]
        public void CompilerDataLogger_CreateCsvOutputFile_ShouldNotThrowExceptionWhenPathIsNull()
        {
            using BinaryAnalyzerContext context = CreateTestContext();
            List<ITelemetry> telemetryEventOutput = TestSetup(context: context,
                                                              sarifVersion: Sarif.SarifVersion.Current,
                                                              logger: out CompilerDataLogger compilerDataLogger);

            compilerDataLogger.CreateCsvOutputFile(csvFilePath: null, overwriteExistingCsv: false);
        }

        private List<ITelemetry> TestSetup(BinaryAnalyzerContext context,
                                           Sarif.SarifVersion sarifVersion,
                                           out CompilerDataLogger logger,
                                           IFileSystem fileSystem = null,
                                           bool isTelemetryOutputEnabled = true,
                                           bool isCsvOutputEnabled = false)
        {
            var telemetryEventOutput = new List<ITelemetry>();
            string sarifLogFilePath = GetExampleSarifPath(sarifVersion);
            fileSystem = fileSystem ?? new Mock<IFileSystem>().Object;

            IL.Sdk.Telemetry telemetry = null;

            if (isTelemetryOutputEnabled || isCsvOutputEnabled)
            {
                TelemetryConfiguration telemetryConfiguration = new TelemetryConfiguration
                {
                    ConnectionString = $"InstrumentationKey={Guid.NewGuid()}",
                    TelemetryChannel = new StubTelemetryChannel { OnSend = item => telemetryEventOutput.Add(item) }
                };

                telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
                telemetry = new IL.Sdk.Telemetry(telemetryConfiguration);

                context.Policy = new Sarif.PropertiesDictionary();
            }

            logger = new CompilerDataLogger(sarifOutputFilePath: sarifLogFilePath,
                                            sarifVersion: sarifVersion,
                                            context: context ?? new BinaryAnalyzerContext(),
                                            fileSystem: fileSystem,
                                            telemetry: telemetry);

            return telemetryEventOutput;
        }

        private BinaryAnalyzerContext CreateTestContext(bool forceOverwrite = false, string outputPath = null, string targetUriPath = null)
        {
            string csvOutputPath = outputPath ?? @$"C:\temp\{Guid.NewGuid()}.csv";
            targetUriPath = targetUriPath ?? TargetUriPath;

            var context = new BinaryAnalyzerContext()
            {
                CurrentTarget = new EnumeratedArtifact(FileSystem.Instance)
                {
                    Uri = new Uri(TargetUriPath)
                },
                OutputFileOptions = forceOverwrite ? FilePersistenceOptions.ForceOverwrite : 0,
            };

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
                // TODO: https://github.com/microsoft/binskim/issues/626
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
                }
                else
                {
                    EventTelemetry eventTelemetry = summaryEvents.First();

                    if (eventTelemetry.Properties[CompilerDataLogger.ToolName] != summary.ToolName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.ToolName),
                            summary.ToolName,
                            eventTelemetry.Properties[CompilerDataLogger.ToolName]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.ToolVersion] != summary.ToolVersion)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.ToolVersion),
                            summary.ToolVersion,
                            eventTelemetry.Properties[CompilerDataLogger.ToolVersion]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.NumberOfBinaryAnalyzed] != summary.FileAnalyzed.ToString())
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.FileAnalyzed),
                            summary.FileAnalyzed,
                            eventTelemetry.Properties[CompilerDataLogger.NumberOfBinaryAnalyzed]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.AnalysisStartTime] != summary.StartTimeUtc.ToString())
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.StartTimeUtc),
                            summary.StartTimeUtc,
                            eventTelemetry.Properties[CompilerDataLogger.AnalysisStartTime]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.AnalysisEndTime] != summary.EndTimeUtc.ToString())
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.EndTimeUtc),
                            summary.EndTimeUtc,
                            eventTelemetry.Properties[CompilerDataLogger.AnalysisEndTime]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.TimeConsumed] != summary.TimeConsumed.ToString())
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.TimeConsumed),
                            summary.TimeConsumed,
                            eventTelemetry.Properties[CompilerDataLogger.TimeConsumed]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.BuildDefinitionId] != summary.BuildDefinitionId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.BuildDefinitionId),
                            summary.BuildDefinitionId,
                            eventTelemetry.Properties[CompilerDataLogger.BuildDefinitionId]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.BuildDefinitionName] != summary.BuildDefinitionName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.BuildDefinitionName),
                            summary.BuildDefinitionName,
                            eventTelemetry.Properties["buildDefinitionName"]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.BuildRunId] != summary.BuildRunId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.BuildRunId),
                            summary.BuildRunId,
                            eventTelemetry.Properties[CompilerDataLogger.BuildRunId]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.ProjectName] != summary.ProjectName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.ProjectName),
                            summary.ProjectName,
                            eventTelemetry.Properties[CompilerDataLogger.ProjectName]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.OrganizationId] != summary.OrganizationId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.OrganizationId),
                            summary.OrganizationId,
                            eventTelemetry.Properties[CompilerDataLogger.OrganizationId]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.OrganizationName] != summary.OrganizationName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.OrganizationName),
                            summary.OrganizationName,
                            eventTelemetry.Properties[CompilerDataLogger.OrganizationName]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.ProjectId] != summary.ProjectId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.ProjectId),
                            summary.ProjectId,
                            eventTelemetry.Properties[CompilerDataLogger.ProjectId]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.RepositoryName] != summary.RepositoryName)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.RepositoryName),
                            summary.RepositoryName,
                            eventTelemetry.Properties[CompilerDataLogger.RepositoryName]));
                    }

                    if (eventTelemetry.Properties[CompilerDataLogger.RepositoryId] != summary.RepositoryId)
                    {
                        sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                            nameof(summary.RepositoryId),
                            summary.RepositoryId,
                            eventTelemetry.Properties[CompilerDataLogger.RepositoryId]));
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
                // TODO: https://github.com/microsoft/binskim/issues/626
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

        private StringBuilder ValidateChunkedEvents(string expectedEventName,
                                                    List<ITelemetry> telemetryGeneratedEvents,
                                                    string expectedChunkedContent,
                                                    int chunkCount)
        {
            var sb = new StringBuilder();
            string assemblyEventsId = string.Empty;
            string commandLineEventsId = string.Empty;

            var compilerEvents = new List<EventTelemetry>();
            var assemblyReferencesEvents = new List<EventTelemetry>();
            var commandLineEvents = new List<EventTelemetry>();
            var summaryEvents = new List<EventTelemetry>();

            foreach (EventTelemetry telemetryEvent in telemetryGeneratedEvents)
            {
                IDictionary<string, string> properties = telemetryEvent.Properties;

                if (string.IsNullOrWhiteSpace(commandLineEventsId)
                    && properties.TryGetValue(CompilerDataLogger.CommandLineId, out string commandLineEventId))
                {
                    commandLineEventsId = commandLineEventId;
                }

                if (string.IsNullOrWhiteSpace(assemblyEventsId)
                    && properties.TryGetValue(CompilerDataLogger.AssemblyReferencesId, out string assemblyEventId))
                {
                    assemblyEventsId = assemblyEventId;
                }

                switch (telemetryEvent.Name)
                {
                    case CompilerDataLogger.CompilerEventName:
                    {
                        compilerEvents.Add(telemetryEvent);

                        if (expectedEventName == CompilerDataLogger.AssemblyReferencesEventName)
                        {
                            if (!properties.TryGetValue(CompilerDataLogger.AssemblyReferencesId, out string compilerAssemblyReferencesId))
                            {
                                sb.AppendLine("Compiler Event is missing the `assemblyReferencesId`");
                            }
                            else if (assemblyEventsId != compilerAssemblyReferencesId)
                            {
                                sb.AppendLine(
                                    $"`{CompilerDataLogger.CompilerEventName}` event detected with unexpected Id. " +
                                    $"Expected `{assemblyEventsId}` but found `{compilerAssemblyReferencesId}`.");
                            }
                        }
                        else if (expectedEventName == CompilerDataLogger.CommandLineEventName)
                        {
                            if (!properties.TryGetValue(CompilerDataLogger.CommandLineId, out string compilerCommandLineEventId))
                            {
                                sb.AppendLine("Compiler Event is missing the `commandLineId`");
                            }
                            else if (commandLineEventsId != compilerCommandLineEventId)
                            {
                                sb.AppendLine(
                                    $"`{CompilerDataLogger.CommandLineEventName}` event detected with unexpected Id. " +
                                    $"Expected `{commandLineEventsId}` but found `{compilerCommandLineEventId}`.");
                            }
                        }
                        break;
                    }
                    case CompilerDataLogger.AssemblyReferencesEventName:
                    {
                        assemblyReferencesEvents.Add(telemetryEvent);

                        if (expectedEventName == CompilerDataLogger.AssemblyReferencesEventName
                            && !expectedChunkedContent.Contains(properties[CompilerDataLogger.ChunkedAssemblyReferences]))
                        {
                            sb.AppendLine(
                                $"Unexpected `{CompilerDataLogger.AssemblyReferencesEventName}` chunked content: " +
                                $"`{properties[CompilerDataLogger.ChunkedAssemblyReferences]}` " +
                                $"expected: `{expectedChunkedContent}`");
                        }

                        string currentAssemblyEventId = properties[CompilerDataLogger.AssemblyReferencesId];

                        if (assemblyEventsId != currentAssemblyEventId)
                        {
                            sb.AppendLine(
                                $"`{CompilerDataLogger.AssemblyReferencesEventName}` event detected with unexpected Id. " +
                                $"Expected `{assemblyEventsId}` but found `{currentAssemblyEventId}`.");
                        }

                        break;
                    }
                    case CompilerDataLogger.CommandLineEventName:
                    {
                        commandLineEvents.Add(telemetryEvent);

                        if (expectedEventName == CompilerDataLogger.CommandLineEventName
                            && !expectedChunkedContent.Contains(properties["chunkedcommandLine"]))
                        {
                            sb.AppendLine(
                                $"Unexpected `{CompilerDataLogger.CommandLineEventName}` chunked content: " +
                                $"`{properties[CompilerDataLogger.ChunkedCommandLine]}` " +
                                $"expected: `{expectedChunkedContent}`");
                        }

                        string currentCommandLineEventId = properties["commandLineId"];

                        if (commandLineEventsId != currentCommandLineEventId)
                        {
                            sb.AppendLine(
                                $"`{CompilerDataLogger.CommandLineEventName}` event detected with unexpected Id. " +
                                $"Expected `{commandLineEventsId}` but found `{currentCommandLineEventId}`.");
                        }
                        break;
                    }
                    case CompilerDataLogger.SummaryEventName:
                    {
                        summaryEvents.Add(telemetryEvent);
                        break;
                    }
                }
            }

            if ((expectedEventName == CompilerDataLogger.AssemblyReferencesEventName
                    && assemblyReferencesEvents.Count != chunkCount) ||
                (expectedEventName == CompilerDataLogger.CommandLineEventName
                    && commandLineEvents.Count != chunkCount))
            {
                sb.AppendLine(
                    $"Expected `{chunkCount}` `{expectedEventName}` events, " +
                    $"but `{assemblyReferencesEvents.Count}` were found");
            }

            return sb;
        }

        private StringBuilder ValidateChunkedContent(StringBuilder sb,
                                                     int expectedChunkSize,
                                                     List<EventTelemetry> chunkedEvents)
        {
            if (chunkedEvents.Count != expectedChunkSize)
            {
                sb.AppendLine(
                    string.Format(
                        "Incorrect number of chunkedEvents de tected. Expected {0}, but found {1}",
                        expectedChunkSize,
                        chunkedEvents.Count));
            }

            return sb;
        }

        public static string GetExampleSarifPath(Sarif.SarifVersion sarifVersion)
        {
            return sarifVersion == Sarif.SarifVersion.Current
                ? Path.Combine(PEBinaryTests.BaselineTestDataDirectory, ExpectedFolder, SampleSarifPath)
                : Path.Combine(GetTestDirectory("Test.UnitTests.BinSkim.Driver"), "Samples", "Native_x86_VS2019_SDL_Enabled_Sarif.v1.0.0.sarif");
        }

        internal static string GetTestDirectory(string relativeDirectory)
        {
            string codeBasePath = Assembly.GetExecutingAssembly().Location;
            string dirPath = Path.GetDirectoryName(codeBasePath);
            dirPath = Path.Combine(dirPath, "..", "..", "..", "..", "src");
            dirPath = Path.GetFullPath(dirPath);
            return Path.Combine(dirPath, relativeDirectory);
        }

        internal static int CalculateChunkedContentSize(int contentLength)
        {
            return (int)Math.Ceiling(1.0 * contentLength / CompilerDataLogger.s_chunkSize);
        }
    }
}
