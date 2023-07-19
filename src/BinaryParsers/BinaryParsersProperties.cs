// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
                IncludeWixBinaries
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

        public static PerLanguageOption<bool> IncludeWixBinaries { get; } =
            new PerLanguageOption<bool>(
                "BinaryParsers", nameof(IncludeWixBinaries), defaultValue: () => false,
                "Set this value to 'true' to include Wix binaries in the analysis.");
    }
}
