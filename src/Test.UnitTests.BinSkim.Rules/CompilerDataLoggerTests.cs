// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class CompilerDataLoggerTests
    {
        [Fact]
        public void ShouldNotThrowWhenInitializedWithBadArguments()
        {
            var testCases = new[]
            {
                new
                {
                    Test = "All parameters are null",
                    Context = (IAnalysisContext)null,
                    RepositoryUri = (string)null,
                    PipelineName = (string)null,
                    TargetFileSpecifiers = (IEnumerable<string>)null,
                    ExpectedException = new ArgumentNullException("pipelineName") as Exception
                },
                new
                {
                    Test = "All parameters are null except PipelineName",
                    Context = (IAnalysisContext)null,
                    RepositoryUri = (string)null,
                    PipelineName = "pipeline-name",
                    TargetFileSpecifiers = (IEnumerable<string>)null,
                    ExpectedException = new ArgumentNullException("repositoryUri") as Exception
                },
                new
                {
                    Test = "All parameters are null except Context",
                    Context = new BinaryAnalyzerContext() as IAnalysisContext,
                    RepositoryUri = (string)null,
                    PipelineName = (string)null,
                    TargetFileSpecifiers = (IEnumerable<string>)null,
                    ExpectedException = new ArgumentNullException("pipelineName") as Exception
                },
                new
                {
                    Test = "All parameters are not null",
                    Context = new BinaryAnalyzerContext() as IAnalysisContext,
                    RepositoryUri = "repository-uri",
                    PipelineName = "pipeline-name",
                    TargetFileSpecifiers = new List<string>{ @"c:\some-path" } as IEnumerable<string>,
                    ExpectedException = (Exception)null
                },
            };

            var sb = new StringBuilder();
            foreach (var testCase in testCases)
            {
                Exception exception = Record.Exception(() => new CompilerDataLogger(testCase.Context,
                                                                                    testCase.RepositoryUri,
                                                                                    testCase.PipelineName,
                                                                                    testCase.TargetFileSpecifiers));
                if (exception != null && exception.Message != testCase.ExpectedException.Message)
                {
                    sb.AppendLine($"The test '{testCase}' was expecting a '{testCase.ExpectedException.Message}' but found '{exception.Message}'.");
                }
            }

            sb.Length.Should().Be(0, sb.ToString());
        }
    }
}
