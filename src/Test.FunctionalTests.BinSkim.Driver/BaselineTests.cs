// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using FluentAssertions;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.BinSkim.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Microsoft.CodeAnalysis.Sarif.Writers;

using Newtonsoft.Json;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.IL
{
    public class BuiltInRuleFunctionalTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private TelemetryConfiguration telemetryConfiguration;

        public BuiltInRuleFunctionalTests(ITestOutputHelper output)
        {
            this.testOutputHelper = output;
        }

        [Fact]
        public void Driver_BuiltInRuleFunctionalTests()
        {
            if (PlatformSpecificHelpers.RunningOnWindows())
            {
                this.BatchRuleRules(string.Empty, Sarif.SarifVersion.Current, "*.dll", "*.exe", "gcc.*", "clang.*", "macho.*");
            }
        }

        [Fact]
        public void Driver_ShouldLogCompilerTelemetryEvents_Managed()
        {
            // We cannot load the .dll on Unix.
            if (!PlatformSpecificHelpers.RunningOnWindows())
            {
                return;
            }

            List<ITelemetry> sendItems = CompilerTelemetryTestSetup();
            var sb = new StringBuilder();
            string testDirectory = PEBinaryTests.BaselineTestDataDirectory + Path.DirectorySeparatorChar;
            string testFile = Path.Combine(testDirectory, "DotNetCore_win-x86_VS2019_Default.dll");

            SarifLog sarifResult = RunRules(sb, testFile);

            List<CompilerData> records = CollectCompilerDetails(binaryPath: testFile,
                                                                out int expectedSummaryEventCount,
                                                                out int expectedCompilerEventCount,
                                                                out int expectedAssemblyReferencesEventCount,
                                                                out int expectedCommandLineEventCount);

            var compilerEvents = new List<EventTelemetry>();
            var assemblyReferencesEvents = new List<EventTelemetry>();
            var commandLineEvents = new List<EventTelemetry>();
            var summaryEvents = new List<EventTelemetry>();
            var ruleSummaryEvents = new List<EventTelemetry>();
            var analysisRequests = new List<RequestTelemetry>();

            foreach (ITelemetry telemetryItem in sendItems)
            {
                switch (telemetryItem)
                {
                    case EventTelemetry telemetryEvent:
                        switch (telemetryEvent.Name)
                        {
                            case CompilerDataLogger.CompilerEventName: compilerEvents.Add(telemetryEvent); break;
                            case CompilerDataLogger.AssemblyReferencesEventName: assemblyReferencesEvents.Add(telemetryEvent); break;
                            case CompilerDataLogger.CommandLineEventName: commandLineEvents.Add(telemetryEvent); break;
                            case CompilerDataLogger.SummaryEventName: summaryEvents.Add(telemetryEvent); break;
                            case RuleTelemetryLogger.RuleSummaryEventName: ruleSummaryEvents.Add(telemetryEvent); break;
                        }

                        break;

                    case RequestTelemetry request:
                        switch (request.Name)
                        {
                            case RuleTelemetryLogger.AnalysisRequestName: analysisRequests.Add(request); break;
                        }

                        break;

                    default:
                        break;
                }
            }

            summaryEvents.Should().NotBeNull();
            if (summaryEvents.Count != expectedSummaryEventCount)
            {
                sb.AppendLine(string.Format("Expected {0} summary event per binary, but found {1}.",
                                            expectedSummaryEventCount,
                                            summaryEvents.Count));
            }

            assemblyReferencesEvents.Should().NotBeNull();
            if (assemblyReferencesEvents.Count != expectedAssemblyReferencesEventCount)
            {
                sb.AppendLine(string.Format("Expected {0} AssemblyReferencesEvent, but found {1}",
                                            expectedAssemblyReferencesEventCount,
                                            assemblyReferencesEvents.Count));
            }

            // Managed code will not result in any Command Line Events.
            commandLineEvents.Should().NotBeNull();
            if (commandLineEvents.Count != expectedCommandLineEventCount)
            {
                sb.AppendLine(string.Format("Expected {0} CommandLineEvents, but found {1}",
                                            expectedCommandLineEventCount,
                                            commandLineEvents.Count));
            }

            compilerEvents.Should().NotBeNull();
            if (compilerEvents.Count != expectedCompilerEventCount)
            {
                sb.AppendFormat("Expected {0} CompilerEvent, but found {1}",
                                            expectedCompilerEventCount,
                                            compilerEvents.Count);
            }

            string summaryEventSessionId = summaryEvents.First().Context.Session.Id;
            string assemblyReferencesEventSessionId = assemblyReferencesEvents.First().Context.Session.Id;
            string compilerEventSessionId = compilerEvents.First().Context.Session.Id;

            if (summaryEventSessionId != assemblyReferencesEventSessionId)
            {
                sb.AppendFormat(
                    "SessionIds did not match. `SummaryEvent.SessionId` was {0} and `AssemblyReferencesEvent.SessionId` was {1}",
                    summaryEventSessionId,
                    assemblyReferencesEventSessionId);
            }

            if (summaryEventSessionId != compilerEventSessionId)
            {
                sb.AppendFormat(
                    "SessionIds did not match. `SummaryEvent.SessionId` was {0} and `CompilerEvent.SessionId` was {1}",
                    summaryEventSessionId,
                    compilerEventSessionId);
            }

            analysisRequests.Count.Should().Be(1);
            string expectedRuntimeConditions = (RuntimeConditions.RuleNotApplicableToTarget | RuntimeConditions.OneOrMoreWarningsFired).ToString();
            string actualRuntimeConditions = analysisRequests.Single().ResponseCode;
            if (actualRuntimeConditions != expectedRuntimeConditions)
            {
                sb.AppendFormat(
                    "Expected {0}, runtime conditions in AnalysisStopped event but found {1}",
                    expectedRuntimeConditions,
                    actualRuntimeConditions);
            }

            ValidateRuleSummaryEvents(sarifResult, ruleSummaryEvents);

            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifResult, string.Empty, null);

            ValidateSummaryEvent(summary, summaryEvents.First(), sb);
        }

        [Fact]
        public void Driver_ShouldLogCompilerTelemetryEvents_Unmanaged()
        {
            // We cannot load the .dll on Unix.
            if (!PlatformSpecificHelpers.RunningOnWindows())
            {
                return;
            }

            List<ITelemetry> sendItems = CompilerTelemetryTestSetup();
            var sb = new StringBuilder();
            string testDirectory = PEBinaryTests.BaselineTestDataDirectory + Path.DirectorySeparatorChar;
            string testFile = Path.Combine(testDirectory, "Native_x64_VS2015_Default.dll");

            SarifLog sarifResult = RunRules(sb, testFile);

            List<CompilerData> records = CollectCompilerDetails(binaryPath: testFile,
                                                                out int expectedSummaryEventCount,
                                                                out int expectedCompilerEventCount,
                                                                out int expectedAssemblyReferencesEventCount,
                                                                out int expectedCommandLineEventCount);

            var compilerEvents = new List<EventTelemetry>();
            var assemblyReferencesEvents = new List<EventTelemetry>();
            var commandLineEvents = new List<EventTelemetry>();
            var summaryEvents = new List<EventTelemetry>();
            var ruleSummaryEvents = new List<EventTelemetry>();
            var analysisRequests = new List<RequestTelemetry>();

            foreach (ITelemetry telemetryItem in sendItems)
            {
                switch (telemetryItem)
                {
                    case EventTelemetry telemetryEvent:
                        switch (telemetryEvent.Name)
                        {
                            case CompilerDataLogger.CompilerEventName: compilerEvents.Add(telemetryEvent); break;
                            case CompilerDataLogger.AssemblyReferencesEventName: assemblyReferencesEvents.Add(telemetryEvent); break;
                            case CompilerDataLogger.CommandLineEventName: commandLineEvents.Add(telemetryEvent); break;
                            case CompilerDataLogger.SummaryEventName: summaryEvents.Add(telemetryEvent); break;
                            case RuleTelemetryLogger.RuleSummaryEventName: ruleSummaryEvents.Add(telemetryEvent); break;
                        }

                        break;

                    case RequestTelemetry request:
                        switch (request.Name)
                        {
                            case RuleTelemetryLogger.AnalysisRequestName: analysisRequests.Add(request); break;
                        }

                        break;

                    default:
                        break;
                }
            }

            summaryEvents.Should().NotBeNull();
            if (summaryEvents.Count != expectedSummaryEventCount)
            {
                sb.AppendLine(string.Format("Expected {0} summary event per binary, but found {1}.",
                                            expectedSummaryEventCount,
                                            summaryEvents.Count));
            }

            assemblyReferencesEvents.Should().NotBeNull();
            if (assemblyReferencesEvents.Count != expectedAssemblyReferencesEventCount)
            {
                sb.AppendLine(string.Format("Expected {0} AssemblyReferencesEvent, but found {1}",
                                            expectedAssemblyReferencesEventCount,
                                            assemblyReferencesEvents.Count));
            }

            // Managed code will not result in any Command Line Events.
            commandLineEvents.Should().NotBeNull();
            if (commandLineEvents.Count != expectedCommandLineEventCount)
            {
                sb.AppendLine(string.Format("Expected {0} CommandLineEvents, but found {1}",
                                            expectedCommandLineEventCount,
                                            commandLineEvents.Count));
            }

            compilerEvents.Should().NotBeNull();
            if (compilerEvents.Count != expectedCompilerEventCount)
            {
                sb.AppendLine(string.Format("Expected {0} CompilerEvent, but found {1}",
                                            expectedCompilerEventCount,
                                            compilerEvents.Count));
            }

            summaryEvents.Count.Should().Be(1);
            assemblyReferencesEvents.Count.Should().Be(0);
            commandLineEvents.Count.Should().Be(25);
            compilerEvents.Count.Should().Be(35);

            summaryEvents.First().Context.Session.Id
                .Should().Be(commandLineEvents.First().Context.Session.Id);

            summaryEvents.First().Context.Session.Id
                .Should().Be(compilerEvents.First().Context.Session.Id);

            analysisRequests.Count.Should().Be(1);
            RuntimeConditions expectedRuntimeConditions =
                RuntimeConditions.RuleNotApplicableToTarget |
                RuntimeConditions.OneOrMoreWarningsFired |
                RuntimeConditions.OneOrMoreErrorsFired;

            analysisRequests.First().ResponseCode.Should().Be(expectedRuntimeConditions.ToString());

            ValidateRuleSummaryEvents(sarifResult, ruleSummaryEvents);

            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifResult, string.Empty, null);

            ValidateSummaryEvent(summary, summaryEvents.First(), sb);
        }

        private List<CompilerData> CollectCompilerDetails(string binaryPath,
                                                          out int expectedSummaryEvents,
                                                          out int expectedCompilerEvents,
                                                          out int expectedAssemblyReferencesEvents,
                                                          out int expectedCommandLineEvents)
        {
            var peBinary = new PEBinary(new Uri(binaryPath));
            Pdb pdb = peBinary.Pdb;
            var records = new List<CompilerData>();

            expectedSummaryEvents = 1;
            expectedCompilerEvents = 0;
            expectedAssemblyReferencesEvents = 0;
            expectedCommandLineEvents = 0;

            if (peBinary.PE.IsManaged)
            {
                records.Add(new CompilerData
                {
                    BinaryType = "PE",
                    CompilerName = ".NET Compiler",
                    Language = nameof(Language.MSIL),
                    DebuggingFileName = pdb.GlobalScope?.Name,
                    DebuggingFileGuid = pdb.GlobalScope?.Guid.ToString(),
                    FileVersion = peBinary.PE.FileVersion?.FileVersion,
                    CompilerBackEndVersion = peBinary.PE.LinkerVersion.ToString(),
                    CompilerFrontEndVersion = peBinary.PE.LinkerVersion.ToString(),
                    AssemblyReferences = string.Join(';', peBinary.PE.GetAssemblyReferenceStrings())
                });
            }
            else
            {
                foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
                {
                    Symbol om = omView.Value;
                    ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                    records.Add(new CompilerData
                    {
                        BinaryType = "PE",
                        ModuleName = omDetails?.Name,
                        ModuleLibrary = omDetails?.Library,
                        Dialect = omDetails.GetDialect(out _),
                        CompilerName = omDetails.CompilerName,
                        CommandLine = omDetails.RawCommandLine,
                        Language = omDetails.Language.ToString(),
                        DebuggingFileName = pdb.GlobalScope?.Name,
                        FileVersion = peBinary.PE.FileVersion?.FileVersion,
                        DebuggingFileGuid = pdb.GlobalScope?.Guid.ToString(),
                        CompilerBackEndVersion = omDetails.CompilerBackEndVersion.ToString(),
                        CompilerFrontEndVersion = omDetails.CompilerFrontEndVersion.ToString(),
                    });
                }
            }

            foreach (CompilerData record in records)
            {
                expectedCompilerEvents += 1;
                if (record.AssemblyReferences != null)
                {
                    expectedAssemblyReferencesEvents += CalculateChunkedContentSize(record.AssemblyReferences.Length);
                }

                if (record.CommandLine != null)
                {
                    expectedCommandLineEvents += CalculateChunkedContentSize(record.CommandLine.Length);
                }
            }

            return records;
        }

        private void BatchRuleRules(string ruleName, Sarif.SarifVersion version, params string[] inputFilters)
        {
            var sb = new StringBuilder();
            string testDirectory = PEBinaryTests.BaselineTestDataDirectory + Path.DirectorySeparatorChar + ruleName;

            foreach (string inputFilter in inputFilters)
            {
                string[] testFiles = Directory.GetFiles(testDirectory, inputFilter);

                foreach (string file in testFiles)
                {
                    this.RunRules(sb, file, version);
                }
            }

            if (sb.Length == 0)
            {
                // Test passes
                return;
            }

            string rebaselineMessage = "If the actual output is expected, generate new baselines by executing `UpdateBaselines.ps1` from a PS command prompt.";
            sb.AppendLine(string.Format(CultureInfo.CurrentCulture, rebaselineMessage));

            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Run the following to all test baselines vs. actual results:");
                sb.AppendLine(this.GenerateDiffCommand(
                    Path.Combine(testDirectory, "Expected"),
                    Path.Combine(testDirectory, "Actual")));
                this.testOutputHelper.WriteLine(sb.ToString());
            }

            Assert.Equal(0, sb.Length);
        }

        private SarifLog RunRules(StringBuilder sb, string inputFileName, Sarif.SarifVersion version = Sarif.SarifVersion.Current)
        {
            string fileName = Path.GetFileName(inputFileName);
            string actualDirectory = Path.Combine(Path.GetDirectoryName(inputFileName), "Actual");
            string expectedDirectory;
            if (PlatformSpecificHelpers.RunningOnWindows())
            {
                expectedDirectory = Path.Combine(Path.GetDirectoryName(inputFileName), "Expected");
            }
            else
            {
                expectedDirectory = Path.Combine(Path.GetDirectoryName(inputFileName), "NonWindowsExpected");
            }

            if (!Directory.Exists(actualDirectory))
            {
                Directory.CreateDirectory(actualDirectory);
            }

            string expectedFileName = Path.Combine(expectedDirectory, fileName + ".sarif");
            string actualFileName = Path.Combine(actualDirectory, fileName + ".sarif");

            using var telemetry = new Sdk.Telemetry(this.telemetryConfiguration);
            var command = new MultithreadedAnalyzeCommand(telemetry);
            var options = new AnalyzeOptions
            {
                Force = true,
                Recurse = false,
                PrettyPrint = true,
                DataToInsert = new[] { OptionallyEmittedData.Hashes },
                DataToRemove = new[] { OptionallyEmittedData.NondeterministicProperties },
                OutputFilePath = actualFileName,
                ConfigurationFilePath = "default",
                SarifOutputVersion = Sarif.SarifVersion.Current,
                TargetFileSpecifiers = new string[] { inputFileName },
                Traces = Array.Empty<string>(),
                Level = new List<FailureLevel> { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note },
                Kind = new List<ResultKind> { ResultKind.Fail, ResultKind.Pass },
            };

            command.UnitTestOutputVersion = version;

            int result = command.Run(options);

            // Note that we don't ensure a success code. That is because we
            // are running end-to-end tests for valid and invalid files

            var settings = new JsonSerializerSettings()
            {
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            string expectedText = File.ReadAllText(expectedFileName);
            string actualText = File.ReadAllText(actualFileName);

            // Replace repository root absolute path for machine and enlistment independence
            string repoRoot = Path.GetFullPath(Path.Combine(actualDirectory, "..", "..", "..", ".."));
            string normalizedRoot = PlatformSpecificHelpers.RunningOnWindows() ? @"Z:" : @"/home/user";
            actualText = actualText.Replace(repoRoot.Replace(@"\", @"\\"), normalizedRoot);
            actualText = actualText.Replace(repoRoot.Replace(@"\", @"/"), normalizedRoot);

            // Remove stack traces as they can change due to inlining differences by configuration and runtime.
            actualText = Regex.Replace(actualText, @"\\r\\n   at [^""]+", "");

            actualText = actualText.Replace(@"""Sarif""", @"""BinSkim""");
            actualText = actualText.Replace(@"        ""fileVersion"": ""15.0.0""," + Environment.NewLine, string.Empty);

            actualText = Regex.Replace(actualText, @"\s*""product""[^\n]+?\n", Environment.NewLine);
            actualText = Regex.Replace(actualText, @"\s*""organization""[^\n]+?\n", Environment.NewLine);

            actualText = Regex.Replace(actualText, @"\s*""fullName""[^\n]+?\n", Environment.NewLine);
            actualText = Regex.Replace(actualText, @"\s*""semanticVersion""[^\n]+?\n", Environment.NewLine);
            actualText = Regex.Replace(actualText, @"      ""id""[^,]+,\s+""tool""", @"      ""tool""", RegexOptions.Multiline);

            // Write back the normalized actual text so that the diff command given on failure shows what was actually compared.

            Encoding utf8encoding = new UTF8Encoding(true);
            using (var textWriter = new StreamWriter(actualFileName, false, utf8encoding))
            {
                textWriter.Write(actualText);
            }

            // Make sure we can successfully deserialize what was just generated
            SarifLog expectedLog = PrereleaseCompatibilityTransformer.UpdateToCurrentVersion(
                                    expectedText,
                                    settings.Formatting,
                                    out expectedText);

            SarifLog actualLog = JsonConvert.DeserializeObject<SarifLog>(actualText, settings);

            var visitor = new ResultDiffingVisitor(expectedLog);

            if (!visitor.Diff(actualLog.Runs[0].Results))
            {
                string errorMessage = "The output of the tool did not match for input {0}.";
                sb.AppendLine(string.Format(CultureInfo.CurrentCulture, errorMessage, inputFileName));
                sb.AppendLine("Check differences with:");
                sb.AppendLine(this.GenerateDiffCommand(expectedFileName, actualFileName));
            }

            return actualLog;
        }

        private List<ITelemetry> CompilerTelemetryTestSetup()
        {
            List<ITelemetry> sendItems = new List<ITelemetry>();
            this.telemetryConfiguration = new TelemetryConfiguration
            {
                ConnectionString = $"InstrumentationKey={Guid.NewGuid()}",
                TelemetryChannel = new StubTelemetryChannel { OnSend = sendItems.Add }
            };

            this.telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
            return sendItems;
        }

        /// <summary>
        /// Check the rule summary metrics against the actual result.
        /// </summary>
        /// <param name="sarifResult">The actual SARIF results.</param>
        /// <param name="ruleSummaryEvents">Rule summary events from telemetry.</param>
        private static void ValidateRuleSummaryEvents(SarifLog sarifResult, List<EventTelemetry> ruleSummaryEvents)
        {
            ruleSummaryEvents.Count.Should().Be(sarifResult.Runs[0].Tool.Driver.Rules.Count);
            foreach (EventTelemetry ruleSummaryEvent in ruleSummaryEvents)
            {
                string ruleId = ruleSummaryEvent.Properties[RuleTelemetryLogger.RuleIdPropertyName];

                // Note that there may be more than one result for a rule (e.g. BA2004), so
                // we need to count.
                int expectedPassCount = 0, expectedFailCount = 0;
                foreach (Result result in sarifResult.Runs[0].Results.Where(r => r.RuleId == ruleId))
                {
                    if (result.Kind == ResultKind.Pass)
                    {
                        expectedPassCount++;
                    }
                    else
                    {
                        expectedFailCount++;
                    }
                }

                if (!ruleSummaryEvent.Metrics.TryGetValue(RuleTelemetryLogger.PassCountMetricName, out double actualPassCount))
                {
                    actualPassCount = 0;
                }

                if (!ruleSummaryEvent.Metrics.TryGetValue(RuleTelemetryLogger.FailCountMetricName, out double actualFailCount))
                {
                    actualFailCount = 0;
                }

                actualPassCount.Should().Be(expectedPassCount);
                actualFailCount.Should().Be(expectedFailCount);
            }
        }

        private StringBuilder ValidateSummaryEvent(AnalysisSummary summary,
                                                   EventTelemetry eventTelemetry,
                                                   StringBuilder sb)
        {
            string projectIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.ProjectIdVariableName);
            string projectNameVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.ProjectNameVariableName);
            string repositoryIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.RepositoryIdVariableName);
            string organizationIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.OrganizationIdVariableName);
            string repositoryNameVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.RepositoryNameVariableName);
            string organizationNameVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.OrganizationNameVariableName);
            string buildDefinitionIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.BuildDefinitionIdVariableName);
            string buildDefinitionNameVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.BuildDefinitionNameVariableName);
            string buildDefinitionRunIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.BuildDefinitionRunIdVariableName);

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

            if (eventTelemetry.Properties[CompilerDataLogger.BuildDefinitionId] != buildDefinitionIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.BuildDefinitionId),
                                        buildDefinitionIdVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.BuildDefinitionId]));
            }

            if (eventTelemetry.Properties[CompilerDataLogger.BuildDefinitionName] != buildDefinitionNameVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.BuildDefinitionName),
                                        buildDefinitionNameVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.BuildDefinitionName]));
            }

            if (eventTelemetry.Properties[CompilerDataLogger.BuildRunId] != buildDefinitionRunIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.BuildRunId),
                                        buildDefinitionRunIdVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.BuildRunId]));
            }

            if (eventTelemetry.Properties[CompilerDataLogger.ProjectName] != projectNameVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.ProjectName),
                                        projectNameVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.ProjectName]));
            }

            if (eventTelemetry.Properties[CompilerDataLogger.OrganizationId] != organizationIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.OrganizationId),
                                        organizationIdVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.OrganizationId]));
            }

            if (eventTelemetry.Properties[CompilerDataLogger.OrganizationName] != organizationNameVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.OrganizationName),
                                        organizationNameVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.OrganizationName]));
            }

            if (eventTelemetry.Properties[CompilerDataLogger.ProjectId] != projectIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.ProjectId),
                                        projectIdVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.ProjectId]));
            }

            if (eventTelemetry.Properties[CompilerDataLogger.RepositoryName] != repositoryNameVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.RepositoryName),
                                        repositoryNameVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.RepositoryName]));
            }

            if (eventTelemetry.Properties[CompilerDataLogger.RepositoryId] != repositoryIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                                        nameof(summary.RepositoryId),
                                        repositoryIdVariable,
                                        eventTelemetry.Properties[CompilerDataLogger.RepositoryId]));
            }

            sb.ToString().Should().Be(string.Empty);

            return sb;
        }

        private string GenerateDiffCommand(string expected, string actual)
        {
            expected = Path.GetFullPath(expected);
            actual = Path.GetFullPath(actual);

            string beyondCompare = TryFindBeyondCompare();
            if (beyondCompare != null)
            {
                return string.Format(CultureInfo.InvariantCulture, "\"{0}\" \"{1}\" \"{2}\" /title1=Expected /title2=Actual", beyondCompare, expected, actual);
            }

            if (PlatformSpecificHelpers.RunningOnWindows())
            {
                return string.Format(CultureInfo.InvariantCulture, "windiff \"{0}\" \"{1}\"", expected, actual);
            }
            else
            {
                return string.Format(CultureInfo.InvariantCulture, "diff \"{0}\", \"{1}\"", expected, actual);
            }
        }

        private static int CalculateChunkedContentSize(int contentLength)
        {
            return (int)Math.Ceiling(1.0 * contentLength / CompilerDataLogger.s_chunkSize);
        }

        private static string TryFindBeyondCompare()
        {
            var directories = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            directories.Add(programFiles);
            directories.Add(programFiles.Replace(" (x86)", ""));

            foreach (string directory in directories)
            {
                for (int idx = 4; idx >= 3; --idx)
                {
                    string beyondComparePath = string.Format(CultureInfo.InvariantCulture, "{0}\\Beyond Compare {1}\\BComp.exe", directory, idx);
                    if (File.Exists(beyondComparePath))
                    {
                        return beyondComparePath;
                    }
                }

                string beyondCompare2Path = programFiles + "\\Beyond Compare 2\\BC2.exe";
                if (File.Exists(beyondCompare2Path))
                {
                    return beyondCompare2Path;
                }
            }

            return null;
        }
    }
}
