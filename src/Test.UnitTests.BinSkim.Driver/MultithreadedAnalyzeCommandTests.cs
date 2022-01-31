// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class MultithreadedAnalyzeCommandTests
    {
        [Fact]
        public void MultithreadedAnalyzeCommand_ReturnCommonPathRootFromTargetSpecifiersIfOneExists()
        {
            var testCases = new[]
            {
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\*.dll",
                        @"D:\*.dll"
                    },
                    ExpectedCommonPath = string.Empty
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\",
                        @"C:\path2\"
                    },
                    ExpectedCommonPath = string.Empty
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\",
                        @"C:\path1\path2\"
                    },
                    ExpectedCommonPath = string.Empty
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\*.dll",
                        @"C:\path1\*.exe"
                    },
                    ExpectedCommonPath = @"C:\path1\"
                },
            };

            var sb = new StringBuilder();
            foreach (var testCase in testCases)
            {
                string commonPath = MultithreadedAnalyzeCommand.ReturnCommonPathRootFromTargetSpecifiersIfOneExists(testCase.TargetFileSpecifiers);
                if (commonPath != testCase.ExpectedCommonPath)
                {
                    sb.AppendLine($"The test was expecting '{testCase.ExpectedCommonPath}' but found '{commonPath}'.");
                }
            }

            sb.Length.Should().Be(0, sb.ToString());
        }
    }
}
