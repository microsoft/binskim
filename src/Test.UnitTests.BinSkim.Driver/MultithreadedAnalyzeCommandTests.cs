// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class MultithreadedAnalyzeCommandTests
    {
        private static readonly Random s_random;
        private static readonly double s_randomSeed;

        static MultithreadedAnalyzeCommandTests()
        {
            s_randomSeed = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
            s_random = new Random((int)s_randomSeed);
        }

        [Fact]
        public void MultithreadedAnalyzeCommand_ReturnCommonPathRootFromTargetSpecifiersIfOneExists()
        {
            var testCases = new[]
            {
                new
                {
                    TargetFileSpecifiers = new[]
                    {
                        @"C:\*.dll"
                    },
                    ExpectedCommonPath = @"C:\"
                },
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
                    ExpectedCommonPath = @"C:\"
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
                if (!string.Equals(commonPath, testCase.ExpectedCommonPath, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"The test was expecting '{testCase.ExpectedCommonPath}' but found '{commonPath}'.");
                }

                // Testing the same string array in a random order.
                // This will guarantee that the sorting is working as expected.
                commonPath = MultithreadedAnalyzeCommand.ReturnCommonPathRootFromTargetSpecifiersIfOneExists(Shuffle(testCase.TargetFileSpecifiers));
                if (!string.Equals(commonPath, testCase.ExpectedCommonPath, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"The test was expecting '{testCase.ExpectedCommonPath}' but found '{commonPath}' when shuffled with seed '{s_randomSeed}'.");
                }
            }

            sb.Length.Should().Be(0, sb.ToString());
        }

        private static string[] Shuffle(string[] data)
        {
            for (int i = 0; i < data.Length; ++i)
            {
                int swapWith = s_random.Next(i, data.Length);
                string temp = data[i];
                data[i] = data[swapWith];
                data[swapWith] = temp;
            }

            return data;
        }
    }
}
