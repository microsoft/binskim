// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Microsoft.CodeAnalysis.Sarif.VersionOne;
using Microsoft.CodeAnalysis.Sarif.Visitors;

using Moq;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Driver
{
    public class AnalyzeCommandTests
    {
        [Fact]
        public void AnalyzeCommand_ReadSarifLog_ShouldBeAbleToReadOneZeroZero()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, "Expected", "Binskim.empty.v1.0.0.sarif");
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);

            fileSystem
                .Setup(f => f.FileOpenRead(It.IsAny<string>()))
                .Returns(new MemoryStream(byteArray));
            SarifLog sarifLog = ReadSarifLog(fileSystem: fileSystem.Object, Guid.NewGuid().ToString(), Sarif.SarifVersion.OneZeroZero);
            sarifLog.Version.Should().Be(Sarif.SarifVersion.Current);
            sarifLog.Runs[0].Tool.Driver.Name.Should().NotBeEmpty();
            sarifLog.Runs[0].Results.Should().BeEmpty();
        }

        [Fact]
        public void AnalyzeCommand_ReadSarifLog_ShouldBeAbleToReadCurrent()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, "Expected", "Binskim.linux-x64.dll.sarif");

            SarifLog sarifLog = ReadSarifLog(fileSystem: null, sarifLogPath, Sarif.SarifVersion.Current);
            sarifLog.Version.Should().Be(Sarif.SarifVersion.Current);
            sarifLog.Runs[0].Tool.Driver.Name.Should().NotBeEmpty();
            sarifLog.Runs[0].Results.Should().NotBeEmpty();
        }

        [Fact]
        public void AnalyzeCommand_ShouldThrowWithNoTargetFile()
        {
            var options = new AnalyzeOptions();
            var command = new MultithreadedAnalyzeCommand();
            Assert.Throws<ArgumentNullException>(() => command.Run(options));
        }

        [Fact]
        public void AnalyzeCommand_ShouldThrowWithVersionOne()
        {
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[] { "dummy.dll" },
                SarifOutputVersion = Sarif.SarifVersion.OneZeroZero
            };
            var command = new MultithreadedAnalyzeCommand();
            command.UnitTestOutputVersion = Sarif.SarifVersion.OneZeroZero;

            Assert.Throws<InvalidOperationException>(() => command.Run(options));
        }

        [Fact]
        public void AnalyzeCommand_ShouldNotThrowWithSupportedTrace()
        {
            IEnumerable<string> allSupportedDefaultTraces = Enum.GetValues(typeof(DefaultTraces)).Cast<DefaultTraces>()
                                                            .Where(e => e != DefaultTraces.None)
                                                            .Select(e => e.ToString());
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[] { GetThisTestAssemblyFilePath() },
                Trace = allSupportedDefaultTraces.Append(nameof(Traces.PdbLoad))
            };

            var command = new MultithreadedAnalyzeCommand
            {
                UnitTestOutputVersion = Sarif.SarifVersion.Current
            };

            BinaryAnalyzerContext context = null;
            int result = command.Run(options, ref context);
            context.Traces.Should().HaveCount(allSupportedDefaultTraces.Count());
            context.TracePdbLoads.Should().BeTrue();
            context.RuntimeExceptions.Should().BeNull();
            result.Should().Be(0);
        }

        [Fact]
        public void AnalyzeCommand_DeterminismTest()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            WindowsBinaryAndPdbSkimmerBase.s_PdbExceptions.Clear();
            string fileName = Path.Combine(Path.GetTempPath(), "AnalyzeCommand_DeterminismTest.sarif");
            string pathDeterminismTest = Path.Combine(PEBinaryTests.TestData, "PE", "Determinism", "*.dll");
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[] {
                    pathDeterminismTest
                },
                Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note, FailureLevel.None },
                Kind = new[] { ResultKind.Fail, ResultKind.Pass },
                OutputFilePath = fileName,
                OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                Recurse = true,
                Threads = 10,
                IgnorePdbLoadError = false,
                DataToInsert = new[] { OptionallyEmittedData.Hashes }
            };
            var command = new MultithreadedAnalyzeCommand();
            command.Run(options);
            var log = SarifLog.Load(fileName);
            log.Runs[0].Invocations[0].ToolConfigurationNotifications.Count.Should().Be(3);
            log.Runs[0].Invocations[0].ToolConfigurationNotifications.Count(t => t.Message.Text.Contains("E_PDB_FORMAT")).Should().Be(1);
            log.Runs[0].Invocations[0].ToolConfigurationNotifications.Count(t => t.Message.Text.Contains("E_OUTOFMEMORY")).Should().Be(1);
            log.Runs[0].Invocations[0].ToolConfigurationNotifications.Count(t => t.Message.Text.Contains("E_PDB_NOT_FOUND")).Should().Be(1);
        }

        [Fact]
        public void AnalyzeCommand_ZeroByteTest()
        {
            string fileName = Path.Combine(Path.GetTempPath(), "AnalyzeCommand_ZeroByteTest.sarif");
            string pathDeterminismTest = Path.Combine(PEBinaryTests.TestData, "Invalid", "ZeroByte", "*");
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[] {
                    pathDeterminismTest
                },
                Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note, FailureLevel.None },
                Kind = new[] { ResultKind.Fail, ResultKind.Pass },
                OutputFilePath = fileName,
                OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                Recurse = true,
                Threads = 10,
                IgnorePdbLoadError = true,
                DataToInsert = new[] { OptionallyEmittedData.Hashes }
            };
            var command = new MultithreadedAnalyzeCommand();

            command.Run(options);
            var log = SarifLog.Load(fileName);

            log.Runs[0].Invocations[0].ToolExecutionNotifications.Should().BeNull();
            log.Runs[0].Invocations[0].ToolConfigurationNotifications.Count(t => t.Message.Text.Contains("skipped")).Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void AnalyzeCommand_ComputeFileHashes_Works()
        {
            string fileName = Path.Combine(Path.GetTempPath(), "AnalyzeCommand_ComputeFileHashes_Works.sarif");
            string pathDeterminismTest = Path.Combine(PEBinaryTests.TestData, "PE", "Managed_x64_VS2022_CSharp_Net48_Default.exe");

#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS0612 // Type or member is obsolete
            var options = new AnalyzeOptions
            {
                TargetFileSpecifiers = new string[] {
                    pathDeterminismTest
                },
                OutputFilePath = fileName,
                OutputFileOptions = new[] { FilePersistenceOptions.ForceOverwrite },
                ComputeFileHashes = true,
                Statistics = true,
            };
#pragma warning restore CS0612 // Type or member is obsolete
#pragma warning restore CS0618 // Type or member is obsolete

            var command = new MultithreadedAnalyzeCommand();

            command.Run(options);
            var log = SarifLog.Load(fileName);

            log.Runs[0].Artifacts[0].Hashes.Should().HaveCount(3);
        }

        private static SarifLog ReadSarifLog(IFileSystem fileSystem, string outputFilePath, Sarif.SarifVersion readSarifVersion)
        {
            SarifLog sarifLog;
            if (readSarifVersion == Sarif.SarifVersion.Current)
            {
                sarifLog = SarifLog.Load(outputFilePath);
            }
            else
            {
                SarifLogVersionOne actualLog = CommandBase.ReadSarifFile<SarifLogVersionOne>(fileSystem,
                                                                                             outputFilePath,
                                                                                             SarifContractResolverVersionOne.Instance);
                var visitor = new SarifVersionOneToCurrentVisitor();
                visitor.VisitSarifLogVersionOne(actualLog);
                sarifLog = visitor.SarifLog;
            }

            return sarifLog;
        }

        private static string GetThisTestAssemblyFilePath()
        {
            string filePath = typeof(AnalyzeCommandTests).Assembly.Location;
            return filePath;
        }
    }
}
