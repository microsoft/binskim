// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.Elf;
using Microsoft.Win32.SafeHandles;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public class DwarfMappedReaderTests
    {
        [Fact]
        public void MappedReader_ReadsAcrossAndBeyondInt32Boundary()
        {
            const uint position = (uint)int.MaxValue - 1;
            const ulong sectionOffset = 17; // Also exercise an unaligned mapping offset.
            const ulong sectionSize = (ulong)int.MaxValue + 128;
            using var file = new TemporarySparseFile();
            using (FileStream stream = File.OpenWrite(file.Path))
            {
                stream.SetLength((long)(sectionOffset + sectionSize));
                stream.Position = (long)sectionOffset + position;
                stream.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 0xE5, 0x8E, 0x26, 0x7E });
                stream.Write(Encoding.ASCII.GetBytes("mapped\0"));
            }

            using var reader = new DwarfMemoryReader(file.Path, sectionOffset, sectionSize);
            reader.Length.Should().Be((long)sectionSize);
            reader.Data.Should().BeEmpty(); // No multi-gigabyte managed array was allocated.
            reader.Position = position;
            reader.Peek().Should().Be(1);
            reader.ReadUshort().Should().Be(0x0201);
            reader.ReadUint().Should().Be(0x06050403);
            reader.ReadByte().Should().Be(7);
            reader.ReadByte().Should().Be(8);
            reader.ULEB128().Should().Be(624485);
            ((int)reader.SLEB128()).Should().Be(-2);
            reader.ReadString().Should().Be("mapped");
            reader.ReadUlong(8).Should().Be(0); // Sparse, unread data remains readable.

            reader.Position = position;
            reader.ReadUlong().Should().Be(0x0807060504030201);
            reader.ReadUint(position + 2).Should().Be(0x06050403);
            reader.Position.Should().Be(position + 8);
            reader.ReadBlock(3, position + 3).Should().Equal(4, 5, 6);
            reader.ReadStructure<uint>(position + 2).Should().Be(0x06050403);
            reader.ReadString(position + 12).Should().Be("mapped");
            reader.Position.Should().Be(position + 8);

            reader.Position = (uint)sectionSize - 1;
            reader.ReadBlock(10).Should().Equal(0);
            reader.IsEnd.Should().BeTrue();
            DwarfBufferOverreadException exception = Assert.Throws<DwarfBufferOverreadException>(() => reader.ReadByte());
            exception.LongBufferLength.Should().Be((long)sectionSize);
        }

        [Fact]
        public void MappedReader_DoesNotReadTerminatorOutsideSection()
        {
            using var file = new TemporarySparseFile();
            File.WriteAllBytes(file.Path, new byte[] { 0xFF, (byte)'a', (byte)'b', 0 });
            using var reader = new DwarfMemoryReader(file.Path, 1, 2);

            Assert.Throws<DwarfBufferOverreadException>(() => reader.ReadString());
            reader.Position.Should().Be(2);
        }

        [Theory]
        [InlineData(5ul, 1ul)]
        [InlineData(1ul, 4ul)]
        [InlineData(ulong.MaxValue, 1ul)]
        public void MappedReader_RejectsSectionOutsideFile(ulong offset, ulong size)
        {
            using var file = new TemporarySparseFile();
            File.WriteAllBytes(file.Path, new byte[] { 1, 2, 3, 4 });
            Assert.Throws<InvalidDataException>(() => new DwarfMemoryReader(file.Path, offset, size));
        }

        [Fact]
        public void MappedReader_RejectsSectionsOutsideSupportedOffsetRange()
        {
            using var file = new TemporarySparseFile();
            Assert.Throws<NotSupportedException>(() => new DwarfMemoryReader(file.Path, 0, (ulong)uint.MaxValue + 1));
        }

        [Fact]
        public void MappedReader_HandlesEmptySectionAndRepeatedDisposal()
        {
            using var file = new TemporarySparseFile();
            var reader = new DwarfMemoryReader(file.Path, 0, 0);
            reader.IsEnd.Should().BeTrue();
            reader.ReadBlock(1).Should().BeEmpty();
            Assert.Throws<DwarfBufferOverreadException>(() => reader.Peek());
            reader.Dispose();
            reader.Dispose();
            Assert.Throws<ObjectDisposedException>(() => reader.ReadByte());
        }

        [Fact]
        public void MappedReader_DisposalReleasesFile()
        {
            using var file = new TemporarySparseFile();
            File.WriteAllBytes(file.Path, new byte[] { 1 });
            var reader = new DwarfMemoryReader(file.Path, 0, 1);
            reader.Dispose();
            using FileStream writable = File.Open(file.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            writable.WriteByte(2);
        }

        [Fact]
        public void ArrayReader_HandlesRepeatedDisposalWithoutMappedView()
        {
            using var reader = new DwarfMemoryReader(new byte[] { 1 });
            reader.ReadByte().Should().Be(1);

            reader.Dispose();
            reader.Dispose();

            Assert.Throws<ObjectDisposedException>(() => reader.ReadByte());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompilationUnits_EmptyDebugInfoReturnsNoUnits(bool useMappedReader)
        {
            string originalPath = System.IO.Path.Combine(ElfBinaryTests.TestData,
                "Dwarf/DebugFileType/BinaryDirectory/gcc.objcopy.stripall.addgnudebuglink.full");
            using var binary = new ElfBinary(new Uri(originalPath));
            using var file = new TemporarySparseFile();
            using DwarfMemoryReader data = useMappedReader
                ? new DwarfMemoryReader(file.Path, 0, 0)
                : new DwarfMemoryReader(Array.Empty<byte>());
            using var strings = new DwarfMemoryReader(binary.DebugDataStrings);

            DwarfSymbolProvider.ParseAllCompilationUnits(binary, data, binary.DebugDataDescription,
                strings, binary.DebugLineStrings, binary.DebugStringOffsets, binary.NormalizeAddress)
                .Should().BeEmpty();
        }

        [Fact]
        public void CompilationUnits_InvalidMappedUnitLengthPreservesParsedUnits()
        {
            string originalPath = System.IO.Path.Combine(ElfBinaryTests.TestData,
                "Dwarf/DebugFileType/BinaryDirectory/gcc.objcopy.stripall.addgnudebuglink.full");
            using var binary = new ElfBinary(new Uri(originalPath));
            DwarfCompilationUnit expectedUnit = binary.CompilationUnits.Value[0];
            using var file = new TemporarySparseFile();
            using (FileStream stream = File.OpenWrite(file.Path))
            {
                stream.Write(binary.DebugData, 0, checked((int)expectedUnit.NextOffset));
                using var writer = new BinaryWriter(stream);
                writer.Write(16u); // No payload follows this declared unit length.
            }

            using var data = new DwarfMemoryReader(file.Path, 0, expectedUnit.NextOffset + (ulong)sizeof(uint));
            using var abbreviations = new DwarfMemoryReader(binary.DebugDataDescription);
            using var strings = new DwarfMemoryReader(binary.DebugDataStrings);
            using var lineStrings = new DwarfMemoryReader(binary.DebugLineStrings);

            data.Position = expectedUnit.NextOffset;
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                new DwarfCompilationUnit(binary, data, abbreviations, strings, lineStrings,
                    DwarfSymbolProvider.ParseDebugStringOffsets(binary.DebugStringOffsets, binary.Is64bit),
                    binary.NormalizeAddress));
            exception.Message.Should().Be("DWARF compilation unit extends past the end of the section.");

            data.Position = 0;
            List<DwarfCompilationUnit> compilationUnits = DwarfSymbolProvider.ParseAllCompilationUnits(binary, data,
                binary.DebugDataDescription, strings, binary.DebugLineStrings,
                binary.DebugStringOffsets, binary.NormalizeAddress);

            compilationUnits.Should().ContainSingle();
            compilationUnits[0].NextOffset.Should().Be(expectedUnit.NextOffset);
            compilationUnits[0].Symbols.Should().HaveCount(expectedUnit.Symbols.Count());
            compilationUnits[0].SymbolsTree[0].Tag.Should().Be(expectedUnit.SymbolsTree[0].Tag);
            compilationUnits[0].SymbolsTree[0].Attributes[DwarfAttribute.Producer].String
                .Should().Be(expectedUnit.SymbolsTree[0].Attributes[DwarfAttribute.Producer].String);
            data.Position.Should().Be(expectedUnit.NextOffset + sizeof(uint));
        }

        [Theory]
        [InlineData("Dwarf/DebugFileType/BinaryDirectory/gcc.objcopy.stripall.addgnudebuglink.full", ".debug_info")]
        [InlineData("Dwarf/DebugFileType/AnotherLocalSymbolDirectory/gcc.gsplitdwarf.5.dwo", ".debug_info.dwo")]
        public void ElfBinary_ParsesOversizedDebugInfoWithoutDiscardingCompilationUnits(string relativePath, string sectionName)
        {
            string originalPath = System.IO.Path.Combine(ElfBinaryTests.TestData, relativePath);
            using var original = new ElfBinary(new Uri(originalPath));
            original.Valid.Should().BeTrue();
            original.Is64bit.Should().BeTrue();
            byte[] originalDebugInfo = original.DebugData;
            original.CompilationUnits.Value.Should().NotBeEmpty();
            using var file = new TemporarySparseFile();
            File.Copy(originalPath, file.Path, overwrite: true);
            file.MarkSparse();

            ulong sectionSize = (ulong)int.MaxValue + 128;
            using (FileStream stream = File.Open(file.Path, FileMode.Open, FileAccess.ReadWrite))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                stream.Position = 0x28;
                ulong tableOffset = reader.ReadUInt64();
                stream.Position = 0x3A;
                ushort entrySize = reader.ReadUInt16();
                int sectionIndex = original.ELF.Sections.ToList().FindIndex(section => section.Name == sectionName);
                sectionIndex.Should().BeGreaterThan(0);
                ulong newOffset = (ulong)stream.Length;
                stream.SetLength((long)(newOffset + sectionSize));
                stream.Position = (long)newOffset;
                writer.Write(originalDebugInfo);
                stream.Position = (long)tableOffset + sectionIndex * entrySize + 24;
                writer.Write(newOffset);
                writer.Write(sectionSize);
            }

            using var oversized = new ElfBinary(new Uri(file.Path));
            oversized.Valid.Should().BeTrue();
            oversized.IsDebugOnlyFile.Should().Be(original.IsDebugOnlyFile);
            oversized.DebugFileType.Should().Be(original.DebugFileType);
            oversized.CompilationUnits.Value.Should().HaveCount(original.CompilationUnits.Value.Count);
            oversized.CompilationUnits.Value.Select(unit => unit.NextOffset)
                .Should().Equal(original.CompilationUnits.Value.Select(unit => unit.NextOffset));
            oversized.CompilationUnits.Value.Select(unit => unit.Symbols.Count())
                .Should().Equal(original.CompilationUnits.Value.Select(unit => unit.Symbols.Count()));
            oversized.CommandLineInfos.Should().BeEquivalentTo(original.CommandLineInfos,
                options => options.WithStrictOrdering());
            oversized.GetLanguage().Should().Be(original.GetLanguage());
        }

        [Fact]
        public void CompilationUnit_ParsesSymbolsAfterInt32Boundary()
        {
            string originalPath = System.IO.Path.Combine(ElfBinaryTests.TestData,
                "Dwarf/DebugFileType/BinaryDirectory/gcc.objcopy.stripall.addgnudebuglink.full");
            using var original = new ElfBinary(new Uri(originalPath));
            byte[] debugInfo = original.DebugData;
            const uint offset = (uint)int.MaxValue + 16;
            using var file = new TemporarySparseFile();
            using (FileStream stream = File.OpenWrite(file.Path))
            {
                stream.SetLength((long)offset + debugInfo.Length);
                stream.Position = offset;
                stream.Write(debugInfo);
            }

            using var data = new DwarfMemoryReader(file.Path, 0, (ulong)offset + (ulong)debugInfo.Length);
            using var strings = new DwarfMemoryReader(original.DebugDataStrings);
            DwarfCompilationUnit unit = DwarfSymbolProvider.ParseOneCompilationUnitByOffset(original, data,
                original.DebugDataDescription, strings, original.DebugLineStrings,
                original.DebugStringOffsets, original.NormalizeAddress, offset);

            unit.Should().NotBeNull();
            unit.NextOffset.Should().Be(offset + original.CompilationUnits.Value[0].NextOffset);
            unit.Symbols.Should().HaveCount(original.CompilationUnits.Value[0].Symbols.Count());
            unit.SymbolsTree[0].Tag.Should().Be(original.CompilationUnits.Value[0].SymbolsTree[0].Tag);
            unit.SymbolsTree[0].Attributes[DwarfAttribute.Producer].String
                .Should().Be(original.CompilationUnits.Value[0].SymbolsTree[0].Attributes[DwarfAttribute.Producer].String);
        }

        // Large sparse fixtures exercise actual >2 GB offsets without allocating or writing gigabytes.
        private sealed class TemporarySparseFile : IDisposable
        {
            internal TemporarySparseFile()
            {
                Path = System.IO.Path.GetTempFileName();
                MarkSparse();
            }

            internal string Path { get; }

            internal void MarkSparse()
            {
                if (OperatingSystem.IsWindows())
                {
                    using FileStream stream = File.OpenWrite(Path);
                    if (!DeviceIoControl(stream.SafeFileHandle, 0x000900C4, IntPtr.Zero, 0, IntPtr.Zero, 0,
                        out _, IntPtr.Zero))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
            }

            public void Dispose() => File.Delete(Path);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool DeviceIoControl(SafeFileHandle handle, uint controlCode,
                IntPtr input, uint inputSize, IntPtr output, uint outputSize,
                out uint bytesReturned, IntPtr overlapped);
        }
    }
}
