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
    }
}
