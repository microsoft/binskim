// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class RuleSelectionTests
    {
        #region ParseRuleSpecifiers

        [Fact]
        public void ParseRuleSpecifiers_EmptyInput_ReturnsEmptyDictionary()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(Array.Empty<string>());
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseRuleSpecifiers_NullInput_ReturnsEmptyDictionary()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(null);
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseRuleSpecifiers_RuleIdOnly_ReturnsNullLevel()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(new[] { "BA2016" });

            result.Should().ContainKey("BA2016");
            result["BA2016"].Should().BeNull();
        }

        [Fact]
        public void ParseRuleSpecifiers_RuleIdWithLevel_ReturnsParsedLevel()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(new[] { "BA2032:Note" });

            result.Should().ContainKey("BA2032");
            result["BA2032"].Should().Be(FailureLevel.Note);
        }

        [Fact]
        public void ParseRuleSpecifiers_MultipleRules_ReturnsAll()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(
                new[] { "BA4001", "BA2032:Note", "BA2016:Error" });

            result.Should().HaveCount(3);
            result["BA4001"].Should().BeNull();
            result["BA2032"].Should().Be(FailureLevel.Note);
            result["BA2016"].Should().Be(FailureLevel.Error);
        }

        [Fact]
        public void ParseRuleSpecifiers_CaseInsensitiveLevel_Parses()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(new[] { "BA2032:warning" });
            result["BA2032"].Should().Be(FailureLevel.Warning);
        }

        [Fact]
        public void ParseRuleSpecifiers_InvalidLevel_Throws()
        {
            Action act = () => MultithreadedAnalyzeCommand.ParseRuleSpecifiers(new[] { "BA2032:Invalid" });

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Invalid*BA2032*Error, Warning, Note*");
        }

        [Fact]
        public void ParseRuleSpecifiers_WhitespaceEntries_AreSkipped()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(new[] { "", "  ", "BA4001" });

            result.Should().HaveCount(1);
            result.Should().ContainKey("BA4001");
        }

        [Fact]
        public void ParseRuleSpecifiers_DuplicateRuleId_LastWins()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(
                new[] { "BA2032:Error", "BA2032:Note" });

            result["BA2032"].Should().Be(FailureLevel.Note);
        }

        [Fact]
        public void ParseRuleSpecifiers_CaseInsensitiveRuleId()
        {
            var result = MultithreadedAnalyzeCommand.ParseRuleSpecifiers(new[] { "ba2032:Note" });

            result.Should().ContainKey("BA2032");
        }

        [Fact]
        public void ParseRuleSpecifiers_NoneLevel_Throws()
        {
            Action act = () => MultithreadedAnalyzeCommand.ParseRuleSpecifiers(new[] { "BA2032:None" });

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Invalid*None*BA2032*");
        }

        #endregion

        #region InitializeSkimmers

        [Fact]
        public void InitializeSkimmers_RunOnlyRules_DisablesAllExceptSpecified()
        {
            var options = CreateOptions(runOnlyRules: new[] { "BA2016" });
            var command = CreateCommand(options);
            var skimmers = CreateDefaultSkimmers();
            var context = CreateContext(options);

            // Capture which rules were enabled by default before our override
            var enabledByDefault = new HashSet<string>(
                skimmers.Where(s => s.DefaultConfiguration.Enabled).Select(s => s.Id));

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            foreach (var skimmer in result)
            {
                if (skimmer.Id == "BA2016")
                {
                    skimmer.DefaultConfiguration.Enabled.Should().BeTrue(
                        $"rule {skimmer.Id} should be enabled (specified in --run-only-rules)");
                }
                else if (enabledByDefault.Contains(skimmer.Id))
                {
                    skimmer.DefaultConfiguration.Enabled.Should().BeFalse(
                        $"rule {skimmer.Id} was enabled by default and should be disabled by --run-only-rules");
                }
                else
                {
                    skimmer.DefaultConfiguration.Enabled.Should().BeFalse(
                        $"rule {skimmer.Id} was already disabled by default and should remain disabled");
                }
            }
        }

        [Fact]
        public void InitializeSkimmers_RunOnlyRules_WithLevel_OverridesLevel()
        {
            var options = CreateOptions(runOnlyRules: new[] { "BA2016:Note" });
            var command = CreateCommand(options);
            var skimmers = CreateDefaultSkimmers();
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            var ba2016 = result.First(s => s.Id == "BA2016");
            ba2016.DefaultConfiguration.Enabled.Should().BeTrue();
            ba2016.DefaultConfiguration.Level.Should().Be(FailureLevel.Note);
        }

        [Fact]
        public void InitializeSkimmers_RunOnlyRules_WithoutLevel_PreservesDefaultLevel()
        {
            var options = CreateOptions(runOnlyRules: new[] { "BA2016" });
            var command = CreateCommand(options);
            var skimmers = CreateDefaultSkimmers();
            var context = CreateContext(options);

            var ba2016Before = skimmers.First(s => s.Id == "BA2016");
            var originalLevel = ba2016Before.DefaultConfiguration.Level;

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            var ba2016After = result.First(s => s.Id == "BA2016");
            ba2016After.DefaultConfiguration.Level.Should().Be(originalLevel);
        }

        [Fact]
        public void InitializeSkimmers_EnableDisabledRules_EnablesSpecifiedRule()
        {
            var options = CreateOptions(enableRules: new[] { "BA2029" });
            var command = CreateCommand(options);
            var skimmers = CreateDefaultSkimmers();
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            var ba2029 = result.First(s => s.Id == "BA2029");
            ba2029.DefaultConfiguration.Enabled.Should().BeTrue();

            // Other rules should remain unchanged
            var ba2016 = result.First(s => s.Id == "BA2016");
            ba2016.DefaultConfiguration.Enabled.Should().BeTrue();
        }

        [Fact]
        public void InitializeSkimmers_EnableDisabledRules_WithLevel_OverridesLevel()
        {
            var options = CreateOptions(enableRules: new[] { "BA2029:Note" });
            var command = CreateCommand(options);
            var skimmers = CreateDefaultSkimmers();
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            var ba2029 = result.First(s => s.Id == "BA2029");
            ba2029.DefaultConfiguration.Enabled.Should().BeTrue();
            ba2029.DefaultConfiguration.Level.Should().Be(FailureLevel.Note);
        }

        [Fact]
        public void InitializeSkimmers_NoOptions_ReturnsSkimmersUnchanged()
        {
            var options = CreateOptions();
            var command = CreateCommand(options);
            var skimmers = CreateDefaultSkimmers();
            var context = CreateContext(options);

            var enabledBefore = skimmers.ToDictionary(s => s.Id, s => s.DefaultConfiguration.Enabled);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            foreach (var skimmer in result)
            {
                skimmer.DefaultConfiguration.Enabled.Should().Be(enabledBefore[skimmer.Id],
                    $"rule {skimmer.Id} should not be changed");
            }
        }

        [Fact]
        public void InitializeSkimmers_BothOptions_ThrowsInvalidOperation()
        {
            var options = CreateOptions(
                enableRules: new[] { "BA2029" },
                runOnlyRules: new[] { "BA4001" });
            var command = CreateCommand(options);
            var skimmers = CreateDefaultSkimmers();
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);

            Action act = () => command.TestInitializeSkimmers(skimmers, context);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Cannot specify both*");
        }

        #endregion

        #region Regression: default behavior unchanged

        [Fact]
        public void InitializeSkimmers_DefaultOptions_PreservesAllEnabledStates()
        {
            // Verifies that when no new arguments are used, every rule's
            // Enabled and Level remain identical to the baseline (no args).
            var skimmers = CreateDefaultSkimmers();

            // Capture baseline enabled/level before InitializeSkimmers
            var baseline = skimmers.ToDictionary(
                s => s.Id,
                s => (Enabled: s.DefaultConfiguration.Enabled, Level: s.DefaultConfiguration.Level));

            var options = CreateOptions();
            var command = CreateCommand(options);
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            foreach (var skimmer in result)
            {
                var expected = baseline[skimmer.Id];
                skimmer.DefaultConfiguration.Enabled.Should().Be(expected.Enabled,
                    $"rule {skimmer.Id} enabled state should not change with default options");
                skimmer.DefaultConfiguration.Level.Should().Be(expected.Level,
                    $"rule {skimmer.Id} level should not change with default options");
            }
        }

        [Fact]
        public void InitializeSkimmers_DefaultOptions_PreservesSkimmerCount()
        {
            // Verifies no rules are added or removed.
            var skimmers = CreateDefaultSkimmers();
            int countBefore = skimmers.Count;

            var options = CreateOptions();
            var command = CreateCommand(options);
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            result.Should().HaveCount(countBefore);
        }

        [Fact]
        public void InitializeSkimmers_EnableDisabledRules_DoesNotAffectOtherRules()
        {
            // Verifies that --enable-disabled-rules only changes the targeted rule,
            // leaving all other rules' enabled state and level untouched.
            var skimmers = CreateDefaultSkimmers();
            var baseline = skimmers.ToDictionary(
                s => s.Id,
                s => (Enabled: s.DefaultConfiguration.Enabled, Level: s.DefaultConfiguration.Level));

            var options = CreateOptions(enableRules: new[] { "BA2029:Note" });
            var command = CreateCommand(options);
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            foreach (var skimmer in result)
            {
                if (skimmer.Id == "BA2029")
                {
                    continue; // This one should change; tested elsewhere.
                }

                var expected = baseline[skimmer.Id];
                skimmer.DefaultConfiguration.Enabled.Should().Be(expected.Enabled,
                    $"rule {skimmer.Id} enabled state should not change");
                skimmer.DefaultConfiguration.Level.Should().Be(expected.Level,
                    $"rule {skimmer.Id} level should not change");
            }
        }

        [Fact]
        public void InitializeSkimmers_RunOnlyRules_MultipleRules_PreservesDefaultLevels()
        {
            // Verifies that --run-only-rules without level overrides preserves
            // the original default levels on the enabled rules.
            var skimmers = CreateDefaultSkimmers();
            var baseline = skimmers.ToDictionary(
                s => s.Id,
                s => s.DefaultConfiguration.Level);

            var rulesToEnable = new[] { "BA2016", "BA2015", "BA2011" };
            var options = CreateOptions(runOnlyRules: rulesToEnable);
            var command = CreateCommand(options);
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            foreach (var ruleId in rulesToEnable)
            {
                var skimmer = result.FirstOrDefault(s => s.Id == ruleId);
                if (skimmer != null)
                {
                    skimmer.DefaultConfiguration.Level.Should().Be(baseline[ruleId],
                        $"rule {ruleId} level should be preserved when no override specified");
                }
            }
        }

        #endregion

        #region Config interaction and edge cases

        [Fact]
        public void InitializeSkimmers_RunOnlyRules_OverridesConfigFileSettings()
        {
            // --run-only-rules should override config-file settings: if config enabled
            // a rule but it's not in --run-only-rules, it should be disabled.
            var skimmers = CreateDefaultSkimmers();

            // Simulate a config that enables BA2029 (normally disabled by default)
            var ba2029 = skimmers.First(s => s.Id == "BA2029");
            ba2029.DefaultConfiguration.Enabled = true;

            var options = CreateOptions(runOnlyRules: new[] { "BA2016" });
            var command = CreateCommand(options);
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);
            var result = command.TestInitializeSkimmers(skimmers, context);

            result.First(s => s.Id == "BA2029").DefaultConfiguration.Enabled.Should().BeFalse(
                "--run-only-rules should override config-file enabled state");
            result.First(s => s.Id == "BA2016").DefaultConfiguration.Enabled.Should().BeTrue();
        }

        [Fact]
        public void InitializeSkimmers_NonExistentRuleId_DoesNotThrow()
        {
            // Non-existent rule IDs should not throw — they emit a warning instead.
            var options = CreateOptions(runOnlyRules: new[] { "BA9999" });
            var command = CreateCommand(options);
            var skimmers = CreateDefaultSkimmers();
            var context = CreateContext(options);

            command.InitializeGlobalContextFromOptions(options, ref context);

            // Should not throw
            var result = command.TestInitializeSkimmers(skimmers, context);

            // All default-enabled rules should be disabled
            result.Where(s => s.Id != "BA9999")
                  .All(s => !s.DefaultConfiguration.Enabled || !s.EnabledByDefault)
                  .Should().BeTrue();
        }

        #endregion

        #region Helpers

        private static AnalyzeOptions CreateOptions(
            string[] enableRules = null,
            string[] runOnlyRules = null)
        {
            return new AnalyzeOptions
            {
                TargetFileSpecifiers = new List<string> { "test.dll" },
                OutputFilePath = "test/output.sarif",
                DisableTelemetry = true,
                EnableRules = enableRules ?? Array.Empty<string>(),
                RunOnlyRules = runOnlyRules ?? Array.Empty<string>(),
            };
        }

        private static TestableMultithreadedAnalyzeCommand CreateCommand(AnalyzeOptions options)
        {
            return new TestableMultithreadedAnalyzeCommand(options);
        }

        private static BinaryAnalyzerContext CreateContext(AnalyzeOptions options)
        {
            return new BinaryAnalyzerContext();
        }

        private static ISet<Skimmer<BinaryAnalyzerContext>> CreateDefaultSkimmers()
        {
            var skimmers = new HashSet<Skimmer<BinaryAnalyzerContext>>();

            // Get real skimmers from the BinSkim.Rules assembly via MEF exports
            var assembly = typeof(MarkImageAsNXCompatible).Assembly;
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsAbstract &&
                    typeof(Skimmer<BinaryAnalyzerContext>).IsAssignableFrom(type))
                {
                    try
                    {
                        var skimmer = (Skimmer<BinaryAnalyzerContext>)Activator.CreateInstance(type);
                        skimmers.Add(skimmer);
                    }
                    catch
                    {
                        // Skip types that can't be instantiated
                    }
                }
            }

            skimmers.Count.Should().BeGreaterThan(5, "should find BinSkim rules");
            return skimmers;
        }

        /// <summary>
        /// Wrapper that exposes the protected InitializeSkimmers for testing.
        /// </summary>
        private class TestableMultithreadedAnalyzeCommand : MultithreadedAnalyzeCommand
        {
            public TestableMultithreadedAnalyzeCommand(AnalyzeOptions options) : base(telemetry: null)
            {
                this.currentOptions = options;
            }

            public ISet<Skimmer<BinaryAnalyzerContext>> TestInitializeSkimmers(
                ISet<Skimmer<BinaryAnalyzerContext>> skimmers,
                BinaryAnalyzerContext context)
            {
                return this.InitializeSkimmers(skimmers, context);
            }
        }

        #endregion
    }
}
