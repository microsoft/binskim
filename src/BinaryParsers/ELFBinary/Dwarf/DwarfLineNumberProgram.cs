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
        internal DwarfLineNumberProgram(DwarfMemoryReader debugLine, NormalizeAddressDelegate addressNormalizer)
        {
            Files = ReadData(debugLine, addressNormalizer);
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
            public uint Column { get; set; }

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
            public uint Isa { get; set; }

            /// <summary>
            /// Gets or sets the discriminator.
            /// </summary>
            public uint Discriminator { get; set; }

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
                File.Lines.Add(new DwarfLineInformation()
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
        private static List<DwarfFileInformation> ReadData(DwarfMemoryReader debugLine, NormalizeAddressDelegate addressNormalizer)
        {
            // Read header
            int beginPosition = debugLine.Position;
            ulong length = debugLine.ReadLength(out bool is64bit);
            int endPosition = debugLine.Position + (int)length;
            ushort version = debugLine.ReadUshort();
            int headerLength = debugLine.ReadOffset(is64bit);
            byte minimumInstructionLength = debugLine.ReadByte();
            bool defaultIsStatement = debugLine.ReadByte() != 0;
            sbyte lineBase = (sbyte)debugLine.ReadByte();
            byte lineRange = debugLine.ReadByte();
            byte operationCodeBase = debugLine.ReadByte();

            // Read operation code lengths
            uint[] operationCodeLengths = new uint[operationCodeBase];

            operationCodeLengths[0] = 0;
            for (int i = 1; i < operationCodeLengths.Length && debugLine.Position < endPosition; i++)
            {
                operationCodeLengths[i] = debugLine.LEB128();
            }

            // Read directories
            List<string> directories = new List<string>();

            while (debugLine.Position < endPosition && debugLine.Peek() != 0)
            {
                string directory = debugLine.ReadString();

                directory = directory.Replace('/', Path.DirectorySeparatorChar);
                directories.Add(directory);
            }
            debugLine.ReadByte(); // Skip zero termination byte

            // Read files
            List<DwarfFileInformation> files = new List<DwarfFileInformation>();

            while (debugLine.Position < endPosition && debugLine.Peek() != 0)
            {
                files.Add(ReadFile(debugLine, directories));
            }
            debugLine.ReadByte(); // Skip zero termination byte

            // Parse lines
            ParsingState state = new ParsingState(files.FirstOrDefault(), defaultIsStatement, minimumInstructionLength);
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
                            uint extendedLength = debugLine.LEB128();
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
                                    state.Discriminator = debugLine.LEB128();
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
                            state.AdvanceAddress((int)debugLine.LEB128());
                            break;

                        case DwarfLineNumberStandardOpcode.AdvanceLine:
                            state.Line += debugLine.SLEB128();
                            break;

                        case DwarfLineNumberStandardOpcode.SetFile:
                            state.File = files[(int)debugLine.LEB128() - 1];
                            break;

                        case DwarfLineNumberStandardOpcode.SetColumn:
                            state.Column = debugLine.LEB128();
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
                            state.Isa = debugLine.LEB128();
                            break;

                        default:
                            throw new Exception($"Unsupported DwarfLineNumberStandardOpcode: {(DwarfLineNumberStandardOpcode)operationCode}");
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

        /// <summary>
        /// Reads the file information from the specified stream.
        /// </summary>
        /// <param name="debugLine">The debug line data stream.</param>
        /// <param name="directories">The list of existing directories.</param>
        private static DwarfFileInformation ReadFile(DwarfMemoryReader debugLine, List<string> directories)
        {
            string name = debugLine.ReadString();
            int directoryIndex = (int)debugLine.LEB128();
            uint lastModification = debugLine.LEB128();
            uint length = debugLine.LEB128();
            string directory = (directoryIndex > 0 && directoryIndex <= directories.Count - 1) ? directories[directoryIndex - 1] : null;
            string path = name;

            try
            {
                path = string.IsNullOrEmpty(directory) || Path.IsPathRooted(path) ? name : Path.Combine(directory, name);
            }
            catch
            {
            }

            return new DwarfFileInformation()
            {
                Name = name,
                Directory = directory,
                Path = path,
                LastModification = lastModification,
                Length = length,
            };
        }
    }
}
