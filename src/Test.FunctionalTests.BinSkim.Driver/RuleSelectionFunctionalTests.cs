// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Driver
{
    public class RuleSelectionFunctionalTests
    {
        [Fact]
        public void RunOnlyRules_ProducesOnlySpecifiedRuleResults()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(Path.GetTempPath(), $"RunOnlyRules_{Guid.NewGuid()}.sarif");
            try
            {
                var options = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { GetThisTestAssemblyFilePath() },
                    OutputFilePath = fileName,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    RunOnlyRules = new[] { "BA2016" },
                    DisableTelemetry = true,
                    Kind = new[] { ResultKind.Fail, ResultKind.Pass, ResultKind.NotApplicable },
                    Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note },
                };

                var command = new MultithreadedAnalyzeCommand();
                int exitCode = command.Run(options);

                var log = SarifLog.Load(fileName);
                var ruleIds = log.Runs[0].Results.Select(r => r.RuleId).Distinct().ToList();

                ruleIds.Should().OnlyContain(id => id == "BA2016",
                    "only BA2016 should produce results when --run-only-rules BA2016 is specified");
            }
            finally
            {
                File.Delete(fileName);
            }
        }

        [Fact]
        public void RunOnlyRules_EmitsWRN999ForDisabledRules()
        {
            // Verify that --run-only-rules emits WRN999 for each enabled-by-default rule
            // that gets disabled, matching config-file behavior.
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(Path.GetTempPath(), $"WRN999_{Guid.NewGuid()}.sarif");
            try
            {
                var options = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { GetThisTestAssemblyFilePath() },
                    OutputFilePath = fileName,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    RunOnlyRules = new[] { "BA4001" },
                    DisableTelemetry = true,
                };

                var command = new MultithreadedAnalyzeCommand();
                command.Run(options);

                var log = SarifLog.Load(fileName);
                IList<Notification> notifications = log.Runs[0].Invocations[0].ToolConfigurationNotifications;

                notifications.Should().NotBeNullOrEmpty(
                    "WRN999 should be emitted for rules disabled by --run-only-rules");

                notifications.Should().Contain(n =>
                    n.Message.Text.Contains("BA2016") &&
                    n.Message.Text.Contains("explicitly disabled"),
                    "BA2016 is enabled by default and should trigger WRN999 when disabled");

                // BA2029 is disabled by default — should NOT have a WRN999
                notifications.Should().NotContain(n =>
                    n.Message.Text.Contains("BA2029") &&
                    n.Message.Text.Contains("explicitly disabled"),
                    "BA2029 is disabled by default and should not trigger WRN999");
            }
            finally
            {
                File.Delete(fileName);
            }
        }

        [Fact]
        public void DefaultRun_ProducesMultipleRuleResults()
        {
            // Regression test: without new args, analysis should produce results from many rules.
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(Path.GetTempPath(), $"DefaultRun_{Guid.NewGuid()}.sarif");
            try
            {
                var options = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { GetThisTestAssemblyFilePath() },
                    OutputFilePath = fileName,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    DisableTelemetry = true,
                    Kind = new[] { ResultKind.Fail, ResultKind.Pass, ResultKind.NotApplicable },
                    Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note },
                };

                var command = new MultithreadedAnalyzeCommand();
                int exitCode = command.Run(options);

                var log = SarifLog.Load(fileName);
                var ruleIds = log.Runs[0].Results.Select(r => r.RuleId).Distinct().ToList();

                ruleIds.Count.Should().BeGreaterThan(1,
                    "a default run (no --run-only-rules) should produce results from multiple rules");
            }
            finally
            {
                File.Delete(fileName);
            }
        }

        [Fact]
        public void DefaultRun_And_RunOnlyRules_ProduceSameResultForTargetedRule()
        {
            // The key regression test: for a specific rule, its result should be
            // identical whether it runs alone via --run-only-rules or as part of
            // a full default analysis.
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string defaultFile = Path.Combine(Path.GetTempPath(), $"Default_{Guid.NewGuid()}.sarif");
            string runOnlyFile = Path.Combine(Path.GetTempPath(), $"RunOnly_{Guid.NewGuid()}.sarif");
            string target = GetThisTestAssemblyFilePath();

            try
            {
                // Run 1: default (all rules)
                var defaultOptions = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { target },
                    OutputFilePath = defaultFile,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    DisableTelemetry = true,
                    Kind = new[] { ResultKind.Fail, ResultKind.Pass, ResultKind.NotApplicable },
                    Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note },
                };
                new MultithreadedAnalyzeCommand().Run(defaultOptions);

                // Run 2: only BA2016
                var runOnlyOptions = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { target },
                    OutputFilePath = runOnlyFile,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    RunOnlyRules = new[] { "BA2016" },
                    DisableTelemetry = true,
                    Kind = new[] { ResultKind.Fail, ResultKind.Pass, ResultKind.NotApplicable },
                    Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note },
                };
                new MultithreadedAnalyzeCommand().Run(runOnlyOptions);

                var defaultLog = SarifLog.Load(defaultFile);
                var runOnlyLog = SarifLog.Load(runOnlyFile);

                var defaultBA2016 = defaultLog.Runs[0].Results
                    .Where(r => r.RuleId == "BA2016").ToList();
                var runOnlyBA2016 = runOnlyLog.Runs[0].Results
                    .Where(r => r.RuleId == "BA2016").ToList();

                runOnlyBA2016.Should().HaveCount(defaultBA2016.Count,
                    "BA2016 should produce the same number of results whether run alone or with all rules");

                if (defaultBA2016.Count > 0)
                {
                    runOnlyBA2016[0].Kind.Should().Be(defaultBA2016[0].Kind,
                        "BA2016 result kind should be identical");
                }
            }
            finally
            {
                File.Delete(defaultFile);
                File.Delete(runOnlyFile);
            }
        }

        [Fact]
        public void EnableDisabledRules_EnablesRuleThatWouldNotRunByDefault()
        {
            // BA2029 (EnableIntegrityCheck) is disabled by default. It should produce
            // no results in a default run, but should produce results with --enable-disabled-rules.
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string defaultFile = Path.Combine(Path.GetTempPath(), $"Default_BA2029_{Guid.NewGuid()}.sarif");
            string enabledFile = Path.Combine(Path.GetTempPath(), $"Enabled_BA2029_{Guid.NewGuid()}.sarif");
            string target = GetThisTestAssemblyFilePath();

            try
            {
                // Run 1: default — BA2029 should be disabled
                var defaultOptions = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { target },
                    OutputFilePath = defaultFile,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    DisableTelemetry = true,
                    Kind = new[] { ResultKind.Fail, ResultKind.Pass, ResultKind.NotApplicable },
                    Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note },
                };
                new MultithreadedAnalyzeCommand().Run(defaultOptions);

                // Run 2: enable BA2029
                var enabledOptions = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { target },
                    OutputFilePath = enabledFile,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    EnableRules = new[] { "BA2029" },
                    DisableTelemetry = true,
                    Kind = new[] { ResultKind.Fail, ResultKind.Pass, ResultKind.NotApplicable },
                    Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note },
                };
                new MultithreadedAnalyzeCommand().Run(enabledOptions);

                var defaultLog = SarifLog.Load(defaultFile);
                var enabledLog = SarifLog.Load(enabledFile);

                var defaultBA2029 = defaultLog.Runs[0].Results
                    .Where(r => r.RuleId == "BA2029").ToList();
                var enabledBA2029 = enabledLog.Runs[0].Results
                    .Where(r => r.RuleId == "BA2029").ToList();

                defaultBA2029.Should().BeEmpty(
                    "BA2029 is disabled by default and should not produce results");
                enabledBA2029.Should().NotBeEmpty(
                    "BA2029 should produce results when enabled via --enable-disabled-rules");
            }
            finally
            {
                File.Delete(defaultFile);
                File.Delete(enabledFile);
            }
        }

        [Fact]
        public void BothOptionsSpecified_FailsWithNonZeroExitCode()
        {
            string fileName = Path.Combine(Path.GetTempPath(), $"BothOptions_{Guid.NewGuid()}.sarif");
            try
            {
                var options = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { GetThisTestAssemblyFilePath() },
                    OutputFilePath = fileName,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    RunOnlyRules = new[] { "BA4001" },
                    EnableRules = new[] { "BA2029" },
                    DisableTelemetry = true,
                };

                var command = new MultithreadedAnalyzeCommand();
                int exitCode = command.Run(options);

                exitCode.Should().NotBe(0,
                    "specifying both --run-only-rules and --enable-disabled-rules should fail");
            }
            finally
            {
                File.Delete(fileName);
            }
        }

        [Fact]
        public void RunOnlyRules_WithQuiet_TelemetryOnlyScenario()
        {
            // The ASan telemetry use case: --run-only-rules BA4001 --quiet
            // Should complete successfully with no console errors.
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(Path.GetTempPath(), $"TelemetryOnly_{Guid.NewGuid()}.sarif");
            try
            {
                var options = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { GetThisTestAssemblyFilePath() },
                    OutputFilePath = fileName,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    RunOnlyRules = new[] { "BA4001" },
                    Quiet = true,
                    DisableTelemetry = true,
                };

                var command = new MultithreadedAnalyzeCommand();
                int exitCode = command.Run(options);

                exitCode.Should().Be(0, "telemetry-only run should succeed");

                var log = SarifLog.Load(fileName);
                log.Runs[0].Invocations[0].ExecutionSuccessful.Should().BeTrue();
            }
            finally
            {
                File.Delete(fileName);
            }
        }

        [Fact]
        public void RunOnlyRules_NonExistentRuleId_EmitsWarningInSarif()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(Path.GetTempPath(), $"UnknownRule_{Guid.NewGuid()}.sarif");
            try
            {
                var options = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { GetThisTestAssemblyFilePath() },
                    OutputFilePath = fileName,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    RunOnlyRules = new[] { "BA9999" },
                    DisableTelemetry = true,
                };

                var command = new MultithreadedAnalyzeCommand();
                command.Run(options);

                var log = SarifLog.Load(fileName);
                IList<Notification> notifications = log.Runs[0].Invocations[0].ToolConfigurationNotifications;

                notifications.Should().Contain(n =>
                    n.Message.Text.Contains("BA9999") &&
                    n.Message.Text.Contains("does not match"),
                    "should warn about non-existent rule ID");
            }
            finally
            {
                File.Delete(fileName);
            }
        }

        private static string GetThisTestAssemblyFilePath()
        {
            return typeof(RuleSelectionFunctionalTests).Assembly.Location;
        }
    }
}
