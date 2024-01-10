// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommandLine;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Driver
{
    public class CommandLineTests
    {
        private const string argsStringBase = @"analyze C:\Native_x64_VS2019_No_CastGuard.exe -o C:\result.sarif --log ForceOverwrite";

        private class SarifVersionTestCase
        {
            internal string ArgsString { get; }
            internal string ExpectedErrorParameter { get; }

            internal SarifVersionTestCase(string expectedErrorParameter)
            {
                this.ArgsString = argsStringBase;
                this.ExpectedErrorParameter = expectedErrorParameter;
            }
        }

        [Fact]
        public void MostlyFunctionlessCommandlineTest()
        {
            var testCases = new List<SarifVersionTestCase>()
            {
                new SarifVersionTestCase(expectedErrorParameter: null),
            };

            var builder = new StringBuilder();

            foreach (SarifVersionTestCase testCase in testCases)
            {
                string[] args = testCase.ArgsString.Split(' ');
                bool parser = new Parser(cfg => cfg.CaseInsensitiveEnumValues = true).ParseArguments<AnalyzeOptions>(args)
                    .MapResult(
                    options =>
                    {
                        if (testCase.ExpectedErrorParameter != null)
                        {
                            builder.AppendLine($"\u2022 {testCase.ArgsString}");
                        }
                        return true;
                    },
                    err =>
                    {
                        var allErrors = err.ToList();
                        if (allErrors.Count != 1 ||
                            !(allErrors[0] is BadFormatConversionError) ||
                            ((NamedError)allErrors[0]).NameInfo.NameText != testCase.ExpectedErrorParameter)
                        {
                            builder.AppendLine($"\u2022 {testCase.ArgsString}");
                        }
                        return true;
                    });
            }
            builder.Length.Should().Be(0,
                $"all test cases should pass, but the following test cases failed:\n{builder}");
        }

        [Fact]
        public void DisableTelemetryCommandlineTest()
        {
            var testCases = new List<Tuple<string, bool?>>()
            {
                new Tuple<string, bool?>(@"analyze C:\Native.exe -o C:\result.sarif", null),
                new Tuple<string, bool?>(@"analyze C:\Native.exe -o C:\result.sarif --disable-telemetry true", true),
                new Tuple<string, bool?>(@"analyze C:\Native.exe -o C:\result.sarif --disable-telemetry false", false)
            };

            var builder = new StringBuilder();

            foreach (Tuple<string, bool?> testCase in testCases)
            {
                string[] args = testCase.Item1.Split(' ');
                bool parser = new Parser(cfg => cfg.CaseInsensitiveEnumValues = true).ParseArguments<AnalyzeOptions>(args)
                    .MapResult(
                    options =>
                    {
                        if (options.DisableTelemetry != testCase.Item2)
                        {
                            builder.AppendLine($"\u2022 {testCase.Item1}");
                        }
                        return true;
                    },
                    err =>
                    {
                        builder.AppendLine($"\u2022 {testCase.Item1}");
                        return true;
                    });
            }
            builder.Length.Should().Be(0,
                $"all test cases should pass, but the following test cases failed:\n{builder}");
        }
    }
}
