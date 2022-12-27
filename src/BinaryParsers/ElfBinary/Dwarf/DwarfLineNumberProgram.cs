// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Helper class that parses debug line data stream and returns list of file/line information.
    /// </summary>
    public class DwarfLineNumberProgram
    {
        /// <summary>
        /// The maximum operations per instruction
        /// </summary>
        private const int MaximumOperationsPerInstruction = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfLineNumberProgram"/> class.
        /// </summary>
        /// <param name="debugLine">The debug line data stream.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        internal DwarfLineNumberProgram(int dwarfVersion,
                                        DwarfMemoryReader debugLine,
                                        DwarfMemoryReader debugStrings,
                                        DwarfMemoryReader debugLineStrings,
                                        NormalizeAddressDelegate addressNormalizer)
        {
            Files = ReadData(dwarfVersion, debugLine, debugStrings, debugLineStrings, addressNormalizer);
        }

        /// <summary>
        /// Gets the list of files.
        /// </summary>
        internal List<DwarfFileInformation> Files { get; private set; }

        /// <summary>
        /// Helper class that stores current parsing state information.
        /// </summary>
        private class ParsingState
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ParsingState"/> class.
            /// </summary>
            /// <param name="defaultFile">The default file.</param>
            /// <param name="defaultIsStatement">if set to <c>true</c> defaulting to statement during reset.</param>
            /// <param name="minimumInstructionLength">Minimum length of the instruction.</param>
            public ParsingState(DwarfFileInformation defaultFile, bool defaultIsStatement, byte minimumInstructionLength)
            {
                DefaultIsStatement = defaultIsStatement;
                MinimumInstructionLength = minimumInstructionLength;
                Reset(defaultFile);
            }

            /// <summary>
            /// Gets or sets the file.
            /// </summary>
            public DwarfFileInformation File { get; set; }

            /// <summary>
            /// Gets or sets the address.
            /// </summary>
            public uint Address { get; set; }

            /// <summary>
            /// Gets or sets the index of the operation.
            /// </summary>
            public uint OperationIndex { get; set; }

            /// <summary>
            /// Gets or sets the line.
            /// </summary>
            public uint Line { get; set; }

            /// <summary>
            /// Gets or sets the column.
            /// </summary>
            public ulong Column { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether we are at statement.
            /// </summary>
            public bool IsStatement { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether we are inside the basic block.
            /// </summary>
            public bool IsBasicBlock { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether sequence has ended.
            /// </summary>
            public bool IsSequenceEnd { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether prologue has ended.
            /// </summary>
            public bool IsPrologueEnd { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether epilogue has ended.
            /// </summary>
            public bool IsEpilogueEnd { get; set; }

            /// <summary>
            /// Gets or sets the ISA.
            /// </summary>
            public ulong Isa { get; set; }

            /// <summary>
            /// Gets or sets the discriminator.
            /// </summary>
            public ulong Discriminator { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether reset is defaulting to statement.
            /// </summary>
            internal bool DefaultIsStatement { get; private set; }

            /// <summary>
            /// Gets or sets the minimum length of the instruction.
            /// </summary>
            internal byte MinimumInstructionLength { get; private set; }

            /// <summary>
            /// Resets the parse state and default to the specified file.
            /// </summary>
            /// <param name="defaultFile">The default file.</param>
            public void Reset(DwarfFileInformation defaultFile)
            {
                Address = 0;
                OperationIndex = 0;
                File = defaultFile;
                Line = 1;
                Column = 0;
                IsStatement = DefaultIsStatement;
                IsBasicBlock = false;
                IsSequenceEnd = false;
                IsPrologueEnd = false;
                IsEpilogueEnd = false;
                Isa = 0;
                Discriminator = 0;
            }

            /// <summary>
            /// Advances the address.
            /// </summary>
            /// <param name="operationAdvance">The operation advance.</param>
            public void AdvanceAddress(int operationAdvance)
            {
                int addressAdvance = MinimumInstructionLength * (((int)OperationIndex + operationAdvance) / MaximumOperationsPerInstruction);

                Address += (uint)addressAdvance;
                OperationIndex = (OperationIndex + (uint)operationAdvance) % MaximumOperationsPerInstruction;
            }

            /// <summary>
            /// Adds the current line information.
            /// </summary>
            public void AddCurrentLineInfo()
            {
                File?.Lines?.Add(new DwarfLineInformation()
                {
                    File = File,
                    Address = Address,
                    Column = Column,
                    Line = Line,
                });
            }
        }

        /// <summary>
        /// Reads the data for single instance.
        /// </summary>
        /// <param name="debugLine">The debug line data stream.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        /// <returns>List of file information.</returns>
        private static List<DwarfFileInformation> ReadData(int dwarfVersion,
                                                           DwarfMemoryReader debugLine,
                                                           DwarfMemoryReader debugStrings,
                                                           DwarfMemoryReader debugLineStrings,
                                                           NormalizeAddressDelegate addressNormalizer)
        {
            // Read header
            ulong unitLength = debugLine.ReadLength(out bool is64bit);
            if (unitLength <= 1)
            {
                // A null condition is a clue to the upstream parser that it shouldn't
                // continue, as we're in an invalid condition. We hit this particular 
                // code path parsing binaries in the wild with language MipsAssembler.
                return null;
            }

            int endPosition = debugLine.Position + (int)unitLength;

            if (endPosition > debugLine.Data.Length - 1)
            {
                return null;
            }

            ushort version = debugLine.ReadUshort();

            // DWARF versions prior to v2 did not have a .debug_line section, so
            // this is a condition that indicates a parsing issue or invalid
            // or corrupt binary.
            if (version < 2)
            {
                return null;
            }

            if (dwarfVersion > 4)
            {
                byte addressSize = debugLine.ReadByte();
                byte segmentSelectorSize = debugLine.ReadByte();
            }

            int headerLength = debugLine.ReadOffset(is64bit);
            byte minimumInstructionLength = debugLine.ReadByte();

            if (version > 3)
            {
                byte maximumOperationsPerInstruction = debugLine.ReadByte();
            }

            bool defaultIsStatement = debugLine.ReadByte() == 1;
            sbyte lineBase = (sbyte)debugLine.ReadByte();
            byte lineRange = debugLine.ReadByte();
            byte operationCodeBase = debugLine.ReadByte();

            if (operationCodeBase <= 0)
            {
                return null;
            }

            // Read operation code lengths
            ulong[] operationCodeLengths = new ulong[operationCodeBase];

            operationCodeLengths[0] = 0;
            for (int i = 1; i < operationCodeLengths.Length && debugLine.Position < endPosition; i++)
            {
                operationCodeLengths[i] = debugLine.ULEB128();
            }

            var directories = new List<string>();
            var files = new List<DwarfFileInformation>();

            if (dwarfVersion != 5)
            {
                // Read directories
                while (debugLine.Position < endPosition && debugLine.Peek() != 0)
                {
                    string directory = debugLine.ReadString();

                    directory = directory.Replace('/', Path.DirectorySeparatorChar);
                    directories.Add(directory);
                }
                debugLine.ReadByte(); // Skip zero termination byte

                // Read files
                while (debugLine.Position < endPosition && debugLine.Peek() != 0)
                {
                    files.Add(ReadFile(debugLine, directories));
                }
                debugLine.ReadByte(); // Skip zero termination byte
            }
            else
            {
                // DWARF5
                int directoryEntryFormatCount = debugLine.ReadByte();
                var defDescriptors = new DwarfLineNumberHeaderEntryDescriptor[directoryEntryFormatCount];

                // These are the directory entry format descriptions.
                for (int i = 0; i < directoryEntryFormatCount; i++)
                {
                    var contentTypeCode = (DwarfLineNumberHeaderEntryFormat)debugLine.ULEB128();
                    var attributeFormCode = (DwarfFormat)debugLine.ULEB128();
                    defDescriptors[i] =
                        new DwarfLineNumberHeaderEntryDescriptor()
                        {
                            AttributeFormat = attributeFormCode,
                            EntryFormat = contentTypeCode
                        };
                }

                ulong directoriesCount = debugLine.ULEB128();

                for (ulong i = 0; i < directoriesCount; i++)
                {
                    string path = null;

                    for (int j = 0; j < defDescriptors.Length; j++)
                    {
                        switch (defDescriptors[0].EntryFormat)
                        {
                            case DwarfLineNumberHeaderEntryFormat.Path:
                            {
                                DwarfFormat format = defDescriptors[0].AttributeFormat;
                                path = ParsePathValue(format, is64bit, debugLine, debugStrings, debugLineStrings);
                                break;
                            }
                            default:
                            {
                                break;
                            }
                        }
                    }
                    directories.Add(path);
                }

                int fileEntryFormatCount = debugLine.ReadByte();
                var fefDescriptors = new DwarfLineNumberHeaderEntryDescriptor[fileEntryFormatCount];

                // These are the file entry format descriptions.
                for (int i = 0; i < fileEntryFormatCount; i++)
                {
                    var contentTypeCode = (DwarfLineNumberHeaderEntryFormat)debugLine.ULEB128();
                    var attributeFormCode = (DwarfFormat)debugLine.ULEB128();
                    fefDescriptors[i] =
                        new DwarfLineNumberHeaderEntryDescriptor()
                        {
                            AttributeFormat = attributeFormCode,
                            EntryFormat = contentTypeCode
                        };
                }

                byte[] timestamp = Array.Empty<byte>(), md5 = Array.Empty<byte>();
                ulong size = 0;

                ulong filesCount = debugLine.ULEB128();
                for (ulong i = 0; i < filesCount; i++)
                {
                    string name = null, directory = null;
                    for (int j = 0; j < fefDescriptors.Length; j++)
                    {
                        DwarfFormat format = fefDescriptors[j].AttributeFormat;
                        switch (fefDescriptors[j].EntryFormat)
                        {
                            case DwarfLineNumberHeaderEntryFormat.Path:
                            {
                                name = ParsePathValue(format, is64bit, debugLine, debugStrings, debugLineStrings);
                                break;
                            }
                            case DwarfLineNumberHeaderEntryFormat.DirectoryIndex:
                            {
                                int index = ParseIndex(format, debugLine);
                                directory = directories[index];
                                break;
                            }
                            case DwarfLineNumberHeaderEntryFormat.Timestamp:
                            {
                                timestamp = ParseTimestamp(format, debugLine);
                                break;
                            }
                            case DwarfLineNumberHeaderEntryFormat.Size:
                            {
                                size = ParseSize(format, debugLine);
                                break;
                            }
                            case DwarfLineNumberHeaderEntryFormat.Md5:
                            {
                                md5 = debugLine.ReadBlock(16);
                                break;
                            }

                            default:
                            {
                                break;
                            }
                        }
                    }

                    string path = string.IsNullOrEmpty(directory) || Path.IsPathRooted(name) ? name : Path.Combine(directory, name);

                    files.Add(new DwarfFileInformation()
                    {
                        Name = name,
                        Directory = directory,
                        Path = path,
                        Size = size,
                        Timestamp = timestamp,
                        MD5 = md5,
                    });
                }
            }

            // Parse lines
            var state = new ParsingState(files.FirstOrDefault(), defaultIsStatement, minimumInstructionLength);
            uint lastAddress = 0;

            while (debugLine.Position < endPosition)
            {
                byte operationCode = debugLine.ReadByte();

                if (operationCode >= operationCodeLengths.Length)
                {
                    // Special operation code
                    int adjustedOperationCode = operationCode - operationCodeBase;
                    int operationAdvance = adjustedOperationCode / lineRange;
                    state.AdvanceAddress(operationAdvance);
                    int lineAdvance = lineBase + (adjustedOperationCode % lineRange);
                    state.Line += (uint)lineAdvance;
                    state.AddCurrentLineInfo();
                    state.IsBasicBlock = false;
                    state.IsPrologueEnd = false;
                    state.IsEpilogueEnd = false;
                    state.Discriminator = 0;
                }
                else
                {
                    switch ((DwarfLineNumberStandardOpcode)operationCode)
                    {
                        case DwarfLineNumberStandardOpcode.Extended:
                        {
                            ulong extendedLength = debugLine.ULEB128();
                            int newPosition = debugLine.Position + (int)extendedLength;
                            DwarfLineNumberExtendedOpcode extendedCode = DwarfLineNumberExtendedOpcode.Unknown;
                            if (debugLine.Position + 1 <= debugLine.Data.Length)
                            {
                                extendedCode = (DwarfLineNumberExtendedOpcode)debugLine.ReadByte();
                            }

                            switch (extendedCode)
                            {
                                case DwarfLineNumberExtendedOpcode.EndSequence:
                                    lastAddress = state.Address;
                                    state.IsSequenceEnd = true;
                                    state.AddCurrentLineInfo();
                                    state.Reset(files.FirstOrDefault());
                                    break;

                                case DwarfLineNumberExtendedOpcode.SetAddress:
                                {
                                    state.Address = debugLine.ReadUint();
                                    if (state.Address == 0)
                                    {
                                        state.Address = lastAddress;
                                    }
                                    state.OperationIndex = 0;
                                }
                                break;

                                case DwarfLineNumberExtendedOpcode.DefineFile:
                                    state.File = ReadFile(debugLine, directories);
                                    files.Add(state.File);
                                    break;

                                case DwarfLineNumberExtendedOpcode.SetDiscriminator:
                                    state.Discriminator = debugLine.ULEB128();
                                    break;

                                default:
                                    break;
                            }
                            debugLine.Position = newPosition;
                        }
                        break;

                        case DwarfLineNumberStandardOpcode.Copy:
                            state.AddCurrentLineInfo();
                            state.IsBasicBlock = false;
                            state.IsPrologueEnd = false;
                            state.IsEpilogueEnd = false;
                            state.Discriminator = 0;
                            break;

                        case DwarfLineNumberStandardOpcode.AdvancePc:
                            state.AdvanceAddress((int)debugLine.ULEB128());
                            break;

                        case DwarfLineNumberStandardOpcode.AdvanceLine:
                            state.Line += debugLine.SLEB128();
                            break;

                        case DwarfLineNumberStandardOpcode.SetFile:
                            int index = (int)debugLine.ULEB128() - 1;
                            if (index >= 0 && index < files.Count)
                            {
                                state.File = files[index];
                            }
                            break;

                        case DwarfLineNumberStandardOpcode.SetColumn:
                            state.Column = debugLine.ULEB128();
                            break;

                        case DwarfLineNumberStandardOpcode.NegateStmt:
                            state.IsStatement = !state.IsStatement;
                            break;

                        case DwarfLineNumberStandardOpcode.SetBasicBlock:
                            state.IsBasicBlock = true;
                            break;

                        case DwarfLineNumberStandardOpcode.ConstAddPc:
                            state.AdvanceAddress((255 - operationCodeBase) / lineRange);
                            break;

                        case DwarfLineNumberStandardOpcode.FixedAdvancePc:
                            state.Address += debugLine.ReadUshort();
                            state.OperationIndex = 0;
                            break;

                        case DwarfLineNumberStandardOpcode.SetPrologueEnd:
                            state.IsPrologueEnd = true;
                            break;

                        case DwarfLineNumberStandardOpcode.SetEpilogueBegin:
                            state.IsEpilogueEnd = true;
                            break;

                        case DwarfLineNumberStandardOpcode.SetIsa:
                            state.Isa = debugLine.ULEB128();
                            break;

                        default:
                            // Special opcodes(13 or greater)
                            break;
                    }
                }
            }

            // Fix lines in files...
            foreach (DwarfFileInformation file in files)
            {
                for (int i = 0; i < file.Lines.Count; i++)
                {
                    file.Lines[i].Address = (uint)addressNormalizer(file.Lines[i].Address);
                }
            }

            return files;
        }

        private static string ParsePathValue(DwarfFormat format, bool is64bit, DwarfMemoryReader debugLine, DwarfMemoryReader debugStrings, DwarfMemoryReader debugLineStrings)
        {
            switch (format)
            {
                case DwarfFormat.Strp:
                {
                    int offsetStrp = debugLine.ReadOffset(is64bit);
                    return debugStrings.ReadString(offsetStrp);
                }
                case DwarfFormat.LineStrp:
                {
                    int offsetStrp = debugLine.ReadOffset(is64bit);
                    return debugLineStrings.ReadString(offsetStrp);
                }
                case DwarfFormat.StrpSup:
                {
                    int offsetStrp = debugLine.ReadOffset(is64bit);
                    // NOTE: we don't support locating this value currently.
                    break;
                }

                // We aren't handling the DWO case yet.
                // See 6.2.4.1 of the DWARF5 spec.
            }
            throw new ArgumentException($"Unhandled format: {format}");
        }

        private static byte[] ParseTimestamp(DwarfFormat format, DwarfMemoryReader reader)
        {
            byte[] timestamp = null;
            switch (format)
            {
                case DwarfFormat.Data4:
                {
                    timestamp = BitConverter.GetBytes(reader.ReadUint());
                    break;
                }
                case DwarfFormat.Data8:
                {
                    timestamp = BitConverter.GetBytes(reader.ReadUlong());
                    break;
                }
                case DwarfFormat.UData:
                {
                    timestamp = BitConverter.GetBytes((ulong)reader.ULEB128());
                    break;
                }
                case DwarfFormat.Block:
                {
                    timestamp = reader.ReadBlock(reader.ULEB128());
                    break;
                }
            }

            return timestamp ?? BitConverter.GetBytes(0);
        }

        private static ulong ParseSize(DwarfFormat format, DwarfMemoryReader reader)
        {
            ulong size = 0;

            switch (format)
            {
                case DwarfFormat.Data1:
                {
                    size = reader.ReadByte();
                    break;
                }
                case DwarfFormat.Data2:
                {
                    size = reader.ReadUshort();
                    break;
                }
                case DwarfFormat.Data4:
                {
                    size = reader.ReadUint();
                    break;
                }
                case DwarfFormat.Data8:
                {
                    size = reader.ReadUlong();
                    break;
                }
                case DwarfFormat.UData:
                {
                    size = (ulong)reader.ULEB128();
                    break;
                }
            }

            return size;
        }

        private static int ParseIndex(DwarfFormat format, DwarfMemoryReader debugLine)
        {
            switch (format)
            {
                case DwarfFormat.Data1:
                {
                    return (int)debugLine.ReadByte();
                }
                case DwarfFormat.Data2:
                {
                    return (int)debugLine.ReadUshort();
                }
                case DwarfFormat.UData:
                {
                    return (int)debugLine.ULEB128();
                }
            }

            throw new ArgumentException($"Unhandled format: {format}");
        }

        /// <summary>
        /// Reads the file information from the specified stream.
        /// </summary>
        /// <param name="debugLine">The debug line data stream.</param>
        /// <param name="directories">The list of existing directories.</param>
        private static DwarfFileInformation ReadFile(DwarfMemoryReader debugLine, List<string> directories)
        {
            string name = debugLine.ReadString();
            int directoryIndex = (int)debugLine.ULEB128();
            ulong lastModified = debugLine.ULEB128();
            ulong length = debugLine.ULEB128();
            string directory = (directoryIndex > 0 && directoryIndex <= directories.Count - 1) ? directories[directoryIndex - 1] : null;
            string path;

            path = string.IsNullOrEmpty(directory) || Path.IsPathRooted(name) ? name : Path.Combine(directory, name);

            return new DwarfFileInformation()
            {
                Name = name,
                Directory = directory,
                Path = path,
                Timestamp = BitConverter.GetBytes(lastModified),
                Size = length,
            };
        }
    }
}
