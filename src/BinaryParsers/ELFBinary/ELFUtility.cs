// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public static class ELFUtility
    {
        /// <summary>
        /// Gets all of the symbol entries from an ELF binary and returns it
        /// as an IEnumerable.
        /// </summary>
        /// <param name="elf">ELF to get the symbols from</param>
        /// <returns>List of all entries in every symbol table in the ELF binary</returns>
        public static IEnumerable<ISymbolEntry> GetAllSymbols(IELF elf)
        {
            IEnumerable<ISymbolTable> symbolTables = elf.GetSections<ISymbolTable>();
            return symbolTables.Aggregate(
                    new List<ISymbolEntry>(),
                    (agg, next) => {
                        agg.AddRange(next.Entries);
                        return agg;
                    }
                );
        }

        /// <summary>
        /// Get the compilers used to create an ELF binary.
        /// </summary>
        /// <param name="elf">ELF binary</param>
        /// <returns>List of compiler tools from the .note section</returns>
        internal static ELFCompiler[] GetELFCompilers(IELF elf)
        {
            throw new NotImplementedException();
        }
    }
}
