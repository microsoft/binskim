// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL;
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

            SarifLog sarifLog = ReadSarifLog(fileSystem.Object, new AnalyzeOptions
            {
                SarifOutputVersion = Sarif.SarifVersion.OneZeroZero,
                OutputFilePath = Guid.NewGuid().ToString(),
            });
            sarifLog.Version.Should().Be(Sarif.SarifVersion.Current);
            sarifLog.Runs[0].Tool.Driver.Name.Should().NotBeEmpty();
            sarifLog.Runs[0].Results.Should().BeEmpty();
        }

        [Fact]
        public void AnalyzeCommand_ReadSarifLog_ShouldBeAbleToReadCurrent()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, "Expected", "Binskim.linux-x64.dll.sarif");

            SarifLog sarifLog = ReadSarifLog(fileSystem: null, new AnalyzeOptions
            {
                SarifOutputVersion = Sarif.SarifVersion.Current,
                OutputFilePath = sarifLogPath,
            });
            sarifLog.Version.Should().Be(Sarif.SarifVersion.Current);
            sarifLog.Runs[0].Tool.Driver.Name.Should().NotBeEmpty();
            sarifLog.Runs[0].Results.Should().NotBeEmpty();
        }

        [Fact]
        [Obsolete]
        public void AnalyzeCommand_Hashes_ShouldUpdateDataToInsert()
        {
            var options = new AnalyzeOptions();
            var command = new MultithreadedAnalyzeCommand();

            options.ComputeFileHashes = false;
            command.Run(options);
            options.DataToInsert.Should().BeNull();

            options.ComputeFileHashes = true;
            command.Run(options);
            options.DataToInsert.Should().Contain(OptionallyEmittedData.Hashes);
        }

        private static SarifLog ReadSarifLog(IFileSystem fileSystem, AnalyzeOptions analyzeOptions)
        {
            SarifLog sarifLog;
            if (analyzeOptions.SarifOutputVersion == Sarif.SarifVersion.Current)
            {
                sarifLog = SarifLog.Load(analyzeOptions.OutputFilePath);
            }
            else
            {
                SarifLogVersionOne actualLog = CommandBase.ReadSarifFile<SarifLogVersionOne>(fileSystem,
                                                                                             analyzeOptions.OutputFilePath,
                                                                                             SarifContractResolverVersionOne.Instance);
                var visitor = new SarifVersionOneToCurrentVisitor();
                visitor.VisitSarifLogVersionOne(actualLog);
                sarifLog = visitor.SarifLog;
            }

            return sarifLog;
        }

    }
}
