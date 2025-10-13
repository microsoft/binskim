// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    internal class BinaryParsersProperties : IOptionsProvider
    {
        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                ComprehensiveBinaryParsing,
                IgnorePdbLoadError,
                DisableTelemetry,
                IncludeWixBinaries,
                LocalSymbolDirectories,
                SymbolPath,
                IgnorePELoadError,
                EnlistmentRootToNormalize,
            }.ToImmutableArray();
        }

        public static PerLanguageOption<bool> ComprehensiveBinaryParsing { get; } =
            new PerLanguageOption<bool>(
                "BinaryParsers", nameof(ComprehensiveBinaryParsing), defaultValue: () => false,
                "Set this value to 'true' to aggressively fault in all binary data on scan target load. " +
                "This is useful to flush out exceptions and other issues in various binary parsers.");

        public static PerLanguageOption<bool> IgnorePdbLoadError { get; } =
            new PerLanguageOption<bool>(
                "BinaryParsers", nameof(IgnorePdbLoadError), defaultValue: () => false,
                "Set this value to 'true' to don't break if we have a 'PdbLoadingException'.");

        public static PerLanguageOption<bool> DisableTelemetry { get; } =
            new PerLanguageOption<bool>(
                "BinaryParsers", nameof(DisableTelemetry), defaultValue: () => false,
                "Set this value to 'true' to disable telemetry.");

        public static PerLanguageOption<bool> IncludeWixBinaries { get; } =
            new PerLanguageOption<bool>(
                "BinaryParsers", nameof(IncludeWixBinaries), defaultValue: () => false,
                "Set this value to 'true' to include Wix binaries in the analysis.");

        public static PerLanguageOption<string> LocalSymbolDirectories { get; } =
            new PerLanguageOption<string>(
                "BinaryParsers", nameof(LocalSymbolDirectories), defaultValue: () => string.Empty,
                "A set of semicolon-delimited local directory paths that will be examined when attempting to locate PDBs.");

        public static PerLanguageOption<string> SymbolPath { get; } =
            new PerLanguageOption<string>(
                "BinaryParsers", nameof(SymbolPath), defaultValue: () => string.Empty,
                "Symbols path value, e.g., Cache*c:\\symbols;SRV*https://msdl.microsoft.com/download/symbols " +
                "or Cache*d:\\symbols;Srv*https://symweb. See " +
                "https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/advanced-symsrv-use for " +
                "syntax information. Note that BinSkim will clear the _NT_SYMBOL_PATH and _NT_ALT_SYMBOL_PATH " +
                "environment variables at runtime. Use this argument instead for specifying the symbol path." +
                "WARNING: Be sure to specify a local file cache in the symbol path if at all possible, in order " +
                "to avoid the possibility of significance time-to-analyze performance degradataion.");

        public static PerLanguageOption<bool> IgnorePELoadError { get; } =
            new PerLanguageOption<bool>(
                "BinaryParsers", nameof(IgnorePELoadError), defaultValue: () => false,
                "Set this value to 'true' to ignore exceptions thrown in reading PE files.");

        public static PerLanguageOption<string> EnlistmentRootToNormalize { get; } =
            new PerLanguageOption<string>(
                "BinaryParsers", nameof(EnlistmentRootToNormalize), defaultValue: () => string.Empty,
                "The root of the current enlistment. Used for normalizing file paths in test scenarios.");
    }
}
