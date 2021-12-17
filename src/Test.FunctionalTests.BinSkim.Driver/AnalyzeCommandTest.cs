// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.Sarif;

using Moq;

using Newtonsoft.Json;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Driver
{
    public class AnalyzeCommandTest
    {
        private const string ToolName = "binskim";
        private readonly string SarifLogV1 = $@"{{
  ""$schema"": ""http://json.schemastore.org/sarif-1.0.0"",
  ""version"": ""1.0.0"",
  ""runs"": [
    {{
      ""tool"": {{
        ""name"": ""{ToolName}""
      }},
      ""results"": []
    }}
  ]
}}";

        [Fact]
        public void AnalyzeCommand_ReadSarifLog_ShouldBeAbleToReadOneZeroZero()
        {
            var fileSystem = new Mock<IFileSystem>();

            byte[] byteArray = Encoding.UTF8.GetBytes(SarifLogV1);

            fileSystem
                .Setup(f => f.FileOpenRead(It.IsAny<string>()))
                .Returns(new MemoryStream(byteArray));

            SarifLog sarifLog = AnalyzeCommand.ReadSarifLog(fileSystem.Object, new AnalyzeOptions
            {
                SarifOutputVersion = Sarif.SarifVersion.OneZeroZero,
                OutputFilePath = Guid.NewGuid().ToString(),
            });
            sarifLog.Version.Should().Be(Sarif.SarifVersion.Current);
            sarifLog.Runs[0].Tool.Driver.Name.Should().Be(ToolName);
        }
    }
}
