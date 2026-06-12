// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Unit tests for <see cref="DwarfFrameDescriptionEntry"/>.
    /// </summary>
    public class DwarfFrameDescriptionEntryTests
    {
        /// <summary>
        /// Minimal test-only CIE that lets us control AddressSize.
        /// </summary>
        private sealed class TestCommonInformationEntry : DwarfCommonInformationEntry
        {
            public TestCommonInformationEntry(byte addressSize)
            {
                AddressSize = addressSize;
            }
        }

        [Fact]
        public void Constructor_WithFourByteAddressSize_ReadsTwoUintsAndRemainingInstructions()
        {
            // Layout for AddressSize = 4:
            //   InitialLocation (4 bytes LE)
            //   AddressRange (4 bytes LE)
            //   Instructions (3 bytes)
            byte[] data = new byte[]
            {
                0x78, 0x56, 0x34, 0x12, // InitialLocation = 0x12345678
                0xEF, 0xCD, 0xAB, 0x90, // AddressRange = 0x90ABCDEF
                0xAA, 0xBB, 0xCC,       // Instructions
            };

            using var reader = new DwarfMemoryReader(data);
            var cie = new TestCommonInformationEntry(addressSize: 4);

            var entry = new DwarfFrameDescriptionEntry(reader, cie, endPosition: (uint)data.Length);

            entry.InitialLocation.Should().Be(0x12345678UL);
            entry.AddressRange.Should().Be(0x90ABCDEFUL);
            entry.Instructions.Should().Equal(0xAA, 0xBB, 0xCC);
            entry.CommonInformationEntry.Should().BeSameAs(cie);
        }

        [Fact]
        public void Constructor_WithEightByteAddressSize_ReadsTwoUlongsAndRemainingInstructions()
        {
            // Layout for AddressSize = 8:
            //   InitialLocation (8 bytes LE)
            //   AddressRange (8 bytes LE)
            //   Instructions (2 bytes)
            ulong initialLocation = 0x1122334455667788UL;
            ulong addressRange = 0x8877665544332211UL;

            byte[] data = new byte[8 + 8 + 2];
            int index = 0;

            // InitialLocation
            System.Array.Copy(System.BitConverter.GetBytes(initialLocation), 0, data, index, 8);
            index += 8;

            // AddressRange
            System.Array.Copy(System.BitConverter.GetBytes(addressRange), 0, data, index, 8);
            index += 8;

            // Instructions
            data[index++] = 0x01;
            data[index++] = 0x02;

            using var reader = new DwarfMemoryReader(data);
            var cie = new TestCommonInformationEntry(addressSize: 8);

            var entry = new DwarfFrameDescriptionEntry(reader, cie, endPosition: (uint)data.Length);

            entry.InitialLocation.Should().Be(initialLocation);
            entry.AddressRange.Should().Be(addressRange);
            entry.Instructions.Should().Equal(0x01, 0x02);
            entry.CommonInformationEntry.Should().BeSameAs(cie);
        }

        [Fact]
        public void Constructor_WhenEndPositionEqualsCurrentPosition_SetsEmptyInstructions()
        {
            // No instruction bytes after InitialLocation and AddressRange; the
            // ReadBlock call should return an empty array.
            byte[] data = new byte[]
            {
                0x11, 0x22, 0x33, 0x44, // InitialLocation (4 bytes)
                0x55, 0x66, 0x77, 0x88, // AddressRange (4 bytes)
            };

            using var reader = new DwarfMemoryReader(data);
            var cie = new TestCommonInformationEntry(addressSize: 4);

            var entry = new DwarfFrameDescriptionEntry(reader, cie, endPosition: (uint)data.Length);

            entry.Instructions.Should().NotBeNull();
            entry.Instructions.Should().BeEmpty();
        }
    }
}
