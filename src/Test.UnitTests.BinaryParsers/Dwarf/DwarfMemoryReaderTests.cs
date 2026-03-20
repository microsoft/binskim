// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public class DwarfMemoryReaderTests
    {
        // ---- ReadThreeBytes: 3-byte little-endian read ----

        [Theory]
        [InlineData(0x56, 0x34, 0x12, 0x123456u)] // standard LE
        [InlineData(0x00, 0x00, 0x00, 0x000000u)] // zero
        [InlineData(0xFF, 0xFF, 0xFF, 0xFFFFFFu)] // max 3-byte value
        [InlineData(0xAB, 0x00, 0x00, 0x0000ABu)] // only low byte set
        [InlineData(0x00, 0x00, 0xCD, 0xCD0000u)] // only high byte set
        public void ReadThreeBytes_ReadsLittleEndian(byte b0, byte b1, byte b2, uint expected)
        {
            using var reader = new DwarfMemoryReader(new byte[] { b0, b1, b2 });

            reader.ReadThreeBytes().Should().Be(expected);
            reader.Position.Should().Be(3);
        }

        [Fact]
        public void ReadThreeBytes_ConsecutiveReads_AdvancePosition()
        {
            using var reader = new DwarfMemoryReader(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });

            reader.ReadThreeBytes().Should().Be(0x030201u);
            reader.ReadThreeBytes().Should().Be(0x060504u);
            reader.Position.Should().Be(6);
        }

        // ---- Fixed-size integer reads ----

        [Theory]
        [InlineData(new byte[] { 0x42 }, 0x42ul, 1u)]  // ReadByte
        [InlineData(new byte[] { 0x34, 0x12 }, 0x1234ul, 2u)]  // ReadUshort
        [InlineData(new byte[] { 0x78, 0x56, 0x34, 0x12 }, 0x12345678ul, 4u)]  // ReadUint
        [InlineData(new byte[] { 0xEF, 0xCD, 0xAB, 0x90, 0x78, 0x56, 0x34, 0x12 }, 0x1234567890ABCDEFul, 8u)]  // ReadUlong
        public void ReadUlong_WithSize_ReadsLittleEndian(byte[] data, ulong expected, uint size)
        {
            using var reader = new DwarfMemoryReader(data);

            reader.ReadUlong(size).Should().Be(expected);
            reader.Position.Should().Be(size);
        }

        // ---- ReadOffset: 32-bit vs 64-bit ----

        [Theory]
        [InlineData(false, new byte[] { 0x78, 0x56, 0x34, 0x12 }, 0x12345678, 4u)]
        [InlineData(true, new byte[] { 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 8, 8u)]
        public void ReadOffset_ReadsCorrectWidth(bool is64bit, byte[] data, int expected, uint expectedPosition)
        {
            using var reader = new DwarfMemoryReader(data);

            reader.ReadOffset(is64bit).Should().Be(expected);
            reader.Position.Should().Be(expectedPosition);
        }

        // ---- ULEB128: unsigned variable-length encoding ----

        [Theory]
        [MemberData(nameof(ULEB128TestData))]
        public void ULEB128_ReadsCorrectly(byte[] data, ulong expected, uint expectedPosition)
        {
            using var reader = new DwarfMemoryReader(data);

            reader.ULEB128().Should().Be(expected);
            reader.Position.Should().Be(expectedPosition);
        }

        public static IEnumerable<object[]> ULEB128TestData => new[]
        {
            new object[] { new byte[] { 0x00 },             0ul,      1u },  // zero
            new object[] { new byte[] { 0x25 },             37ul,     1u },  // single byte
            new object[] { new byte[] { 0x80, 0x01 },       128ul,    2u },  // two bytes
            new object[] { new byte[] { 0xE5, 0x8E, 0x26 }, 624485ul, 3u },  // three bytes (DWARF spec example)

            // Non-canonical (overlong/padded) encodings — valid per DWARF spec, compilers may emit these
            new object[] { new byte[] { 0x80, 0x00 },                   0ul,  2u },  // zero padded to 2 bytes
            new object[] { new byte[] { 0x80, 0x80, 0x00 },             0ul,  3u },  // zero padded to 3 bytes
            new object[] { new byte[] { 0x81, 0x00 },                   1ul,  2u },  // 1 padded to 2 bytes
            new object[] { new byte[] { 0x81, 0x80, 0x00 },             1ul,  3u },  // 1 padded to 3 bytes
            new object[] { new byte[] { 0xA5, 0x80, 0x00 },             37ul, 3u },  // 37 padded to 3 bytes (0x25 → 0xA5,0x80,0x00)
            new object[] { new byte[] { 0xE5, 0x8E, 0xA6, 0x00 },  624485ul, 4u },  // DWARF spec example padded to 4 bytes
        };

        // ---- SLEB128: signed variable-length encoding ----

        [Theory]
        [MemberData(nameof(SLEB128TestData))]
        public void SLEB128_ReadsCorrectly(byte[] data, int expected, uint expectedPosition)
        {
            using var reader = new DwarfMemoryReader(data);

            ((int)reader.SLEB128()).Should().Be(expected);
            reader.Position.Should().Be(expectedPosition);
        }

        public static IEnumerable<object[]> SLEB128TestData => new[]
        {
            new object[] { new byte[] { 0x02 },       2,    1u },  // positive single byte
            new object[] { new byte[] { 0x7E },       -2,   1u },  // negative single byte
            new object[] { new byte[] { 0x80, 0x7F }, -128, 2u },  // negative two bytes
        };

        // ---- ReadLength: 32-bit vs 64-bit (sentinel-based) ----

        [Theory]
        [MemberData(nameof(ReadLengthTestData))]
        public void ReadLength_ReadsCorrectly(byte[] data, ulong expectedLength, bool expectedIs64bit, uint expectedPosition)
        {
            using var reader = new DwarfMemoryReader(data);

            reader.ReadLength(out bool is64bit).Should().Be(expectedLength);
            is64bit.Should().Be(expectedIs64bit);
            reader.Position.Should().Be(expectedPosition);
        }

        public static IEnumerable<object[]> ReadLengthTestData => new[]
        {
            new object[] { new byte[] { 0x10, 0x00, 0x00, 0x00 },                                                         16ul, false, 4u  },  // 32-bit
            new object[] { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },         32ul, true,  12u },  // 64-bit (0xFFFFFFFF sentinel)
        };

        // ---- ReadString, ReadBlock, Peek, IsEnd ----

        [Fact]
        public void ReadString_ReadsNullTerminated()
        {
            using var reader = new DwarfMemoryReader(new byte[] { 0x48, 0x69, 0x00, 0xFF }); // "Hi\0\xFF"

            reader.ReadString().Should().Be("Hi");
            reader.Position.Should().Be(3);
        }

        [Fact]
        public void ReadBlock_ReadsCorrectBytes()
        {
            using var reader = new DwarfMemoryReader(new byte[] { 0x0A, 0x0B, 0x0C, 0x0D, 0x0E });

            reader.ReadBlock(3).Should().Equal(0x0A, 0x0B, 0x0C);
            reader.Position.Should().Be(3);
        }

        [Fact]
        public void IsEnd_ReturnsTrueAtEnd()
        {
            using var reader = new DwarfMemoryReader(new byte[] { 0x01 });

            reader.IsEnd.Should().BeFalse();
            reader.ReadByte();
            reader.IsEnd.Should().BeTrue();
        }

        [Fact]
        public void Peek_DoesNotAdvancePosition()
        {
            using var reader = new DwarfMemoryReader(new byte[] { 0x42, 0x43 });

            reader.Peek().Should().Be(0x42);
            reader.Position.Should().Be(0);
        }
    }
}
