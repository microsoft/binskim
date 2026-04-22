// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Unit tests for <see cref="DwarfLineNumberProgram"/>.
    /// </summary>
    public class DwarfLineNumberProgramTests
    {
        /// <summary>
        /// Helper to encode an unsigned LEB128 value into bytes.
        /// Copied from DwarfCompilationUnitFormTests to keep tests self-contained.
        /// </summary>
        private static byte[] EncodeULEB128(ulong value)
        {
            var bytes = new List<byte>();
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0)
                {
                    b |= 0x80;
                }

                bytes.Add(b);
            } while (value != 0);
            return bytes.ToArray();
        }

        [Fact]
        public void Constructor_WithUnitLengthLessOrEqualToOne_ReturnsNullFiles()
        {
            // unit_length = 1 triggers the early-null path in ReadData.
            byte[] debugLineData = new byte[] { 0x01, 0x00, 0x00, 0x00 };

            using var debugLine = new DwarfMemoryReader(debugLineData);
            using var debugStrings = new DwarfMemoryReader(new byte[] { 0x00 });

            var program = new DwarfLineNumberProgram(
                dwarfVersion: 4,
                debugLine: debugLine,
                debugStrings: debugStrings,
                addressNormalizer: addr => addr);

            program.Files.Should().BeNull();
        }

        [Fact]
        public void ReadData_Dwarf4_SingleFileSingleCopy_ProducesOneNormalizedLine()
        {
            // Build a minimal DWARF4 .debug_line section with:
            // - one file entry
            // - a single COPY opcode to emit one line record

            var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                // version = 4
                bw.Write((ushort)4);

                // header_length (ignored by implementation, but required field)
                bw.Write(0); // 32-bit offset

                // minimum_instruction_length
                bw.Write((byte)1);

                // maximum_operations_per_instruction (version > 3)
                bw.Write((byte)1);

                // default_is_stmt = 1 (true)
                bw.Write((byte)1);

                // line_base (sbyte) and line_range
                bw.Write(unchecked((byte)0)); // line_base = 0
                bw.Write((byte)1);            // line_range = 1

                // opcode_base and standard_opcode_lengths (1..12)
                byte opcodeBase = 13;
                bw.Write(opcodeBase);
                for (int i = 1; i < opcodeBase; i++)
                {
                    // All standard opcodes take 0 extra operands for this test
                    bw.Write((byte)0);
                }

                // Directories: none — write terminator 0
                bw.Write((byte)0x00);

                // Files: one entry "file.c" with directory index 0, timestamp 0, length 0
                bw.Write(System.Text.Encoding.UTF8.GetBytes("file.c"));
                bw.Write((byte)0x00);                 // null terminator for name
                bw.Write((byte)0x00);                 // directory index (ULEB128 0)
                bw.Write((byte)0x00);                 // lastModified (ULEB128 0)
                bw.Write((byte)0x00);                 // length (ULEB128 0)
                bw.Write((byte)0x00);                 // files terminator

                // Instructions: a single COPY opcode (standard opcode 1)
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }

            byte[] bodyBytes = body.ToArray();
            uint unitLength = (uint)bodyBytes.Length;

            byte[] debugLineData = BitConverter.GetBytes(unitLength)
                .Concat(bodyBytes)
                .Concat(new byte[] { 0x00 }) // extra padding byte so endPosition stays within bounds
                .ToArray();

            using var debugLine = new DwarfMemoryReader(debugLineData);
            using var debugStrings = new DwarfMemoryReader(new byte[] { 0x00 });

            // Normalize addresses by adding a fixed offset to prove post-processing ran.
            uint normalizeDelta = 0x10;
            var program = new DwarfLineNumberProgram(
                dwarfVersion: 4,
                debugLine: debugLine,
                debugStrings: debugStrings,
                addressNormalizer: addr => addr + normalizeDelta);

            program.Files.Should().NotBeNull();
            program.Files.Should().HaveCount(1);

            var file = program.Files[0];
            file.Name.Should().Be("file.c");
            file.Lines.Should().HaveCount(1);

            var line = file.Lines[0];
            line.File.Should().BeSameAs(file);
            line.Line.Should().Be(1u);          // initial line
            line.Column.Should().Be(0ul);       // default column
            line.Address.Should().Be(normalizeDelta); // 0 + normalizeDelta
        }

        [Fact]
        public void ReadData_Dwarf5_DirectoriesAndFiles_UseStrpAndDirectoryIndex()
        {
            // Build DWARF5 .debug_line section exercising the DWARF5-specific
            // directory and file tables using Strp and DirectoryIndex entries.

            // .debug_line/.debug_line_str (or shared debugStrings) data:
            //  offset 0: "dir\0"
            //  offset 4: "foo.c\0"
            byte[] debugStringsData =
            {
                (byte)'d', (byte)'i', (byte)'r', 0x00,
                (byte)'f', (byte)'o', (byte)'o', (byte)'.', (byte)'c', 0x00,
            };

            var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                // version = 5
                bw.Write((ushort)5);

                // address_size and segment_selector_size (ignored by tests)
                bw.Write((byte)8); // address_size
                bw.Write((byte)0); // segmentSelectorSize

                // header_length (offset; implementation does not use it for skipping)
                bw.Write(0); // 32-bit

                // minimum_instruction_length
                bw.Write((byte)1);

                // maximum_operations_per_instruction (version > 3)
                bw.Write((byte)1);

                // default_is_stmt = 1
                bw.Write((byte)1);

                // line_base, line_range
                bw.Write(unchecked((byte)0)); // line_base = 0
                bw.Write((byte)1);            // line_range = 1

                // opcode_base and standard_opcode_lengths
                byte opcodeBase = 13;
                bw.Write(opcodeBase);
                for (int i = 1; i < opcodeBase; i++)
                {
                    bw.Write((byte)0);
                }

                // ---- DWARF5 directory table ----

                // directoryEntryFormatCount = 1
                bw.Write((byte)1);

                // Entry 0: Path (Strp)
                bw.Write(EncodeULEB128((ulong)DwarfLineNumberHeaderEntryFormat.Path));
                bw.Write(EncodeULEB128((ulong)DwarfFormat.Strp));

                // directoriesCount = 1
                bw.Write(EncodeULEB128(1));

                // Directory 0: path at offset 0 in debugStringsData
                bw.Write(BitConverter.GetBytes(0));

                // ---- DWARF5 file table ----

                // fileEntryFormatCount = 2
                bw.Write((byte)2);

                // Entry 0: Path (Strp)
                bw.Write(EncodeULEB128((ulong)DwarfLineNumberHeaderEntryFormat.Path));
                bw.Write(EncodeULEB128((ulong)DwarfFormat.Strp));

                // Entry 1: DirectoryIndex (Data1)
                bw.Write(EncodeULEB128((ulong)DwarfLineNumberHeaderEntryFormat.DirectoryIndex));
                bw.Write(EncodeULEB128((ulong)DwarfFormat.Data1));

                // filesCount = 1
                bw.Write(EncodeULEB128(1));

                // File 0: name at offset 4, directory index = 0 (implementation treats this as first directory)
                bw.Write(BitConverter.GetBytes(4)); // Strp offset to "foo.c"
                bw.Write((byte)0);                  // directory index (Data1)

                // ---- Opcodes ----

                // Single COPY to emit one line record.
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }

            byte[] bodyBytes = body.ToArray();
            uint unitLength = (uint)bodyBytes.Length;

            byte[] debugLineData = BitConverter.GetBytes(unitLength)
                .Concat(bodyBytes)
                .Concat(new byte[] { 0x00 }) // extra padding byte so endPosition stays within bounds
                .ToArray();

            using var debugLine = new DwarfMemoryReader(debugLineData);
            using var debugStrings = new DwarfMemoryReader(debugStringsData);

            var program = new DwarfLineNumberProgram(
                dwarfVersion: 5,
                debugLine: debugLine,
                debugStrings: debugStrings,
                addressNormalizer: addr => addr);

            program.Files.Should().NotBeNull();
            program.Files.Should().HaveCount(1);

            var file = program.Files[0];
            file.Name.Should().Be("foo.c");
            file.Directory.Should().Be("dir");
            // Path combines directory and name; we only check that both segments appear.
            file.Path.Should().Contain("foo.c");
            file.Path.Should().Contain("dir");

            file.Lines.Should().HaveCount(1);
            var line = file.Lines[0];
            line.File.Should().BeSameAs(file);
            line.Line.Should().Be(1u);
            line.Address.Should().Be(0u);
        }

        #region Helpers

        /// <summary>
        /// Encode a signed LEB128 value into bytes.
        /// </summary>
        private static byte[] EncodeSLEB128(int value)
        {
            var bytes = new List<byte>();
            bool more = true;
            while (more)
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if ((value == 0 && (b & 0x40) == 0) || (value == -1 && (b & 0x40) != 0))
                {
                    more = false;
                }
                else
                {
                    b |= 0x80;
                }

                bytes.Add(b);
            }
            return bytes.ToArray();
        }

        /// <summary>
        /// Builds a minimal DWARF4 .debug_line section and returns the parsed program.
        /// </summary>
        private static DwarfLineNumberProgram BuildDwarf4Program(
            Action<BinaryWriter> writeOpcodes,
            byte minimumInstructionLength = 1,
            sbyte lineBase = 0,
            byte lineRange = 1,
            byte operationCodeBase = 13,
            string[] fileNames = null,
            string[] directories = null,
            NormalizeAddressDelegate addressNormalizer = null,
            int dwarfVersion = 4)
        {
            fileNames ??= new[] { "file.c" };
            addressNormalizer ??= addr => addr;

            var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write((ushort)dwarfVersion);

                if (dwarfVersion > 4)
                {
                    bw.Write((byte)8); // addressSize
                    bw.Write((byte)0); // segmentSelectorSize
                }

                bw.Write(0); // header_length (32-bit)
                bw.Write(minimumInstructionLength);

                if (dwarfVersion > 3)
                {
                    bw.Write((byte)1); // maximumOperationsPerInstruction
                }

                bw.Write((byte)1); // default_is_stmt
                bw.Write(unchecked((byte)lineBase));
                bw.Write(lineRange);
                bw.Write(operationCodeBase);

                for (int i = 1; i < operationCodeBase; i++)
                {
                    bw.Write((byte)0);
                }

                // Directories
                if (directories != null)
                {
                    foreach (string dir in directories)
                    {
                        bw.Write(System.Text.Encoding.UTF8.GetBytes(dir));
                        bw.Write((byte)0x00);
                    }
                }
                bw.Write((byte)0x00); // directory terminator

                // Files
                foreach (string fileName in fileNames)
                {
                    bw.Write(System.Text.Encoding.UTF8.GetBytes(fileName));
                    bw.Write((byte)0x00); // null terminator
                    bw.Write((byte)0x00); // directory index
                    bw.Write((byte)0x00); // timestamp
                    bw.Write((byte)0x00); // length
                }
                bw.Write((byte)0x00); // file terminator

                writeOpcodes(bw);
            }

            byte[] bodyBytes = body.ToArray();
            uint unitLength = (uint)bodyBytes.Length;

            byte[] debugLineData = BitConverter.GetBytes(unitLength)
                .Concat(bodyBytes)
                .Concat(new byte[] { 0x00 })
                .ToArray();

            using var debugLine = new DwarfMemoryReader(debugLineData);
            using var debugStrings = new DwarfMemoryReader(new byte[] { 0x00 });

            return new DwarfLineNumberProgram(
                dwarfVersion: dwarfVersion,
                debugLine: debugLine,
                debugStrings: debugStrings,
                addressNormalizer: addressNormalizer);
        }

        /// <summary>
        /// Writes a SetAddress extended opcode.
        /// </summary>
        private static void WriteSetAddress(BinaryWriter bw, uint address)
        {
            bw.Write((byte)0x00); // extended marker
            bw.Write(EncodeULEB128(5)); // length: 1 opcode + 4 address
            bw.Write((byte)DwarfLineNumberExtendedOpcode.SetAddress);
            bw.Write(address);
        }

        /// <summary>
        /// Writes an EndSequence extended opcode.
        /// </summary>
        private static void WriteEndSequence(BinaryWriter bw)
        {
            bw.Write((byte)0x00);
            bw.Write(EncodeULEB128(1));
            bw.Write((byte)DwarfLineNumberExtendedOpcode.EndSequence);
        }

        #endregion

        #region Error / Early-Return Paths

        [Fact]
        public void ReadData_WithVersionLessThanTwo_ReturnsNull()
        {
            // Manually build data with version=1 so ReadData returns null.
            var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write((ushort)1); // version < 2
                bw.Write(0);         // header_length
                bw.Write((byte)1);   // minInstrLen
                bw.Write((byte)1);   // maxOpsPerInstr
                bw.Write((byte)1);   // default_is_stmt
                bw.Write((byte)0);   // lineBase
                bw.Write((byte)1);   // lineRange
                bw.Write((byte)13);  // opcodeBase
                for (int i = 1; i < 13; i++) bw.Write((byte)0);
            }

            byte[] bodyBytes = body.ToArray();
            byte[] debugLineData = BitConverter.GetBytes((uint)bodyBytes.Length)
                .Concat(bodyBytes).Concat(new byte[] { 0x00 }).ToArray();

            using var debugLine = new DwarfMemoryReader(debugLineData);
            using var debugStrings = new DwarfMemoryReader(new byte[] { 0x00 });

            var program = new DwarfLineNumberProgram(4, debugLine, debugStrings, addr => addr);
            program.Files.Should().BeNull();
        }

        [Fact]
        public void ReadData_WithOperationCodeBaseZero_ReturnsNull()
        {
            var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write((ushort)4);
                bw.Write(0);
                bw.Write((byte)1);
                bw.Write((byte)1); // maxOpsPerInstr
                bw.Write((byte)1);
                bw.Write((byte)0);
                bw.Write((byte)1);
                bw.Write((byte)0); // operationCodeBase = 0
            }

            byte[] bodyBytes = body.ToArray();
            byte[] debugLineData = BitConverter.GetBytes((uint)bodyBytes.Length)
                .Concat(bodyBytes).Concat(new byte[] { 0x00 }).ToArray();

            using var debugLine = new DwarfMemoryReader(debugLineData);
            using var debugStrings = new DwarfMemoryReader(new byte[] { 0x00 });

            var program = new DwarfLineNumberProgram(4, debugLine, debugStrings, addr => addr);
            program.Files.Should().BeNull();
        }

        [Fact]
        public void ReadData_WithEndPositionBeyondBuffer_ReturnsNull()
        {
            // unitLength points past the end of data.
            byte[] debugLineData = BitConverter.GetBytes((uint)9999)
                .Concat(new byte[] { 0x04, 0x00 }) // version=4 stub
                .ToArray();

            using var debugLine = new DwarfMemoryReader(debugLineData);
            using var debugStrings = new DwarfMemoryReader(new byte[] { 0x00 });

            var program = new DwarfLineNumberProgram(4, debugLine, debugStrings, addr => addr);
            program.Files.Should().BeNull();
        }

        #endregion

        #region Standard Opcodes

        [Fact]
        public void ReadData_AdvancePc_AdvancesAddressByOperandTimesMinInstructionLength()
        {
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.AdvancePc);
                bw.Write(EncodeULEB128(5));
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }, minimumInstructionLength: 4);

            program.Files.Should().HaveCount(1);
            program.Files[0].Lines.Should().HaveCount(1);
            program.Files[0].Lines[0].Address.Should().Be(20u);
        }

        [Fact]
        public void ReadData_AdvanceLine_PositiveValue_IncrementsLine()
        {
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.AdvanceLine);
                bw.Write(EncodeSLEB128(10));
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            });

            program.Files[0].Lines.Should().HaveCount(1);
            program.Files[0].Lines[0].Line.Should().Be(11u); // initial 1 + 10
        }

        [Fact]
        public void ReadData_AdvanceLine_NegativeAfterPositive_DecrementsLine()
        {
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.AdvanceLine);
                bw.Write(EncodeSLEB128(20));
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
                bw.Write((byte)DwarfLineNumberStandardOpcode.AdvanceLine);
                bw.Write(EncodeSLEB128(-5));
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            });

            program.Files[0].Lines.Should().HaveCount(2);
            program.Files[0].Lines[0].Line.Should().Be(21u);
            program.Files[0].Lines[1].Line.Should().Be(16u);
        }

        [Fact]
        public void ReadData_SetFile_SwitchesToSecondFile()
        {
            var program = BuildDwarf4Program(bw =>
            {
                // SetFile to file 2 (1-based index)
                bw.Write((byte)DwarfLineNumberStandardOpcode.SetFile);
                bw.Write(EncodeULEB128(2));
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }, fileNames: new[] { "first.c", "second.c" });

            program.Files.Should().HaveCount(2);
            program.Files[0].Lines.Should().BeEmpty();
            program.Files[1].Lines.Should().HaveCount(1);
            program.Files[1].Name.Should().Be("second.c");
        }

        [Fact]
        public void ReadData_SetColumn_SetsColumnOnEmittedLine()
        {
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.SetColumn);
                bw.Write(EncodeULEB128(42));
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            });

            program.Files[0].Lines[0].Column.Should().Be(42ul);
        }

        [Fact]
        public void ReadData_ConstAddPc_AdvancesAddressByFormula()
        {
            // With opcodeBase=13, lineRange=14, minInstrLen=1:
            // advance = (255 - 13) / 14 = 17
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.ConstAddPc);
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }, lineRange: 14, operationCodeBase: 13);

            program.Files[0].Lines[0].Address.Should().Be(17u);
        }

        [Fact]
        public void ReadData_FixedAdvancePc_AddsUshortToAddress()
        {
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.FixedAdvancePc);
                bw.Write((ushort)256);
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            });

            program.Files[0].Lines[0].Address.Should().Be(256u);
        }

        #endregion

        #region Extended Opcodes

        [Fact]
        public void ReadData_SetAddress_SetsAddressToValue()
        {
            var program = BuildDwarf4Program(bw =>
            {
                WriteSetAddress(bw, 0x2000);
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            });

            program.Files[0].Lines[0].Address.Should().Be(0x2000u);
        }

        [Fact]
        public void ReadData_EndSequence_EmitsLineAndResetsState()
        {
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.AdvanceLine);
                bw.Write(EncodeSLEB128(5));
                WriteSetAddress(bw, 0x1000);
                WriteEndSequence(bw);
                // After reset: address=0, line=1
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            });

            program.Files[0].Lines.Should().HaveCount(2);
            // EndSequence emission
            program.Files[0].Lines[0].Address.Should().Be(0x1000u);
            program.Files[0].Lines[0].Line.Should().Be(6u); // 1 + 5
            // After reset
            program.Files[0].Lines[1].Address.Should().Be(0u);
            program.Files[0].Lines[1].Line.Should().Be(1u);
        }

        [Fact]
        public void ReadData_SetAddress_WhenZero_UsesLastAddressFromPreviousEndSequence()
        {
            var program = BuildDwarf4Program(bw =>
            {
                WriteSetAddress(bw, 0x5000);
                WriteEndSequence(bw);
                // SetAddress(0) should fall back to lastAddress = 0x5000
                WriteSetAddress(bw, 0);
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            });

            program.Files[0].Lines.Should().HaveCount(2);
            program.Files[0].Lines[1].Address.Should().Be(0x5000u);
        }

        [Fact]
        public void ReadData_DefineFile_AddsAndSwitchesToNewFile()
        {
            var program = BuildDwarf4Program(bw =>
            {
                // Extended: DefineFile
                bw.Write((byte)0x00); // extended marker
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes("new.c");
                // length = 1 (opcode) + name.Length + 1 (null) + 3 (ULEB128 zeros)
                int extLen = 1 + nameBytes.Length + 1 + 3;
                bw.Write(EncodeULEB128((ulong)extLen));
                bw.Write((byte)DwarfLineNumberExtendedOpcode.DefineFile);
                bw.Write(nameBytes);
                bw.Write((byte)0x00); // null terminator
                bw.Write((byte)0x00); // directory index
                bw.Write((byte)0x00); // timestamp
                bw.Write((byte)0x00); // length
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            });

            program.Files.Should().HaveCount(2);
            program.Files[1].Name.Should().Be("new.c");
            program.Files[1].Lines.Should().HaveCount(1);
        }

        #endregion

        #region Special Opcodes

        [Fact]
        public void ReadData_SpecialOpcode_AdvancesAddressAndLine()
        {
            // lineBase=-3, lineRange=12, opcodeBase=13, minInstrLen=1
            // Opcode 20: adjusted = 20 - 13 = 7
            //   operationAdvance = 7 / 12 = 0
            //   lineAdvance = -3 + (7 % 12) = -3 + 7 = 4
            // Result: address=0, line=1+4=5
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)20); // special opcode
            }, lineBase: -3, lineRange: 12);

            program.Files[0].Lines.Should().HaveCount(1);
            program.Files[0].Lines[0].Address.Should().Be(0u);
            program.Files[0].Lines[0].Line.Should().Be(5u);
        }

        [Fact]
        public void ReadData_SpecialOpcode_WithAddressAdvance()
        {
            // lineBase=0, lineRange=4, opcodeBase=13, minInstrLen=2
            // Opcode 21: adjusted = 21 - 13 = 8
            //   operationAdvance = 8 / 4 = 2
            //   lineAdvance = 0 + (8 % 4) = 0
            // address = 2 * 2 = 4, line = 1
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)21);
            }, lineBase: 0, lineRange: 4, minimumInstructionLength: 2);

            program.Files[0].Lines[0].Address.Should().Be(4u);
            program.Files[0].Lines[0].Line.Should().Be(1u);
        }

        [Fact]
        public void ReadData_MultipleSpecialOpcodes_AccumulateState()
        {
            // lineBase=0, lineRange=4, opcodeBase=13, minInstrLen=1
            // Opcode 17: adjusted=4, opAdvance=1, lineAdv=0. addr=1, line=1.
            // Opcode 17: adjusted=4, opAdvance=1, lineAdv=0. addr=2, line=1.
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)17);
                bw.Write((byte)17);
            }, lineBase: 0, lineRange: 4);

            program.Files[0].Lines.Should().HaveCount(2);
            program.Files[0].Lines[0].Address.Should().Be(1u);
            program.Files[0].Lines[1].Address.Should().Be(2u);
        }

        #endregion

        #region Multiple Files, Directories, Normalization

        [Fact]
        public void ReadData_Dwarf4_MultipleDirectories_ResolvesFilePaths()
        {
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }, fileNames: null, directories: new[] { "src", "lib" });

            // Override: need custom file entries with directory indices.
            // Since BuildDwarf4Program doesn't support dir indices on files,
            // build manually.
            var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bw.Write((ushort)4);
                bw.Write(0);
                bw.Write((byte)1);
                bw.Write((byte)1);
                bw.Write((byte)1);
                bw.Write((byte)0);
                bw.Write((byte)1);
                bw.Write((byte)13);
                for (int i = 1; i < 13; i++) bw.Write((byte)0);

                // Directories
                bw.Write(System.Text.Encoding.UTF8.GetBytes("src"));
                bw.Write((byte)0x00);
                bw.Write(System.Text.Encoding.UTF8.GetBytes("lib"));
                bw.Write((byte)0x00);
                bw.Write((byte)0x00); // terminator

                // File in dir 1 (src)
                bw.Write(System.Text.Encoding.UTF8.GetBytes("main.c"));
                bw.Write((byte)0x00);
                bw.Write(EncodeULEB128(1)); // directory index 1 = "src"
                bw.Write((byte)0x00);
                bw.Write((byte)0x00);

                // File in dir 2 (lib)
                bw.Write(System.Text.Encoding.UTF8.GetBytes("util.c"));
                bw.Write((byte)0x00);
                bw.Write(EncodeULEB128(2)); // directory index 2 = "lib"
                bw.Write((byte)0x00);
                bw.Write((byte)0x00);

                bw.Write((byte)0x00); // file terminator

                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }

            byte[] bodyBytes = body.ToArray();
            byte[] debugLineData = BitConverter.GetBytes((uint)bodyBytes.Length)
                .Concat(bodyBytes).Concat(new byte[] { 0x00 }).ToArray();

            using var debugLine = new DwarfMemoryReader(debugLineData);
            using var debugStrings = new DwarfMemoryReader(new byte[] { 0x00 });

            var prog = new DwarfLineNumberProgram(4, debugLine, debugStrings, addr => addr);

            prog.Files.Should().HaveCount(2);
            prog.Files[0].Path.Should().Contain("src");
            prog.Files[0].Path.Should().Contain("main.c");
            prog.Files[1].Path.Should().Contain("lib");
            prog.Files[1].Path.Should().Contain("util.c");
        }

        [Fact]
        public void ReadData_Dwarf4_MultipleFiles_LinesAssignedToCorrectFile()
        {
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy); // line on file 1
                bw.Write((byte)DwarfLineNumberStandardOpcode.SetFile);
                bw.Write(EncodeULEB128(2)); // switch to file 2
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy); // line on file 2
            }, fileNames: new[] { "a.c", "b.c" });

            program.Files[0].Lines.Should().HaveCount(1);
            program.Files[1].Lines.Should().HaveCount(1);
        }

        [Fact]
        public void ReadData_NormalizationAppliedToAllLinesAcrossFiles()
        {
            var program = BuildDwarf4Program(bw =>
            {
                WriteSetAddress(bw, 0x100);
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
                bw.Write((byte)DwarfLineNumberStandardOpcode.SetFile);
                bw.Write(EncodeULEB128(2));
                WriteSetAddress(bw, 0x200);
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }, fileNames: new[] { "a.c", "b.c" },
               addressNormalizer: addr => addr + 0x1000);

            program.Files[0].Lines[0].Address.Should().Be(0x1100u);
            program.Files[1].Lines[0].Address.Should().Be(0x1200u);
        }

        [Fact]
        public void ReadData_Dwarf3_ParsesWithoutMaxOperationsPerInstruction()
        {
            // DWARF3: no maxOperationsPerInstruction field in header.
            var program = BuildDwarf4Program(bw =>
            {
                bw.Write((byte)DwarfLineNumberStandardOpcode.Copy);
            }, dwarfVersion: 3);

            program.Files.Should().NotBeNull();
            program.Files.Should().HaveCount(1);
            program.Files[0].Lines.Should().HaveCount(1);
        }

        #endregion
    }
}
