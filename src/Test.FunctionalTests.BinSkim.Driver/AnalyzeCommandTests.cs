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
        public void AnalyzeCommand_ReadSarifLog_ShouldBeAbleToReadCurrent()
        {
            string sarifLogPath = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, "Expected", "Binskim.linux-x64.dll.sarif");

            SarifLog sarifLog = ReadSarifLog(new AnalyzeOptions
            {
                SarifOutputVersion = BinSkimSarifVersion.Current,
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

        private static SarifLog ReadSarifLog(AnalyzeOptions analyzeOptions)
        {
            return SarifLog.Load(analyzeOptions.OutputFilePath);
        }
    }
}
