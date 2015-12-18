// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using CommandLine;

using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    [Verb("analyze", HelpText = "Analyze one or more binary files for security and correctness issues.")]
    internal class AnalyzeOptions : AnalyzeOptionsBase
    {
        [Option(
            "sympath",
            HelpText = "Symbols path value, e.g., SRV*http://msdl.microsoft.com/download/symbols or Cache*d:\\symbols;Srv**http://symweb")]
        public string SymbolsPath { get; internal set; }
    }
}