// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Unit tests for <see cref="DwarfSymbolProvider"/>.
    /// </summary>
    public class DwarfSymbolProviderTests
    {
        /// <summary>
        /// Minimal IDwarfBinary stub needed by ParseLineNumberPrograms.
        /// </summary>
        private class StubDwarfBinary : IDwarfBinary
        {
            public int DwarfVersion { get; set; }
            public DwarfUnitType DwarfUnitType { get; set; }
            public byte[] DebugData { get; } = Array.Empty<byte>();
            public byte[] DebugDataDescription { get; } = Array.Empty<byte>();
            public byte[] DebugDataStrings { get; } = Array.Empty<byte>();
            public byte[] DebugLine { get; } = Array.Empty<byte>();
            public byte[] DebugFrame { get; } = Array.Empty<byte>();
            public byte[] EhFrame { get; } = Array.Empty<byte>();
            public ulong CodeSegmentOffset { get; } = 0;
            public ulong EhFrameAddress { get; } = 0;
            public ulong TextSectionAddress { get; } = 0;
            public ulong DataSectionAddress { get; } = 0;
            public IReadOnlyList<DwarfPublicSymbol> PublicSymbols { get; } = Array.Empty<DwarfPublicSymbol>();
            public bool Is64bit { get; } = false;
            public ICompiler[] Compilers { get; } = Array.Empty<ICompiler>();
            public List<DwarfCompileCommandLineInfo> CommandLineInfos { get; } = new List<DwarfCompileCommandLineInfo>();
            public ulong NormalizeAddress(ulong address) => address;
            public DwarfLanguage GetLanguage() => DwarfLanguage.C;
            public void Dispose() { }
        }

        private static byte[] EncodeULEB128(ulong value) => DwarfTestHelpers.EncodeULEB128(value);

        // ---- ParseDebugStringOffsets ----

        [Fact]
        public void ParseDebugStringOffsets_Reads32BitOffsets()
        {
            byte[] data = BitConverter.GetBytes(1u)
                .Concat(BitConverter.GetBytes(2u))
                .ToArray();

            List<int> offsets = DwarfSymbolProvider.ParseDebugStringOffsets(data, is64bit: false);

            offsets.Should().Equal(1, 2);
        }

        [Fact]
        public void ParseDebugStringOffsets_Reads64BitOffsets()
        {
            byte[] data = BitConverter.GetBytes(1ul)
                .Concat(BitConverter.GetBytes(2ul))
                .ToArray();

            List<int> offsets = DwarfSymbolProvider.ParseDebugStringOffsets(data, is64bit: true);

            offsets.Should().Equal(1, 2);
        }

        // ---- ParseLineNumberPrograms ----

        [Fact]
        public void ParseLineNumberPrograms_WithEmptyDebugLine_ReturnsEmptyList()
        {
            var dwarfBinary = new StubDwarfBinary { DwarfVersion = 4 };

            List<DwarfLineNumberProgram> programs = DwarfSymbolProvider.ParseLineNumberPrograms(
                dwarfBinary,
                debugLine: Array.Empty<byte>(),
                debugStrings: new byte[] { 0x00 },
                debugLineStrings: new byte[] { 0x00 },
                addressNormalizer: addr => addr);

            programs.Should().NotBeNull();
            programs.Should().BeEmpty();
        }

        [Fact]
        public void ParseLineNumberPrograms_StopsWhenProgramFilesIsNull()
        {
            var dwarfBinary = new StubDwarfBinary { DwarfVersion = 4 };

            // unit_length = 1 triggers the early-null path in DwarfLineNumberProgram.ReadData.
            byte[] debugLineData = new byte[] { 0x01, 0x00, 0x00, 0x00 };

            List<DwarfLineNumberProgram> programs = DwarfSymbolProvider.ParseLineNumberPrograms(
                dwarfBinary,
                debugLine: debugLineData,
                debugStrings: new byte[] { 0x00 },
                debugLineStrings: new byte[] { 0x00 },
                addressNormalizer: addr => addr);

            programs.Should().NotBeNull();
            programs.Should().BeEmpty();
        }

        [Fact]
        public void ParseLineNumberPrograms_Dwarf4SingleProgram_ProducesOneNormalizedLine()
        {
            // Build a minimal DWARF4 .debug_line section with one file and a single COPY opcode,
            // mirroring DwarfLineNumberProgramTests but exercising the aggregator in DwarfSymbolProvider.

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

            // Append 4 padding bytes so that the aggregator's loop terminates cleanly
            // after the valid program.
            byte[] debugLineData = BitConverter.GetBytes(unitLength)
                .Concat(bodyBytes)
                .Concat(new byte[] { 0x00, 0x00, 0x00, 0x00 })
                .ToArray();

            var dwarfBinary = new StubDwarfBinary { DwarfVersion = 4 };

            uint normalizeDelta = 0x10;
            List<DwarfLineNumberProgram> programs = DwarfSymbolProvider.ParseLineNumberPrograms(
                dwarfBinary,
                debugLine: debugLineData,
                debugStrings: new byte[] { 0x00 },
                debugLineStrings: new byte[] { 0x00 },
                addressNormalizer: addr => addr + normalizeDelta);

            programs.Should().HaveCount(1);

            DwarfLineNumberProgram program = programs[0];
            program.Files.Should().NotBeNull();
            program.Files.Should().HaveCount(1);

            DwarfFileInformation file = program.Files[0];
            file.Name.Should().Be("file.c");
            file.Lines.Should().HaveCount(1);

            DwarfLineInformation line = file.Lines[0];
            line.File.Should().BeSameAs(file);
            line.Line.Should().Be(1u);
            line.Column.Should().Be(0ul);
            line.Address.Should().Be(normalizeDelta);
        }

        // ---- ParseAllCommandLineInfos ----

        [Fact]
        public void ParseAllCommandLineInfos_CompileUnitWithSupportedLanguage_ProducesInfo()
        {
            var attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
            {
                { DwarfAttribute.Name, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "/src/main.c" } },
                { DwarfAttribute.CompDir, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "/src" } },
                { DwarfAttribute.Producer, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "gcc -o main main.c" } },
                { DwarfAttribute.Language, new DwarfAttributeValue { Type = DwarfAttributeValueType.Constant, Value = (ulong)DwarfLanguage.CPlusPlus11 } },
            };

            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.CompileUnit,
                Attributes = attributes,
            };

            DwarfCompilationUnit cu = CreateCompilationUnitWithSymbols(symbol);

            List<DwarfCompileCommandLineInfo> infos = DwarfSymbolProvider.ParseAllCommandLineInfos(
                new List<DwarfCompilationUnit> { cu });

            infos.Should().HaveCount(1);
            DwarfCompileCommandLineInfo info = infos[0];

            info.Type.Should().Be(DwarfTag.CompileUnit);
            info.FullName.Should().Be("/src/main.c");
            info.CompileDirectory.Should().Be("/src");
            info.FileName.Should().Be("main.c");
            info.CommandLine.Should().Be("gcc -o main main.c");
            info.Language.Should().Be(DwarfLanguage.CPlusPlus11);
        }

        [Fact]
        public void ParseAllCommandLineInfos_CompileUnitWithUnsupportedLanguage_IsSkipped()
        {
            var attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
            {
                { DwarfAttribute.Name, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "mod.f" } },
                { DwarfAttribute.Producer, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "gfortran mod.f" } },
                { DwarfAttribute.Language, new DwarfAttributeValue { Type = DwarfAttributeValueType.Constant, Value = (ulong)DwarfLanguage.Fortran77 } },
            };

            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.CompileUnit,
                Attributes = attributes,
            };

            DwarfCompilationUnit cu = CreateCompilationUnitWithSymbols(symbol);

            List<DwarfCompileCommandLineInfo> infos = DwarfSymbolProvider.ParseAllCommandLineInfos(
                new List<DwarfCompilationUnit> { cu });

            infos.Should().BeEmpty();
        }

        [Fact]
        public void ParseAllCommandLineInfos_SubprogramUsesLinkageName()
        {
            var attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
            {
                { DwarfAttribute.Name, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "foo" } },
                { DwarfAttribute.LinkageName, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "_Z3foov" } },
            };

            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.Subprogram,
                Attributes = attributes,
            };

            DwarfCompilationUnit cu = CreateCompilationUnitWithSymbols(symbol);

            List<DwarfCompileCommandLineInfo> infos = DwarfSymbolProvider.ParseAllCommandLineInfos(
                new List<DwarfCompilationUnit> { cu });

            infos.Should().HaveCount(1);
            DwarfCompileCommandLineInfo info = infos[0];

            info.Type.Should().Be(DwarfTag.Subprogram);
            info.FullName.Should().Be("foo");
            info.FileName.Should().Be("foo");
            info.CommandLine.Should().Be("_Z3foov");
            info.Language.Should().Be(DwarfLanguage.Unknown);
        }

        [Fact]
        public void ParseAllCommandLineInfos_StripsNumericAndLongUnsignedIntNames()
        {
            var attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
            {
                { DwarfAttribute.Name, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "1234" } },
                { DwarfAttribute.CompDir, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = ElfUtility.LongUnsignedInt } },
                { DwarfAttribute.Producer, new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "gcc 1234" } },
                { DwarfAttribute.Language, new DwarfAttributeValue { Type = DwarfAttributeValueType.Constant, Value = (ulong)DwarfLanguage.C } },
            };

            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.CompileUnit,
                Attributes = attributes,
            };

            DwarfCompilationUnit cu = CreateCompilationUnitWithSymbols(symbol);

            List<DwarfCompileCommandLineInfo> infos = DwarfSymbolProvider.ParseAllCommandLineInfos(
                new List<DwarfCompilationUnit> { cu });

            infos.Should().HaveCount(1);
            DwarfCompileCommandLineInfo info = infos[0];

            info.FullName.Should().BeEmpty();
            info.CompileDirectory.Should().BeEmpty();
            info.CommandLine.Should().Be("gcc 1234");
        }

        private static DwarfCompilationUnit CreateCompilationUnitWithSymbols(params DwarfSymbol[] symbols)
        {
            // Create a real DwarfCompilationUnit instance with a minimal, version-0 header
            // so that ReadData bails out early, then inject our own symbol table via reflection.
            var dwarfBinary = new StubDwarfBinary();

            // 4-byte unit_length (0) + 2-byte version (0) is enough for ReadData
            // to hit the "version == 0" early-return path without reading further.
            using var debugData = new DwarfMemoryReader(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            using var debugDataDescription = new DwarfMemoryReader(Array.Empty<byte>());
            using var debugStrings = new DwarfMemoryReader(Array.Empty<byte>());
            using var debugLineStrings = new DwarfMemoryReader(Array.Empty<byte>());
            IList<int> debugStringOffsets = Array.Empty<int>();

            var cu = new DwarfCompilationUnit(
                dwarfBinary,
                debugData,
                debugDataDescription,
                debugStrings,
                debugLineStrings,
                debugStringOffsets,
                address => address);

            FieldInfo field = typeof(DwarfCompilationUnit).GetField("symbolsByOffset", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull("DwarfCompilationUnit must have a symbolsByOffset field for tests to seed symbols");

            var map = new Dictionary<uint, DwarfSymbol>();
            uint offset = 0;
            foreach (DwarfSymbol symbol in symbols)
            {
                map[offset++] = symbol;
            }

            field.SetValue(cu, map);

            return cu;
        }
    }
}
