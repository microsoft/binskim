// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using CommandLine;

using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL
{
    [Verb("analyze", HelpText = "Analyze one or more binary files for security and correctness issues.")]
    public class AnalyzeOptions : AnalyzeOptionsBase
    {
        private IEnumerable<string> trace = Array.Empty<string>();
        [Option(
            "trace",
            Separator = ';',
            Default = new string[] { },
            HelpText = "Execution traces, expressed as a semicolon-delimited list enclosed in double quotes, " +
                       "that should be emitted to the console and log file (if appropriate). " +
                       "Valid values: PdbLoad, ScanTime, RuleScanTime, PeakWorkingSet, TargetsScanned, ResultsSummary.")]
        public new IEnumerable<string> Trace
        {
            get => this.trace;
            set
            {
                this.trace = value;
                base.Trace = value?.Where(s => s != nameof(IL.Traces.PdbLoad));
            }
        }

        [Option(
            "sympath",
            HelpText = "Symbols path value, e.g., Cache*c:\\symbols;SRV*https://msdl.microsoft.com/download/symbols " +
                       "or Cache*d:\\symbols;Srv*https://symweb. See " +
                       "https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/advanced-symsrv-use for " +
                       "syntax information. Note that BinSkim will clear the _NT_SYMBOL_PATH and _NT_ALT_SYMBOL_PATH " +
                       "environment variables at runtime. Use this argument instead for specifying the symbol path." +
                       "WARNING: Be sure to specify a local file cache in the symbol path if at all possible, in order " +
                       "to avoid the possibility of significance time-to-analyze performance degradataion.")]
        public string SymbolsPath { get; internal set; }

        [Option(
            "local-symbol-directories",
            HelpText = "A set of semicolon-delimited local directory paths that will be examined when attempting to locate PDBs.")]
        public string LocalSymbolDirectories { get; internal set; }

        [Option(
            "ignorePdbLoadError",
            HelpText = "If enabled, BinSkim won't break if we have a 'PdbLoadingException'.")]
        public bool? IgnorePdbLoadError { get; set; }

        [Option(
            "ignorePELoadErrors",
            HelpText = "If enabled, BinSkim won't break if we have a ExceptionInCanAnalyzeError")]
        public bool? IgnorePELoadError { get; set; }

        [Option(
            "disable-telemetry",
            HelpText = "If enabled, BinSkim will disable telemetry.")]
        public bool? DisableTelemetry { get; set; }

        [Option(
            's',
            "statistics",
            HelpText = "Generate timing and other statistics for analysis session.")]
        [Obsolete()]
        public bool Statistics { get; set; }

        [Option(
            'h',
            "hashes",
            HelpText = "Output MD5, SHA1, and SHA-256 hash of analysis targets when emitting SARIF reports.")]
        [Obsolete("Use --insert instead, passing 'Hashes' along with any other references to data to be inserted.")]
        public bool ComputeFileHashes { get; set; }


        // Hidden options for test normalization purposes.

        [Option("enlistment-root",
                HelpText = "BinSkim enlistment root. Used for normalizing test outputs.",
                Hidden = true)]
        public string EnlistmentRoot { get; set; }

        [Option("normalize-output-for-comparison",
                HelpText = "Normalize certain data in SARIF to support stable diff'ing across test environments.",
                Hidden = true)]
        public bool? NormalizeOutputForComparison { get; set; }
    }
}
