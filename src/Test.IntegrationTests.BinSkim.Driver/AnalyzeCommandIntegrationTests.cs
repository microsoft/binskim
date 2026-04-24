// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.IL
{
    [Trait("Category", "Integration")]
    public class AnalyzeCommandIntegrationTests : IDisposable
    {
        private readonly string _tempDir;

        public AnalyzeCommandIntegrationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BinSkimIntegration", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }

        [Fact]
        public async Task Analyze_SelfScan_ExitsWithZero()
        {
            // BinSkim analyzing its own DLL — PDB is co-located so symbol loading should succeed.
            string targetBinary = BinSkimRunner.GetBinSkimDllPath();
            string sarifOutput = Path.Combine(_tempDir, "output.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                targetBinary,
                "-o", sarifOutput,
            });

            result.ExitCode.Should().Be(0,
                $"BinSkim should exit cleanly.\nStdOut: {result.StdOut}\nStdErr: {result.StdErr}");
        }

        [Fact]
        public async Task Analyze_SelfScan_ProducesValidSarif()
        {
            string targetBinary = BinSkimRunner.GetBinSkimDllPath();
            string sarifOutput = Path.Combine(_tempDir, "output.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                targetBinary,
                "-o", sarifOutput,
            });

            File.Exists(sarifOutput).Should().BeTrue("SARIF output file should be created");

            SarifLog sarifLog = result.LoadSarifLog();
            sarifLog.Should().NotBeNull();
            sarifLog.Runs.Should().HaveCount(1);
            sarifLog.Runs[0].Tool.Driver.Name.Should().Be("BinSkim");
        }

        [Fact]
        public async Task Analyze_KnownFailBinary_ProducesErrorResults()
        {
            string failBinary = GetFunctionalTestDataPath(
                "BA2016.MarkImageAsNXCompatible", "Fail", "ManagedFail.dll");
            string sarifOutput = Path.Combine(_tempDir, "fail-output.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                failBinary,
                "-o", sarifOutput,
                "--run-only-rules", "BA2016",
                "--kind", "Fail",
                "--level", "Error;Warning;Note",
            });

            // BinSkim exits 0 even when rules fire errors — exit code reflects tool health, not rule results.
            result.ExitCode.Should().Be(0,
                $"BinSkim should complete analysis successfully.\nStdErr: {result.StdErr}");

            SarifLog sarifLog = result.LoadSarifLog();
            sarifLog.Should().NotBeNull();
            sarifLog.Runs[0].Results.Should().Contain(
                r => r.RuleId == "BA2016" && r.Level == FailureLevel.Error,
                "BA2016 should fire an error for ManagedFail.dll (not NX compatible)");
        }

        [Fact]
        public async Task Analyze_RunOnlyRules_FiltersToSpecifiedRule()
        {
            string targetBinary = BinSkimRunner.GetBinSkimDllPath();
            string sarifOutput = Path.Combine(_tempDir, "filtered.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                targetBinary,
                "-o", sarifOutput,
                "--run-only-rules", "BA2016",
                "--kind", "Fail;Pass;NotApplicable",
                "--level", "Error;Warning;Note",
            });

            result.ExitCode.Should().Be(0);

            SarifLog sarifLog = result.LoadSarifLog();
            sarifLog.Should().NotBeNull();

            // Only BA2016 results should appear (plus WRN999 notifications for disabled rules).
            sarifLog.Runs[0].Results
                .Where(r => !r.RuleId.StartsWith("WRN"))
                .Should().OnlyContain(
                    r => r.RuleId == "BA2016",
                    "only BA2016 results should be present when --run-only-rules BA2016 is specified");
        }

        [Fact]
        public async Task Analyze_NoValidTargets_ExitsWithNonZero()
        {
            string nonExistentTarget = Path.Combine(_tempDir, "does_not_exist.dll");
            string sarifOutput = Path.Combine(_tempDir, "output.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                nonExistentTarget,
                "-o", sarifOutput,
            });

            result.ExitCode.Should().NotBe(0,
                "BinSkim should report a non-zero exit code when no valid targets are found.");
        }

        [Fact]
        public async Task Analyze_InvalidArgument_ExitsWithNonZero()
        {
            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                "--bogus-flag",
            });

            result.ExitCode.Should().NotBe(0,
                "An unrecognized CLI argument should produce a non-zero exit code.");
        }

        [Fact]
        public async Task Analyze_InvalidVerb_ExitsWithNonZero()
        {
            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "not-a-real-verb",
            });

            result.ExitCode.Should().NotBe(0,
                "An unrecognized verb should produce a non-zero exit code.");
        }

        [Fact]
        public async Task Analyze_HelpFlag_ExitsCleanly()
        {
            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "help",
            });

            result.ExitCode.Should().Be(0);
            string combinedOutput = result.StdOut + result.StdErr;
            combinedOutput.Should().NotBeNullOrWhiteSpace("help output should be printed");
        }

        [Fact]
        public async Task Analyze_VersionFlag_ExitsCleanly()
        {
            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "--version",
            });

            result.ExitCode.Should().Be(0);
            string combinedOutput = result.StdOut + result.StdErr;
            combinedOutput.Should().NotBeNullOrWhiteSpace("version output should be printed");
        }

        [Fact]
        public async Task Dump_SelfScan_ProducesMetadataOutput()
        {
            string targetBinary = BinSkimRunner.GetBinSkimDllPath();

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "dump",
                targetBinary,
            });

            result.ExitCode.Should().Be(0,
                $"Dump should succeed.\nStdErr: {result.StdErr}");
            result.StdOut.Should().Contain("BinSkim.dll",
                "dump output should reference the target binary");
        }

        [Fact]
        public async Task Dump_Verbose_ProducesMoreDetailedOutput()
        {
            string targetBinary = BinSkimRunner.GetBinSkimDllPath();

            BinSkimRunResult normalResult = await BinSkimRunner.RunAsync(new[]
            {
                "dump",
                targetBinary,
            });

            BinSkimRunResult verboseResult = await BinSkimRunner.RunAsync(new[]
            {
                "dump",
                targetBinary,
                "--verbose",
            });

            normalResult.ExitCode.Should().Be(0);
            verboseResult.ExitCode.Should().Be(0);

            verboseResult.StdOut.Length.Should().BeGreaterThanOrEqualTo(normalResult.StdOut.Length,
                "verbose dump should produce output at least as detailed as non-verbose");
        }

        [Fact]
        public async Task ExportRules_ProducesValidSarifOutput()
        {
            string outputPath = Path.Combine(_tempDir, "rules.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "export-rules",
                outputPath,
            });

            result.ExitCode.Should().Be(0,
                $"export-rules should succeed.\nStdErr: {result.StdErr}");

            File.Exists(outputPath).Should().BeTrue("rules SARIF file should be created");

            string json = File.ReadAllText(outputPath);
            json.Should().Contain("BA2016",
                "exported rules should include BA2016 (MarkImageAsNXCompatible)");
        }

        [Fact]
        public async Task ExportConfig_ProducesValidJsonOutput()
        {
            string outputPath = Path.Combine(_tempDir, "config.json");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "export-config",
                outputPath,
            });

            result.ExitCode.Should().Be(0,
                $"export-config should succeed.\nStdErr: {result.StdErr}");

            File.Exists(outputPath).Should().BeTrue("config JSON file should be created");

            string json = File.ReadAllText(outputPath);
            json.Should().NotBeNullOrWhiteSpace("config file should contain content");
        }

        /// <summary>
        /// Resolves a path into the BinSkim.Rules functional test data directory.
        /// These binaries are curated per-rule with known Pass/Fail outcomes.
        /// </summary>
        private static string GetFunctionalTestDataPath(params string[] relativeParts)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string testDataRoot = Path.GetFullPath(
                Path.Combine(assemblyDir, "..", "..", "..", "..", "src",
                    "Test.FunctionalTests.BinSkim.Rules", "FunctionalTestData"));
            string fullPath = Path.Combine(new[] { testDataRoot }.Concat(relativeParts).ToArray());

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Functional test data not found at: {fullPath}");
            }

            return fullPath;
        }
    }
}
