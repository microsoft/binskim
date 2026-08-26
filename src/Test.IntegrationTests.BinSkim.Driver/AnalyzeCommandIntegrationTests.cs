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
            _tempDir = Path.Join(Path.GetTempPath(), "BinSkimIntegration", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }

        [Fact]
        public async Task Analyze_SelfScan_ProducesValidSarif()
        {
            string targetBinary = BinSkimRunner.GetBinSkimDllPath();
            string sarifOutput = Path.Join(_tempDir, "output.sarif");

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
            string sarifOutput = Path.Join(_tempDir, "fail-output.sarif");

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
            string sarifOutput = Path.Join(_tempDir, "filtered.sarif");

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
            string nonExistentTarget = Path.Join(_tempDir, "does_not_exist.dll");
            string sarifOutput = Path.Join(_tempDir, "output.sarif");

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
            string outputPath = Path.Join(_tempDir, "rules.sarif");

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
            string outputPath = Path.Join(_tempDir, "config.json");

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
        /// Finds the repository root by searching upward for the BinSkim.sln file.
        /// </summary>
        private static string FindRepoRoot()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "src", "BinSkim.sln")))
                {
                    return dir;
                }

                dir = Path.GetDirectoryName(dir);
            }

            throw new DirectoryNotFoundException(
                "Could not locate the repository root (searched upward for src/BinSkim.sln from assembly location).");
        }

        /// <summary>
        /// Resolves a path into the BinSkim.Rules functional test data directory.
        /// These binaries are curated per-rule with known Pass/Fail outcomes.
        /// </summary>
        private static string GetFunctionalTestDataPath(params string[] relativeParts)
        {
            string repoRoot = FindRepoRoot();
            string testDataRoot = Path.Join(repoRoot, "src",
                "Test.FunctionalTests.BinSkim.Rules", "FunctionalTestData");
            string fullPath = Path.Join(new[] { testDataRoot }.Concat(relativeParts).ToArray());

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Functional test data not found at: {fullPath}");
            }

            return fullPath;
        }

        /// <summary>
        /// Resolves a path into the BinaryParsers unit test data directory.
        /// </summary>
        private static string GetBinaryParsersTestDataPath(params string[] relativeParts)
        {
            string repoRoot = FindRepoRoot();
            string testDataRoot = Path.Join(repoRoot, "src",
                "Test.UnitTests.BinaryParsers", "TestData");
            string fullPath = Path.Join(new[] { testDataRoot }.Concat(relativeParts).ToArray());

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"BinaryParsers test data not found at: {fullPath}");
            }

            return fullPath;
        }

        #region ELF Binary Analysis

        [Fact]
        public async Task Analyze_ElfBinary_ProducesValidSarif()
        {
            string elfBinary = GetFunctionalTestDataPath(
                "BA3001.EnablePositionIndependentExecutable", "Pass", "gcc.pie_executable");
            string sarifOutput = Path.Join(_tempDir, "elf-sarif.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                elfBinary,
                "-o", sarifOutput,
                "--kind", "Fail;Pass;NotApplicable",
                "--level", "Error;Warning;Note",
            });

            result.ExitCode.Should().Be(0,
                $"BinSkim should complete ELF analysis.\nStdErr: {result.StdErr}");

            SarifLog sarifLog = result.LoadSarifLog();
            sarifLog.Should().NotBeNull();
            sarifLog.Runs.Should().HaveCount(1);
            sarifLog.Runs[0].Tool.Driver.Name.Should().Be("BinSkim");
            sarifLog.Runs[0].Results.Should().NotBeEmpty(
                "ELF rules should produce results for a valid ELF executable");
        }

        [Fact]
        public async Task Analyze_ElfBinary_KnownFail_ReportsElfRule()
        {
            string failBinary = GetFunctionalTestDataPath(
                "BA3006.EnableNonExecutableStack", "Fail", "gcc.helloworld.execstack.5.o");
            string sarifOutput = Path.Combine(_tempDir, "elf-fail.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                failBinary,
                "-o", sarifOutput,
                "--run-only-rules", "BA3006",
                "--kind", "Fail",
                "--level", "Error;Warning;Note",
            });

            result.ExitCode.Should().Be(0,
                $"BinSkim should complete analysis.\nStdErr: {result.StdErr}");

            SarifLog sarifLog = result.LoadSarifLog();
            sarifLog.Should().NotBeNull();
            sarifLog.Runs[0].Results.Should().Contain(
                r => r.RuleId == "BA3006" && r.Level == FailureLevel.Error,
                "BA3006 should fire an error for an ELF binary with executable stack");
        }

        [Fact]
        public async Task Analyze_ElfBinary_KnownPass_ReportsPass()
        {
            string passBinary = GetFunctionalTestDataPath(
                "BA3001.EnablePositionIndependentExecutable", "Pass", "gcc.pie_executable");
            string sarifOutput = Path.Combine(_tempDir, "elf-pass.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                passBinary,
                "-o", sarifOutput,
                "--run-only-rules", "BA3001",
                "--kind", "Fail;Pass",
                "--level", "Error;Warning;Note",
            });

            result.ExitCode.Should().Be(0,
                $"BinSkim should complete analysis.\nStdErr: {result.StdErr}");

            SarifLog sarifLog = result.LoadSarifLog();
            sarifLog.Should().NotBeNull();
            sarifLog.Runs[0].Results.Should().Contain(
                r => r.RuleId == "BA3001" && r.Level == FailureLevel.None,
                "BA3001 should pass for a PIE executable");
        }

        #endregion

        #region Multi-target and Glob

        [Fact]
        public async Task Analyze_MultipleTargets_ScansAll()
        {
            string target1 = BinSkimRunner.GetBinSkimDllPath();
            string target2 = GetFunctionalTestDataPath(
                "BA2016.MarkImageAsNXCompatible", "Fail", "ManagedFail.dll");
            string sarifOutput = Path.Combine(_tempDir, "multi.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                target1,
                target2,
                "-o", sarifOutput,
                "--kind", "Fail;Pass;NotApplicable",
                "--level", "Error;Warning;Note",
            });

            result.ExitCode.Should().Be(0,
                $"BinSkim should handle multiple targets.\nStdErr: {result.StdErr}");

            SarifLog sarifLog = result.LoadSarifLog();
            sarifLog.Should().NotBeNull();

            // Results should reference both target files
            var targetUris = sarifLog.Runs[0].Results
                .Select(r => r.Locations?.FirstOrDefault()?.PhysicalLocation?.ArtifactLocation?.Uri?.ToString() ?? "")
                .Distinct()
                .ToList();

            targetUris.Should().HaveCountGreaterThan(1,
                "results should come from multiple target binaries");
        }

        [Fact]
        public async Task Analyze_Recurse_FindsNestedBinaries()
        {
            // Set up a temp directory tree with binaries
            string subDir = Path.Combine(_tempDir, "nested");
            Directory.CreateDirectory(subDir);

            string source = BinSkimRunner.GetBinSkimDllPath();
            string copy1 = Path.Combine(_tempDir, "top.dll");
            string copy2 = Path.Combine(subDir, "nested.dll");
            File.Copy(source, copy1);
            File.Copy(source, copy2);

            string sarifOutput = Path.Combine(_tempDir, "recurse.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                Path.Join(_tempDir, "*.dll"),
                "-o", sarifOutput,
                "--recurse", "True",
                "--kind", "Fail;Pass;NotApplicable",
                "--level", "Error;Warning;Note",
            });

            result.ExitCode.Should().Be(0,
                $"BinSkim should recurse directories.\nStdErr: {result.StdErr}");

            SarifLog sarifLog = result.LoadSarifLog();
            sarifLog.Should().NotBeNull();

            var analyzedFiles = sarifLog.Runs[0].Results
                .Select(r => r.Locations?.FirstOrDefault()?.PhysicalLocation?.ArtifactLocation?.Uri?.ToString() ?? "")
                .Distinct()
                .ToList();

            analyzedFiles.Should().HaveCountGreaterThan(1,
                "recurse should find binaries in subdirectories");
        }

        #endregion

        #region Rich Return Code

        [Fact]
        public async Task Analyze_RichReturnCode_InvalidTarget_ReturnsRuntimeConditions()
        {
            string nonExistentTarget = Path.Combine(_tempDir, "does_not_exist.dll");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                nonExistentTarget,
                "--rich-return-code",
            });

            // With rich return code, the exit code encodes RuntimeConditions flags
            result.ExitCode.Should().NotBe(0,
                "rich return code should return non-zero for invalid targets");
            result.ExitCode.Should().NotBe(1,
                "rich return code should return a RuntimeConditions bitmask, not simple 1");
        }

        #endregion

        #region Local Symbol Directories

        [Fact]
        public async Task Analyze_LocalSymbolDirectories_AcceptsOption()
        {
            string elfBinary = GetBinaryParsersTestDataPath("Dwarf", "hello-dwarf4-o2");
            string sarifOutput = Path.Join(_tempDir, "symdir.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                elfBinary,
                "-o", sarifOutput,
                "--local-symbol-directories", _tempDir,
                "--kind", "Fail;Pass;NotApplicable",
                "--level", "Error;Warning;Note",
            });

            result.ExitCode.Should().Be(0,
                $"BinSkim should accept --local-symbol-directories.\nStdErr: {result.StdErr}");
        }

        #endregion

        #region Trace Output

        [Fact]
        public async Task Analyze_TraceTargetsScanned_ProducesTraceOutput()
        {
            string targetBinary = BinSkimRunner.GetBinSkimDllPath();
            string sarifOutput = Path.Join(_tempDir, "trace.sarif");

            BinSkimRunResult result = await BinSkimRunner.RunAsync(new[]
            {
                "analyze",
                targetBinary,
                "-o", sarifOutput,
                "--trace", "TargetsScanned;ResultsSummary",
            });

            result.ExitCode.Should().Be(0,
                $"BinSkim should accept trace flags.\nStdErr: {result.StdErr}");

            string combined = result.StdOut + result.StdErr;
            combined.Should().NotBeNullOrWhiteSpace(
                "trace output should produce console output");
        }

        #endregion
    }
}
