// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Gets address offset within module when it is loaded.
    /// </summary>
    /// <param name="address">Virtual address that points where something should be loaded.</param>
    public delegate ulong NormalizeAddressDelegate(ulong address);

    public static class DwarfSymbolProvider
    {
        /// <summary>
        /// Parses the compilation units.
        /// </summary>
        /// <param name="debugData">The debug data.</param>
        /// <param name="debugDataDescription">The debug data description.</param>
        /// <param name="debugStrings">The debug strings.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        internal static List<DwarfCompilationUnit> ParseCompilationUnits(ELFBinary elfBinary, byte[] debugData, byte[] debugDataDescription, byte[] debugStrings, NormalizeAddressDelegate addressNormalizer)
        {
            using DwarfMemoryReader debugDataReader = new DwarfMemoryReader(debugData);
            using DwarfMemoryReader debugDataDescriptionReader = new DwarfMemoryReader(debugDataDescription);
            using DwarfMemoryReader debugStringsReader = new DwarfMemoryReader(debugStrings);
            List<DwarfCompilationUnit> compilationUnits = new List<DwarfCompilationUnit>();

            if (!debugDataReader.IsEnd)
            {
                DwarfCompilationUnit compilationUnit = new DwarfCompilationUnit(elfBinary, debugDataReader, debugDataDescriptionReader, debugStringsReader, addressNormalizer);

                compilationUnits.Add(compilationUnit);
            }

            return compilationUnits;
        }

        /// <summary>
        /// Parses the line number programs.
        /// </summary>
        /// <param name="debugLine">The debug line.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        internal static DwarfLineNumberProgram[] ParseLineNumberPrograms(byte[] debugLine, NormalizeAddressDelegate addressNormalizer)
        {
            using DwarfMemoryReader debugLineReader = new DwarfMemoryReader(debugLine);
            List<DwarfLineNumberProgram> programs = new List<DwarfLineNumberProgram>();

            while (!debugLineReader.IsEnd)
            {
                DwarfLineNumberProgram program = new DwarfLineNumberProgram(debugLineReader, addressNormalizer);

                programs.Add(program);
            }

            return programs.ToArray();
        }

        /// <summary>
        /// Parses the common information entries.
        /// </summary>
        /// <param name="debugFrame">The debug frame.</param>
        /// <param name="ehFrame">The exception handling frames.</param>
        /// <param name="input">The input data for parsing configuration.</param>
        internal static DwarfCommonInformationEntry[] ParseCommonInformationEntries(byte[] debugFrame, byte[] ehFrame, DwarfExceptionHandlingFrameParsingInput input)
        {
            List<DwarfCommonInformationEntry> entries = new List<DwarfCommonInformationEntry>();

            using (DwarfMemoryReader debugFrameReader = new DwarfMemoryReader(debugFrame))
            {
                entries.AddRange(DwarfCommonInformationEntry.ParseAll(debugFrameReader, input.DefaultAddressSize));
            }

            using (DwarfMemoryReader ehFrameReader = new DwarfMemoryReader(ehFrame))
            {
                entries.AddRange(DwarfExceptionHandlingCommonInformationEntry.ParseAll(ehFrameReader, input));
            }

            return entries.ToArray();
        }
    }
}
