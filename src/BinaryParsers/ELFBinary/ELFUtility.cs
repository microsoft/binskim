// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

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
                    (agg, next) =>
                    {
                        agg.AddRange(next.Entries);
                        return agg;
                    }
                );
        }

        /// <summary>
        /// Get the compilers used to create an ELF binary.
        /// </summary>
        /// <param name="elf">ELF binary</param>
        /// <returns>List of compiler tools from the .comment section</returns>
        internal static ELFCompiler[] GetELFCompilers(IELF elf)
        {
            ISection commentSection = elf.Sections.Where(s => s.Name == ".comment").FirstOrDefault();
            if (commentSection != null)
            {
                try
                {
                    string[] commentData = NullTermAsciiToStrings(commentSection.GetContents());
                    var compilers = new ELFCompiler[commentData.Length];
                    for (int i = 0; i < commentData.Length; i++)
                    {
                        compilers[i] = new ELFCompiler(commentData[i]);
                    }
                    return compilers;
                }
                // Catch cases when the .comment section is not formatted the way we expect it to be.
                catch (Exception ex) when (ex is ArgumentException || ex is ArgumentNullException)
                {
                    return new ELFCompiler[] { new ELFCompiler(string.Empty) };
                }
            }
            else
            {
                return new ELFCompiler[] { new ELFCompiler(string.Empty) };
            }
        }

        internal static void GetDebugInfo(IELF elf, out List<byte> debugStr, out List<Abbreviation> abbreviations, out List<CompilationUnit> compilationUnits)
        {
            debugStr = default;
            abbreviations = default;
            compilationUnits = default;

            ISection section = elf.Sections.FirstOrDefault(s => s.Name == ".debug_str");
            if (section == null)
            {
                return;
            }
            debugStr = section.GetContents().ToList();

            abbreviations = Abbreviation.Extract(elf);
            if (abbreviations == null)
            {
                return;
            }

            compilationUnits = CompilationUnit.Extract(elf, abbreviations);
        }

        /// <summary>
        /// Takes a byte[] array of null terminated ascii strings (how the .comments ELF section is represented)
        /// and returns an array of the strings that were contained in the data section.
        /// </summary>
        /// <param name="data">A byte array consisting of one or more null terminated ascii string.</param>
        /// <returns>Array of the strings contained in the byte array</returns>
        internal static string[] NullTermAsciiToStrings(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Length == 0)
            {
                throw new ArgumentException("Data passed to NullTermAsciiToStrings() must be a list of null terminated ascii strings, but the parameter was empty.", nameof(data));
            }

            var strings = new List<string>();
            int curr_start = 0;
            int curr_end;
            while (curr_start < data.Length)
            {
                curr_end = Array.IndexOf(data, (byte)0, curr_start);
                if (curr_end == -1)
                {
                    throw new ArgumentException("Data passed to NullTermAsciiToStrings() must be a list of null terminated ascii strings. At least one string was not null-terminated.", nameof(data));
                }
                strings.Add(System.Text.Encoding.ASCII.GetString(data, curr_start, (curr_end - curr_start)).TrimEnd((char)0));
                curr_start = curr_end + 1;
            }
            return strings.ToArray();
        }
    }
}
