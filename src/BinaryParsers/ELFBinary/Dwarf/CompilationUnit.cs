// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public class CompilationUnit
    {
        public CompilationUnitHeader CompilationUnitHeader { get; }
        public List<DebuggingInformationEntry> DebuggingInformationEntries { get; }

        public CompilationUnit(CompilationUnitHeader compilationUnitHeader, List<DebuggingInformationEntry> debuggingInformationEntries)
        {
            CompilationUnitHeader = compilationUnitHeader;
            DebuggingInformationEntries = debuggingInformationEntries;
        }

        public static CompilationUnit Parse(List<byte> infoData, ref int index, List<Abbreviation> abbrevList)
        {
            if (index >= infoData.Count)
            {
                return null;
            }

            int compilationUnitId = index;
            var compilationUnitHeader = CompilationUnitHeader.Parse(infoData, ref index, compilationUnitId);
            uint compilationUnitLength = compilationUnitHeader.Length + 4;

            // TODO: hack way to fix offset for dwarf5.
            // This should be compilationUnitHeader.AbbrevOffset
            var abbrevListFiltered = abbrevList.Where(a => a.Offset == 0).ToList();

            var dieList = new List<DebuggingInformationEntry>();
            while (index < compilationUnitId + compilationUnitLength)
            {
                var die = DebuggingInformationEntry.Parse(infoData, ref index, abbrevListFiltered, compilationUnitId);
                dieList.Add(die);
            }

            return new CompilationUnit(compilationUnitHeader, dieList);
        }

        // Read and parse for compilation units from ELF file
        public static List<CompilationUnit> Extract(IELF elfFile, List<Abbreviation> abbreviations)
        {
            var compilationUnitListFlat = new List<CompilationUnit>();
            var compilationUnitList = new List<CompilationUnit>();
            int index = 0;

            ISection section = elfFile.Sections.First(s => s.Name == ".debug_info");
            if (section == null)
            {
                return null;
            }

            var infoData = section.GetContents().ToList();

            CompilationUnit compilationUnit;
            while ((compilationUnit = Parse(infoData, ref index, abbreviations)) != null)
            {
                compilationUnitListFlat.Add(compilationUnit);
            }

            foreach (CompilationUnit c in compilationUnitListFlat)
            {
                index = 0;
                List<DebuggingInformationEntry> dieList = InflateDieListRecompilationUnitrsive(c.DebuggingInformationEntries, ref index);
                var inflatedCu = new CompilationUnit(c.CompilationUnitHeader, dieList);
                compilationUnitList.Add(inflatedCu);
            }

            return compilationUnitList;
        }

        // Group children to parent DIEs
        private static List<DebuggingInformationEntry> InflateDieListRecompilationUnitrsive(List<DebuggingInformationEntry> dieList, ref int index)
        {
            var output = new List<DebuggingInformationEntry>();
            while (index < dieList.Count)
            {
                DebuggingInformationEntry die = dieList.ElementAt(index);
                index++;
                if (die == null)
                {
                    continue;
                }

                if (die.HasChildren == DW_CHILDREN.Yes)
                {
                    List<DebuggingInformationEntry> childDieList = InflateDieListRecompilationUnitrsive(dieList, ref index);
                    die.AddDieList(childDieList);
                }
                output.Add(die);
            }
            return output;
        }

        public string GetName(List<byte> strData)
        {
            return DebuggingInformationEntries[0].GetName(strData);
        }

        public List<DebuggingInformationEntry> GetChildren()
        {
            List<DebuggingInformationEntry> output;

            // Determine if inflated our not
            if (DebuggingInformationEntries.Count == 1)
            {
                output = DebuggingInformationEntries[0].Children;
            }
            else
            {
                output = DebuggingInformationEntries;
            }

            return output;
        }
    }
}
