// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    // A bundle of DWARF v5 directory information.
    internal class DwarfLineNumberHeaderEntryDescriptor
    {
        public DwarfFormat AttributeFormat { get; set; }

        public DwarfLineNumberHeaderEntryFormat EntryFormat { get; set; }
    }
}
