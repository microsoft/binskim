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
                        @"c:\*.dll",
                        @"c:\*.dll",
                        @"C:\*.DLL"
                    },
                    ExpectedCommonPath = @"c:\"
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\",
                        @"C:\path1\path2\"
                    },
                    ExpectedCommonPath = @"C:\path1\"
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
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\",
                        @"C:\path1\1.dll",
                        @"C:\path1\2.exe",
                        @"C:\path1\path2\",
                        @"C:\path1\path2\1.exe",
                        @"C:\path1\path2\path3\",
                        @"C:\path1\path2\path3\1.sys",
                        "c:\\path1\\path2\\path3\\path4\\"
                    },
                    ExpectedCommonPath = @"C:\path1\"
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"\\PC1\path1\",
                        @"\\PC1\path1\path2\",
                    },
                    ExpectedCommonPath = @"\\PC1\path1\",
                },
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\path1\..\*.dll",
                        @"C:\*.dll",
                    },
                    ExpectedCommonPath = @"C:\",
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
