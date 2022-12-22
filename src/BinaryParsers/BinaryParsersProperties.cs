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
                ComprehensiveBinaryParsing
            }.ToImmutableArray();
        }

        public static PerLanguageOption<bool> ComprehensiveBinaryParsing { get; } =
            new PerLanguageOption<bool>(                
                "BinaryParsers", nameof(ComprehensiveBinaryParsing), defaultValue: () => false,
                "Set this value to 'true' to aggressively fault in all binary data on scan target load. " +
                "This is useful to flush out exceptions and other issues in various binary parsers.");
    }
}
