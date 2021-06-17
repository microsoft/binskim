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
        /// <param name="dwarfBinary">Instance of a IDwarfBinary</param>
        /// <param name="debugData">The debug data.</param>
        /// <param name="debugDataDescription">The debug data description.</param>
        /// <param name="debugStrings">The debug strings.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        internal static List<DwarfCompilationUnit> ParseCompilationUnits(IDwarfBinary dwarfBinary, byte[] debugData, byte[] debugDataDescription, byte[] debugStrings, NormalizeAddressDelegate addressNormalizer)
        {
            using var debugDataReader = new DwarfMemoryReader(debugData);
            using var debugDataDescriptionReader = new DwarfMemoryReader(debugDataDescription);
            using var debugStringsReader = new DwarfMemoryReader(debugStrings);
            var compilationUnits = new List<DwarfCompilationUnit>();

            if (!debugDataReader.IsEnd)
            {
                DwarfCompilationUnit compilationUnit = new DwarfCompilationUnit(dwarfBinary, debugDataReader, debugDataDescriptionReader, debugStringsReader, addressNormalizer);

                compilationUnits.Add(compilationUnit);
            }

            return compilationUnits;
        }

        /// <summary>
        /// Parses the line number programs.
        /// </summary>
        /// <param name="debugLine">The debug line.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        internal static List<DwarfLineNumberProgram> ParseLineNumberPrograms(byte[] debugLine, NormalizeAddressDelegate addressNormalizer)
        {
            using var debugLineReader = new DwarfMemoryReader(debugLine);
            var programs = new List<DwarfLineNumberProgram>();

            while (!debugLineReader.IsEnd)
            {
                var program = new DwarfLineNumberProgram(debugLineReader, addressNormalizer);

                programs.Add(program);
            }

            return programs;
        }

        /// <summary>
        /// Parses the common information entries.
        /// </summary>
        /// <param name="debugFrame">The debug frame.</param>
        /// <param name="ehFrame">The exception handling frames.</param>
        /// <param name="input">The input data for parsing configuration.</param>
        internal static List<DwarfCommonInformationEntry> ParseCommonInformationEntries(byte[] debugFrame, byte[] ehFrame, DwarfExceptionHandlingFrameParsingInput input)
        {
            var entries = new List<DwarfCommonInformationEntry>();

            using (var debugFrameReader = new DwarfMemoryReader(debugFrame))
            {
                entries.AddRange(DwarfCommonInformationEntry.ParseAll(debugFrameReader, input.DefaultAddressSize));
            }

            using (var ehFrameReader = new DwarfMemoryReader(ehFrame))
            {
                entries.AddRange(DwarfExceptionHandlingCommonInformationEntry.ParseAll(ehFrameReader, input));
            }

            return entries;
        }
    }
}
