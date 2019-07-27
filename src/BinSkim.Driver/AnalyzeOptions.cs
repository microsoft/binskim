// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using CommandLine;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL
{
    [Verb("analyze", HelpText = "Analyze one or more binary files for security and correctness issues.")]
    internal class AnalyzeOptions : AnalyzeOptionsBase
    {
        [Option(
            "sympath",
            HelpText = "Symbols path value, e.g., SRV*http://msdl.microsoft.com/download/symbols or Cache*d:\\symbols;Srv*http://symweb. " +
                       "See https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/advanced-symsrv-use for syntax information. " + 
                        "Note that BinSkim will clear the _NT_SYMBOL_PATH environment variable at runtime. Use this argument for symbol " +
                        "information instead.")]
        public string SymbolsPath { get; internal set; }
    }
}