// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class CompilerDataHashTests
    {
        [Fact]
        public void CompilerDataLogger_PopulatesFileHash_WhenHashesEnabled_PE()
        {
            if (!BinaryParsers.PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string sarifFile = Path.Combine(Path.GetTempPath(), $"HashTest_PE_{Guid.NewGuid()}.sarif");
            string csvFile = Path.Combine(Path.GetTempPath(), $"HashTest_PE_{Guid.NewGuid()}.csv");
            string configFile = Path.Combine(Path.GetTempPath(), $"HashTest_PE_{Guid.NewGuid()}.xml");

            try
            {
                string configContent = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    $"<Properties><Properties Key=\"CompilerTelemetry.Options\">" +
                    $"<Property Key=\"CsvOutputPath\" Value=\"{csvFile}\"/>" +
                    $"</Properties></Properties>";
                File.WriteAllText(configFile, configContent);

                string pathToTestFile = Path.Combine(TestData, "PE", "Native_x64_VS2019_CPlusPlus_DEBUG_DEFAULT.dll");

                var options = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { pathToTestFile },
                    OutputFilePath = sarifFile,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    DataToInsert = new[] { OptionallyEmittedData.Hashes },
                    ConfigurationFilePath = configFile,
                };

                var command = new MultithreadedAnalyzeCommand();
                command.Run(options);

                File.Exists(csvFile).Should().BeTrue("CSV telemetry file should be created");

                string[] lines = File.ReadAllLines(csvFile);
                lines.Length.Should().BeGreaterThan(1, "CSV should have header + at least one data row");

                for (int i = 1; i < lines.Length; i++)
                {
                    string[] columns = lines[i].Split(',');
                    string hashValue = columns.Length >= 2 ? columns[^2] : string.Empty;
                    if (hashValue.Length == 64)
                    {
                        // At least one row has a valid SHA-256 hash — fix confirmed.
                        return;
                    }
                }

                lines.Length.Should().Be(0,
                    "At least one CSV row should contain a 64-char SHA-256 hash");
            }
            finally
            {
                File.Delete(sarifFile);
                File.Delete(csvFile);
                File.Delete(configFile);
            }
        }

        [Fact]
        public void CompilerDataLogger_PopulatesFileHash_WithoutHashesFlag_PE()
        {
            if (!BinaryParsers.PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string sarifFile = Path.Combine(Path.GetTempPath(), $"HashTest_NoFlag_{Guid.NewGuid()}.sarif");
            string csvFile = Path.Combine(Path.GetTempPath(), $"HashTest_NoFlag_{Guid.NewGuid()}.csv");
            string configFile = Path.Combine(Path.GetTempPath(), $"HashTest_NoFlag_{Guid.NewGuid()}.xml");

            try
            {
                string configContent = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    $"<Properties><Properties Key=\"CompilerTelemetry.Options\">" +
                    $"<Property Key=\"CsvOutputPath\" Value=\"{csvFile}\"/>" +
                    $"</Properties></Properties>";
                File.WriteAllText(configFile, configContent);

                string pathToTestFile = Path.Combine(TestData, "PE", "Native_x64_VS2019_CPlusPlus_DEBUG_DEFAULT.dll");

                var options = new AnalyzeOptions
                {
                    TargetFileSpecifiers = new[] { pathToTestFile },
                    OutputFilePath = sarifFile,
                    OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                    // NOT including Hashes - hash should still be populated from CompilerData.FileHash
                    ConfigurationFilePath = configFile,
                };

                var command = new MultithreadedAnalyzeCommand();
                command.Run(options);

                File.Exists(csvFile).Should().BeTrue("CSV telemetry file should be created");

                string[] lines = File.ReadAllLines(csvFile);
                lines.Length.Should().BeGreaterThan(1, "CSV should have header + at least one data row");

                for (int i = 1; i < lines.Length; i++)
                {
                    string[] columns = lines[i].Split(',');
                    string hashValue = columns.Length >= 2 ? columns[^2] : string.Empty;
                    if (hashValue.Length == 64)
                    {
                        return;
                    }
                }

                lines.Length.Should().Be(0,
                    "At least one CSV row should contain a 64-char SHA-256 hash");
            }
            finally
            {
                File.Delete(sarifFile);
                File.Delete(csvFile);
                File.Delete(configFile);
            }
        }
    }
}
