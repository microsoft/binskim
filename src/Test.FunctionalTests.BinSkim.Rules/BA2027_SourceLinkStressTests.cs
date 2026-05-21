// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    /// <summary>
    /// Stress tests for BA2027 (EnableSourceLink) rule with portable PDB binaries.
    /// These tests ensure the rule handles portable PDB SourceLink extraction correctly
    /// under GC pressure and repeated analysis, preventing the AV crash from IcM 798776046.
    /// </summary>
    public class BA2027_SourceLinkStressTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public BA2027_SourceLinkStressTests(ITestOutputHelper output = null)
        {
            this.testOutputHelper = output;
        }

        [Fact]
        public void BA2027_PortablePdbWithSourceLink_RepeatedAnalysis()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            // Regression test
            // BA2027 would crash with AccessViolationException when analyzing
            // portable PDB binaries with SourceLink under GC pressure.
            // This test repeats the analysis to simulate multi-threaded scanning.

            string testDataDir = GetFunctionalTestDataPath("BA2027.EnableSourceLink", "Pass");
            string binaryPath = Path.Combine(testDataDir, "CSharp_PortablePdb_SourceLink.dll");

            if (!File.Exists(binaryPath))
            {
                // Skip if test data not available
                return;
            }

            var rule = new EnableSourceLink();
            var context = new BinaryAnalyzerContext();
            var logger = new TestMessageLogger();
            context.Logger = logger;

            rule.Initialize(context);

            // Analyze the same binary repeatedly to stress-test the lifetime fix
            // If the original bug existed, this would likely cause AV on iteration 2-5
            // under sufficient GC pressure
            for (int iteration = 0; iteration < 10; iteration++)
            {
                context = CreateContextForTarget(logger, binaryPath);

                if (!context.IsValidAnalysisTarget)
                {
                    continue;
                }

                context.Rule = rule;

                // The analysis should not throw AccessViolationException
                // or any other unhandled exception
                rule.Analyze(context);

                // Force garbage collection to maximize the chance of triggering
                // the original lifetime bug if it still exists
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // Verify that the rule produced a PASS result (not an error)
            logger.PassTargets.Should().Contain(
                t => t.EndsWith("CSharp_PortablePdb_SourceLink.dll", StringComparison.OrdinalIgnoreCase),
                because: "BA2027 should PASS for a portable PDB with SourceLink");
        }

        [Fact]
        public void BA2027_MultiplePortablePdbBinaries_SequentialAnalysis()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            // Test analyzing multiple portable PDB binaries sequentially
            // This simulates the real-world scanning pattern where BinSkim
            // processes many files in sequence with --threads parameter

            string testDataDir = GetFunctionalTestDataPath("BA2027.EnableSourceLink", "Pass");

            if (!Directory.Exists(testDataDir))
            {
                return;
            }

            var rule = new EnableSourceLink();
            var context = new BinaryAnalyzerContext();
            var logger = new TestMessageLogger();
            context.Logger = logger;

            rule.Initialize(context);

            // Find all portable PDB binaries
            string[] binaries = Directory.GetFiles(
                testDataDir,
                "*PortablePdb*.dll",
                SearchOption.AllDirectories);

            binaries.Should().NotBeEmpty("Test data should include portable PDB binaries");

            // Analyze each binary multiple times to simulate the stress condition
            for (int iteration = 0; iteration < 3; iteration++)
            {
                foreach (string binaryPath in binaries)
                {
                    context = CreateContextForTarget(logger, binaryPath);

                    if (!context.IsValidAnalysisTarget)
                    {
                        continue;
                    }

                    context.Rule = rule;

                    // Should not throw
                    rule.Analyze(context);

                    // Aggressive GC to stress the lifetime fix
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            // Should have analyzed at least one binary successfully
            logger.PassTargets.Count.Should().BeGreaterThan(0);
        }

        private static BinaryAnalyzerContext CreateContextForTarget(
            TestMessageLogger logger,
            string targetPath)
        {
            var context = new BinaryAnalyzerContext
            {
                Logger = logger,
                Policy = null
            };

            if (targetPath != null)
            {
                context.CurrentTarget = new EnumeratedArtifact(FileSystem.Instance)
                {
                    Uri = new Uri(targetPath)
                };
            }

            return context;
        }

        private static string GetFunctionalTestDataPath(string rulePath, string result)
        {
            return Path.Combine(
                Path.GetDirectoryName(typeof(RuleTests).Assembly.Location),
                "FunctionalTestData",
                rulePath,
                result);
        }
    }
}
