// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.Sarif;

using Moq;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Driver
{
    public class AnalyzeCommandTests
    {
        [Fact]
        public void AnalyzeCommand_ReadSarifLog_ShouldBeAbleToReadOneZeroZero()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestsDataDirectory, "Expected", "Binskim.empty.v1.0.0.sarif");
            var fileSystem = new Mock<IFileSystem>();
            string content = File.ReadAllText(sarifLogPath);
            byte[] byteArray = Encoding.UTF8.GetBytes(content);

            fileSystem
                .Setup(f => f.FileOpenRead(It.IsAny<string>()))
                .Returns(new MemoryStream(byteArray));

            SarifLog sarifLog = AnalyzeCommand.ReadSarifLog(fileSystem.Object, new AnalyzeOptions
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
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestsDataDirectory, "Expected", "Binskim.linux-x64.dll.sarif");

            var fileSystem = new Mock<IFileSystem>();

            SarifLog sarifLog = AnalyzeCommand.ReadSarifLog(fileSystem: null, new AnalyzeOptions
            {
                SarifOutputVersion = Sarif.SarifVersion.Current,
                OutputFilePath = sarifLogPath,
            });
            sarifLog.Version.Should().Be(Sarif.SarifVersion.Current);
            sarifLog.Runs[0].Tool.Driver.Name.Should().NotBeEmpty();
            sarifLog.Runs[0].Results.Should().NotBeEmpty();
        }
    }
}
