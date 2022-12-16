﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;

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
        /// Parses all compilation units.
        /// </summary>
        /// <param name="dwarfBinary">Instance of a IDwarfBinary</param>
        /// <param name="debugData">The debug data.</param>
        /// <param name="debugDataDescription">The debug data description.</param>
        /// <param name="debugStrings">The debug strings.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        internal static List<DwarfCompilationUnit> ParseAllCompilationUnits(IDwarfBinary dwarfBinary,
                                                                            byte[] debugData,
                                                                            byte[] debugDataDescription,
                                                                            byte[] debugStrings,
                                                                            byte[] debugLineStrings,
                                                                            byte[] debugStringOffsets,
                                                                            NormalizeAddressDelegate addressNormalizer)
        {
            int offset = 0;
            DwarfCompilationUnit compilationUnit;
            List<DwarfCompilationUnit> returnValue = new List<DwarfCompilationUnit>();

            List<int> debugStringOffsetsValues = DwarfSymbolProvider.ParseDebugStringOffsets(debugStringOffsets, dwarfBinary.Is64bit);

            while (true)
            {
                compilationUnit = ParseOneCompilationUnitByOffset(dwarfBinary,
                                                                  debugData,
                                                                  debugDataDescription,
                                                                  debugStrings,
                                                                  debugLineStrings,
                                                                  debugStringOffsetsValues,
                                                                  addressNormalizer,
                                                                  offset);

                if (compilationUnit?.Symbols.Any() != true)
                {
                    return returnValue;
                }

                returnValue.Add(compilationUnit);

                if (compilationUnit.NextOffset == offset)
                {
                    return returnValue;
                }

                offset = compilationUnit.NextOffset;
            }
        }

        /// <summary>
        /// Parses one compilation unit from the offset.
        /// </summary>
        /// <param name="dwarfBinary">Instance of a IDwarfBinary</param>
        /// <param name="debugData">The debug data.</param>
        /// <param name="debugDataDescription">The debug data description.</param>
        /// <param name="debugStrings">The debug strings.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        /// <param name="offset">The offset to start reading data.</param>
        internal static DwarfCompilationUnit ParseOneCompilationUnitByOffset(IDwarfBinary dwarfBinary,
                                                                             byte[] debugData,
                                                                             byte[] debugDataDescription,
                                                                             byte[] debugStrings,
                                                                             byte[] debugLineStrings,
                                                                             IList<int> debugStringOffsets,
                                                                             NormalizeAddressDelegate addressNormalizer,
                                                                             int offset)
        {
            using var debugDataReader = new DwarfMemoryReader(debugData);
            using var debugStringsReader = new DwarfMemoryReader(debugStrings);
            using var debugLineStringsReader = new DwarfMemoryReader(debugLineStrings);
            using var debugDataDescriptionReader = new DwarfMemoryReader(debugDataDescription);

            if (offset >= debugDataReader.Data.Length)
            {
                return null;
            }

            debugDataReader.Position = offset;
            return new DwarfCompilationUnit(dwarfBinary,
                                            debugDataReader,
                                            debugDataDescriptionReader,
                                            debugStringsReader,
                                            debugLineStringsReader,
                                            debugStringOffsets,
                                            addressNormalizer);
        }


        internal static List<int> ParseDebugStringOffsets(byte[] debugStringOffsets, bool is64bit)
        {
            using var debugStringOffsetsReader = new DwarfMemoryReader(debugStringOffsets);
            var stringOffsets = new List<int>();

            while (!debugStringOffsetsReader.IsEnd)
            {
                int offset = debugStringOffsetsReader.ReadOffset(is64bit);
                stringOffsets.Add(offset);
            }

            return stringOffsets;
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
        /// Parses all command line info.
        /// </summary>
        /// <param name="compilationUnits">the compilation units.</param>
        internal static List<DwarfCompileCommandLineInfo> ParseAllCommandLineInfos(List<DwarfCompilationUnit> compilationUnits)
        {
            var returnValue = new List<DwarfCompileCommandLineInfo>();

            foreach (DwarfCompilationUnit compilationUnit in compilationUnits)
            {
                foreach (DwarfSymbol symbol in compilationUnit.Symbols)
                {
                    var info = new DwarfCompileCommandLineInfo();

                    symbol.Attributes.TryGetValue(DwarfAttribute.Name, out DwarfAttributeValue name);
                    info.FullName = (name == null
                        || name.Value == null
                        || name.Value.ToString().All(char.IsDigit)
                        || name.Value.ToString().Equals(ElfUtility.LongUnsignedInt, StringComparison.OrdinalIgnoreCase))
                        ? string.Empty
                        : name.Value.ToString();

                    symbol.Attributes.TryGetValue(DwarfAttribute.CompDir, out DwarfAttributeValue compDir);
                    info.CompileDirectory = (compDir == null
                        || compDir.Value == null
                        || compDir.Value.ToString().All(char.IsDigit)
                        || compDir.Value.ToString().Equals(ElfUtility.LongUnsignedInt, StringComparison.OrdinalIgnoreCase))
                        ? string.Empty
                        : compDir.Value.ToString();

                    try
                    {
                        info.FileName = Path.GetFileName(info.FullName);
                    }
                    catch (ArgumentException)
                    {
                        info.FileName = string.Empty;
                    }
                    info.Language = DwarfLanguage.Unknown;
                    info.Type = symbol.Tag;

                    if (symbol.Tag == DwarfTag.CompileUnit
                        && symbol.Attributes.TryGetValue(DwarfAttribute.Producer, out DwarfAttributeValue producer)
                        && symbol.Attributes.TryGetValue(DwarfAttribute.Language, out DwarfAttributeValue language))
                    {
                        DwarfLanguage dwarfLanguage;
                        if (Enum.TryParse(language.Value?.ToString(), out dwarfLanguage))
                        {
                            info.Language = dwarfLanguage;
                        }
                        info.CommandLine = producer.Value?.ToString();

                        if (info.Language != DwarfLanguage.C89
                            && info.Language != DwarfLanguage.C
                            && info.Language != DwarfLanguage.CPlusPlus
                            && info.Language != DwarfLanguage.C99
                            && info.Language != DwarfLanguage.CPlusPlus03
                            && info.Language != DwarfLanguage.CPlusPlus11
                            && info.Language != DwarfLanguage.C11
                            && info.Language != DwarfLanguage.CPlusPlus14)
                        {
                            continue;
                        }
                    }
                    else if (symbol.Tag == DwarfTag.Subprogram
                        && symbol.Attributes.TryGetValue(DwarfAttribute.LinkageName, out DwarfAttributeValue linkageName))
                    {
                        // No Language property for Subprogram
                        info.CommandLine = linkageName.Value?.ToString();
                    }
                    else
                    {
                        continue;
                    }

                    if (info.CommandLine != null
                        && (info.CommandLine.Equals(ElfUtility.LongUnsignedInt, StringComparison.OrdinalIgnoreCase)
                        || info.CommandLine.All(char.IsDigit)))
                    {
                        info.CommandLine = string.Empty;
                    }

                    if (info.CommandLine != null)
                    {
                        returnValue.Add(info);
                    }
                }
            }

            return returnValue;
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
