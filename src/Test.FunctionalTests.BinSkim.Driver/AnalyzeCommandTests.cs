// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL;
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
        public void AnalyzeCommand_ShouldThrowWithVersionOne()
        {
            var options = new AnalyzeOptions
            {
                SarifOutputVersion = Sarif.SarifVersion.OneZeroZero
            };
            var command = new MultithreadedAnalyzeCommand();
            command.UnitTestOutputVersion = Sarif.SarifVersion.OneZeroZero;

            Assert.Throws<InvalidOperationException>(() => command.Run(options));
        }

        [Fact]
        [Obsolete]
        public void AnalyzeCommand_Hashes_ShouldUpdateDataToInsert()
        {
            var options = new AnalyzeOptions
            {
                Level = new[] { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note, FailureLevel.None },
                Kind = new[] { ResultKind.Fail }
            };
            var command = new MultithreadedAnalyzeCommand();

            options.ComputeFileHashes = false;
            command.Run(options);
            options.DataToInsert.Should().BeNull();

            options.ComputeFileHashes = true;
            command.Run(options);
            options.DataToInsert.Should().Contain(OptionallyEmittedData.Hashes);
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

    }
}
