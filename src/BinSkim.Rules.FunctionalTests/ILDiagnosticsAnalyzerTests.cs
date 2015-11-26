// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using FluentAssertions;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.IL
{
    public class ILDiagnosticsAnalyzerTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ILDiagnosticsAnalyzerTests(ITestOutputHelper output)
        {
            _testOutputHelper = output;
        }

        private void Verify(string assemblyPath, string analyzerPath, IEnumerable<string> expectedMessages)
        {
            var actualMessages = new List<string>();
            var analyzer = new ILDiagnosticsAnalyzer();
            analyzer.LoadAnalyzer(this.GetType().Assembly.Location);
            analyzer.Analyze(assemblyPath, (d) => actualMessages.Add(d.GetMessage()));

            try
            {
                actualMessages.ShouldBeEquivalentTo(expectedMessages);
            }
            catch
            {
                _testOutputHelper.WriteLine("Expected messages:");
                foreach (string message in expectedMessages) { _testOutputHelper.WriteLine("\"" + message + "\","); }

                _testOutputHelper.WriteLine("Actual messages:");
                foreach (string message in actualMessages) { _testOutputHelper.WriteLine("\"" + message + "\","); }

                throw;
            }
        }

        [Fact]
        public void RunSymbolNameReportingAnalyzer()
        {
            var expected = new List<string>(new string[] {
                "Symbol encountered in MSIL '<Module>'",
                "Symbol encountered in MSIL '<>c__DisplayClass11_0'",
                "Symbol encountered in MSIL '<>c__DisplayClass12_0'",
                "Symbol encountered in MSIL 'AnalyzeCommandTests'",
                "Symbol encountered in MSIL 'ExceptionCondition'",
                "Symbol encountered in MSIL 'ExceptionRaisingRule'",
                "Symbol encountered in MSIL 'RuleTests'",
                "Symbol encountered in MSIL 'SarifHelpers'",
                "Symbol encountered in MSIL 'TestMessageLogger'",
                "Symbol encountered in MSIL '<>c'",
                "Symbol encountered in MSIL 'TestRoslynAnalyzer'",
                "Symbol encountered in MSIL '<>c__DisplayClass2_0'",
                "Symbol encountered in MSIL 'ILDiagnosticsAnalyzerTests'",
                "Symbol encountered in MSIL '<>c__DisplayClass0_0'",
                "Symbol encountered in MSIL 'RoslynAnalysisContextTests'",
                "Symbol encountered in MSIL '<>c__DisplayClass0_0'",
                "Symbol encountered in MSIL 'RoslynCompilationStartAnalysisContextTests'",
            });
            string testAssemblyPath = this.GetType().Assembly.Location;
            Verify(testAssemblyPath, testAssemblyPath, expected);
        }
    }
}
