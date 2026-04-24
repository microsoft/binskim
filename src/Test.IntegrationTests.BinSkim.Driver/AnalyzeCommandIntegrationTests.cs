// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
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
    }
}
