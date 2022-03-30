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

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinSkim.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
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

        public BuiltInRuleFunctionalTests(ITestOutputHelper output)
        {
            this.testOutputHelper = output;
        }

        [Fact]
        public void Driver_BuiltInRuleFunctionalTests()
        {
            MultithreadedAnalyzeCommand.s_UnitTestOutputVersion = Sarif.SarifVersion.Current;
            this.BatchRuleRules(string.Empty, "*.dll", "*.exe", "gcc.*", "clang.*", "macho.*");
        }

        [Fact]
        public void Driver_ShouldLogCompilerTelemetryEvents_Managed()
        {

            List<ITelemetry> sendItems = CompilerTelemetryTestSetup();
            var sb = new StringBuilder();
            string testDirectory = PEBinaryTests.BaselineTestDataDirectory + Path.DirectorySeparatorChar;
            string testFile = Path.Combine(testDirectory, "DotNetCore_win-x86_VS2019_Default.dll");

            SarifLog sarifResult = RunRules(sb, testFile);

            string testEnviornmentVar = Environment.GetEnvironmentVariable(nameof(AnalysisSummaryExtractor.ProjectIdVariableName), EnvironmentVariableTarget.Process);

            sendItems.All<ITelemetry>(item => item.GetType() == typeof(EventTelemetry));
            List<EventTelemetry> events = sendItems.OfType<EventTelemetry>().ToList();

            List<EventTelemetry> compilerEvents = events.Where(e => e.Name == CompilerDataLogger.CompilerEventName)
                .OfType<EventTelemetry>().ToList();

            List<EventTelemetry> assemblyReferencesEvents = events.Where(e => e.Name == CompilerDataLogger.AssemblyReferencesEventName)
                .OfType<EventTelemetry>().ToList();

            List<EventTelemetry> commandLineEvents = events.Where(e => e.Name == CompilerDataLogger.CommandLineEventName)
                .OfType<EventTelemetry>().ToList();

            List<EventTelemetry> summaryEvents = events.Where(e => e.Name == CompilerDataLogger.SummaryEventName)
                .OfType<EventTelemetry>().ToList();

            if (summaryEvents.Count != 1)
            {
                sb.AppendLine("Expected at least 1 summaryEvent, but found none.");
            }
            if (assemblyReferencesEvents.Count != 1)
            {
                sb.AppendLine(string.Format("Expected 1 AssemblyReferencesEvent, but found {0}", assemblyReferencesEvents.Count));
            }
            if (commandLineEvents.Count != 0)
            {
                sb.AppendLine(string.Format("Expected 0 CommandLineEvents, but found {0}", commandLineEvents.Count));
            }
            if (compilerEvents.Count != 1)
            {
                sb.AppendLine(string.Format("Expected 1 CompilerEvent, but found {0}", compilerEvents.Count));
            }

            string summaryEventSessionId = summaryEvents.First().Properties["sessionId"];
            string assemblyReferencesEventSessionId = assemblyReferencesEvents.First().Properties["sessionId"];
            string compilerEventSessionId = compilerEvents.First().Properties["sessionId"];

            if (summaryEventSessionId != assemblyReferencesEventSessionId)
            {
                sb.AppendLine(
                    string.Format("SessionIds did not match. `SummaryEvent.SessionId` was {0} and `AssemblyReferencesEvent.SessionId` was {1}",
                    summaryEventSessionId,
                    assemblyReferencesEventSessionId));
            }

            if (summaryEventSessionId != compilerEventSessionId)
            {
                sb.AppendLine(
                    string.Format("SessionIds did not match. `SummaryEvent.SessionId` was {0} and `CompilerEvent.SessionId` was {1}",
                    summaryEventSessionId,
                    compilerEventSessionId));
            }

            AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(sarifResult, "", null);

            ValidateSummaryEvent(summary, summaryEvents.First(), sb);

            sb.ToString().Should().Be("");
        }

        [Fact]
        public void Driver_ShouldLogCompilerTelemetryEvents_Unmanaged()
        {

            List<ITelemetry> sendItems = CompilerTelemetryTestSetup();
            var sb = new StringBuilder();
            string testDirectory = PEBinaryTests.BaselineTestDataDirectory + Path.DirectorySeparatorChar;
            string testFile = Path.Combine(testDirectory, "Native_x64_VS2015_Default.dll");

            SarifLog sarifResult = RunRules(sb, testFile);

            sendItems.All<ITelemetry>(item => item.GetType() == typeof(EventTelemetry));
            List<EventTelemetry> events = sendItems.OfType<EventTelemetry>().ToList();

            List<EventTelemetry> compilerEvents = events.Where(e => e.Name == CompilerDataLogger.CompilerEventName)
                .OfType<EventTelemetry>().ToList();

            List<EventTelemetry> assemblyReferencesEvents = events.Where(e => e.Name == CompilerDataLogger.AssemblyReferencesEventName)
                .OfType<EventTelemetry>().ToList();

            List<EventTelemetry> commandLineEvents = events.Where(e => e.Name == CompilerDataLogger.CommandLineEventName)
                .OfType<EventTelemetry>().ToList();

            List<EventTelemetry> summaryEvents = events.Where(e => e.Name == CompilerDataLogger.SummaryEventName)
                .OfType<EventTelemetry>().ToList();

            summaryEvents.Count.Should().Be(1);
            assemblyReferencesEvents.Count.Should().Be(0);
            commandLineEvents.Count.Should().Be(25);
            compilerEvents.Count.Should().Be(35);

            summaryEvents.First().Properties["sessionId"]
                .Should().Be(commandLineEvents.First().Properties["sessionId"]);

            summaryEvents.First().Properties["sessionId"]
                .Should().Be(compilerEvents.First().Properties["sessionId"]);

            Assert.Equal(0, sb.Length);
        }

        private void BatchRuleRules(string ruleName, params string[] inputFilters)
        {
            var sb = new StringBuilder();
            string testDirectory = PEBinaryTests.BaselineTestDataDirectory + Path.DirectorySeparatorChar + ruleName;

            foreach (string inputFilter in inputFilters)
            {
                string[] testFiles = Directory.GetFiles(testDirectory, inputFilter);

                foreach (string file in testFiles)
                {
                    this.RunRules(sb, file);
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

        private SarifLog RunRules(StringBuilder sb, string inputFileName)
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

            var command = new MultithreadedAnalyzeCommand();
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
            // Setup mocks for CompilerDataLogger.
            MultithreadedAnalyzeCommand.s_UnitTestOutputVersion = Sarif.SarifVersion.Current;

            List<ITelemetry> sendItems = null;
            sendItems = new List<ITelemetry>();
            TelemetryClient telemetryClient;
            TelemetryConfiguration telemetryConfiguration;
            telemetryConfiguration = new TelemetryConfiguration();
            telemetryConfiguration.InstrumentationKey = Guid.NewGuid().ToString();
            telemetryConfiguration.TelemetryChannel = new StubTelemetryChannel { OnSend = item => sendItems.Add(item) };
            telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
            telemetryClient = new TelemetryClient(telemetryConfiguration);
            CompilerDataLogger.s_injectedTelemetryClient = telemetryClient;
            CompilerDataLogger.s_injectedTelemetryConfiguration = telemetryConfiguration;

            return sendItems;
        }

        private void ValidateSummaryEvent(AnalysisSummary summary, EventTelemetry eventTelemetry, StringBuilder sb)
        {
            string projectIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.ProjectIdVariableName);
            string projectNameVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.ProjectNameVariableName);
            string repositoryIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.RepositoryIdVariableName);
            string organizationIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.OrganizationIdVariableName);
            string buildDefinitionRunIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.BuildDefinitionRunIdVariableName);
            string repositoryNameVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.RepositoryNameVariableName);
            string OrganizationNameVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.OrganizationNameVariableName);
            string BuildDefinitionIdVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.BuildDefinitionIdVariableName);
            string BuildDefinitionNameVariable = Environment.GetEnvironmentVariable(AnalysisSummaryExtractor.BuildDefinitionNameVariableName);

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

            if (eventTelemetry.Properties["buildDefinitionId"] != BuildDefinitionIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.BuildDefinitionId),
                    BuildDefinitionIdVariable,
                    eventTelemetry.Properties["buildDefinitionId"]));
            }
            if (eventTelemetry.Properties["buildDefinitionName"] != BuildDefinitionNameVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.BuildDefinitionName),
                    BuildDefinitionNameVariable,
                    eventTelemetry.Properties["buildDefinitionName"]));
            }
            if (eventTelemetry.Properties["buildRunId"] != buildDefinitionRunIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.BuildRunId),
                    buildDefinitionRunIdVariable,
                    eventTelemetry.Properties["buildRunId"]));
            }
            if (eventTelemetry.Properties["projectName"] != projectNameVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.ProjectName),
                    projectNameVariable,
                    eventTelemetry.Properties["projectName"]));
            }
            if (eventTelemetry.Properties["organizationId"] != organizationIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.OrganizationId),
                    organizationIdVariable,
                    eventTelemetry.Properties["organizationId"]));
            }
            if (eventTelemetry.Properties["organizationName"] != OrganizationNameVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.OrganizationName),
                    OrganizationNameVariable,
                    eventTelemetry.Properties["organizationName"]));
            }
            if (eventTelemetry.Properties["projectId"] != projectIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.ProjectId),
                    projectIdVariable,
                    eventTelemetry.Properties["projectId"]));
            }
            if (eventTelemetry.Properties["repositoryName"] != repositoryNameVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.RepositoryName),
                    repositoryNameVariable,
                    eventTelemetry.Properties["repositoryName"]));
            }
            if (eventTelemetry.Properties["repositoryId"] != repositoryIdVariable)
            {
                sb.Append(string.Format("Unexpected {0} in `SummaryEvent`. Expected {1}, found {2}.",
                    nameof(summary.RepositoryId),
                    repositoryIdVariable,
                    eventTelemetry.Properties["repositoryId"]));
            }
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
