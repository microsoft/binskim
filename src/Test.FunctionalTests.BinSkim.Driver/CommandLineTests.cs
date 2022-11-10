// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommandLine;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Driver
{
    public class CommandLineTests
    {
        private const string argsStringBase = @"analyze C:\Native_x64_VS2019_No_CastGuard.exe -o C:\result.sarif -f";

        private class SarifVersionTestCase
        {
            internal string InputBinSkimSarifVersion { get; }
            internal string ArgsString { get; }
            internal Sarif.SarifVersion? ExpectedSarifVersion { get; }
            internal string ExpectedErrorParameter { get; }

            internal SarifVersionTestCase(string inputBinSkimSarifVersion, Sarif.SarifVersion? expectedSarifVersion, string expectedErrorParameter)
            {
                this.InputBinSkimSarifVersion = inputBinSkimSarifVersion;
                this.ArgsString = inputBinSkimSarifVersion == null ? argsStringBase : argsStringBase + inputBinSkimSarifVersion;
                this.ExpectedSarifVersion = expectedSarifVersion;
                this.ExpectedErrorParameter = expectedErrorParameter;
            }
        }

        [Fact]
        public void CorrectlyParseGenerateJsonIntegerAs()
        {
            var testCases = new List<SarifVersionTestCase>()
            {
                new SarifVersionTestCase(null, Sarif.SarifVersion.Current, null),
                new SarifVersionTestCase(" --sarif-output-version Current", Sarif.SarifVersion.Current, null),
                new SarifVersionTestCase(" --sarif-output-version CURRENT", Sarif.SarifVersion.Current, null),
                new SarifVersionTestCase(" -v Current", Sarif.SarifVersion.Current, null),
                new SarifVersionTestCase(" -v current", Sarif.SarifVersion.Current, null),
                new SarifVersionTestCase(" -v ThreeZeroZero", null, "v, sarif-output-version"),
                new SarifVersionTestCase(" -v OneZeroZero", Sarif.SarifVersion.OneZeroZero, null),
                new SarifVersionTestCase(" --sarif-output-version OneZeroZero", Sarif.SarifVersion.OneZeroZero, null),
            };

            var builder = new StringBuilder();

            foreach (SarifVersionTestCase testCase in testCases)
            {
                string[] args = testCase.ArgsString.Split(' ');
                bool parser = new Parser(cfg => cfg.CaseInsensitiveEnumValues = true).ParseArguments<AnalyzeOptions>(args)
                    .MapResult(
                    options =>
                    {
                        if (testCase.ExpectedSarifVersion == null
                        || testCase.ExpectedErrorParameter != null
                        || ((AnalyzeOptionsBase)options).SarifOutputVersion != testCase.ExpectedSarifVersion)
                        {
                            builder.AppendLine($"\u2022 {testCase.ArgsString}");
                        }
                        return true;
                    },
                    err =>
                    {
                        var allErrors = err.ToList();
                        if (testCase.ExpectedSarifVersion != null
                        || testCase.ExpectedErrorParameter == null
                        || allErrors.Count != 1
                        || !(allErrors[0] is BadFormatConversionError)
                        || ((NamedError)allErrors[0]).NameInfo.NameText != testCase.ExpectedErrorParameter)
                        {
                            builder.AppendLine($"\u2022 {testCase.ArgsString}");
                        }
                        return true;
                    });
            }
            builder.Length.Should().Be(0,
                $"all test cases should pass, but the following test cases failed:\n{builder}");
        }
    }
}
