// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Unit tests for <see cref="DwarfCommonInformationEntry"/> and its interaction with
    /// <see cref="DwarfFrameDescriptionEntry"/> when parsing .debug_frame style data.
    /// </summary>
    public class DwarfCommonInformationEntryTests
    {
        [Fact]
        public void ParseAll_WithNonEmptyAugmentation_UsesFixedDefaultsAndReadsInstructions()
        {
            // Layout for a single Common Information Entry (CIE):
            //   length (4 bytes)
            //   offset (4 bytes)            -- unused for CIE
            //   second offset = -1 (4 bytes) -- sentinel indicating CIE
            //   version (1 byte)
            //   augmentation string "x" + null terminator (2 bytes)
            //   initial instructions (2 bytes)

            byte[] data = new byte[]
            {
                // length = 13 bytes following this field (4 + 4 + 1 + 2 + 2)
                0x0D, 0x00, 0x00, 0x00,

                // first offset (ignored for CIE)
                0x00, 0x00, 0x00, 0x00,

                // second offset = -1 (0xFFFFFFFF) marks this as CIE
                0xFF, 0xFF, 0xFF, 0xFF,

                // version
                0x03,

                // augmentation string "x" (ASCII 0x78) + null terminator
                0x78, 0x00,

                // initial instructions
                0xAA, 0xBB,
            };

            using var reader = new DwarfMemoryReader(data);

            DwarfCommonInformationEntry[] entries = DwarfCommonInformationEntry.ParseAll(reader, defaultAddressSize: 8);

            entries.Should().HaveCount(1);
            DwarfCommonInformationEntry entry = entries[0];

            entry.Version.Should().Be(3);
            entry.Augmentation.Should().Be("x");

            // Non-empty augmentation path uses fixed defaults.
            entry.AddressSize.Should().Be(4);
            entry.SegmentSelectorSize.Should().Be(0);
            entry.CodeAlignmentFactor.Should().Be(0);
            entry.DataAlignmentFactor.Should().Be(0);
            entry.ReturnAddressRegister.Should().Be(0);

            entry.InitialInstructions.Should().Equal(0xAA, 0xBB);
            entry.FrameDescriptionEntries.Should().BeEmpty();
        }

        [Fact]
        public void ParseAll_WithEmptyAugmentationAndVersionLessThanFour_UsesDefaultAddressSize()
        {
            // Version < 4 and empty augmentation string should use the provided
            // defaultAddressSize and read alignment factors and return address
            // via ULEB128.

            // Body layout after the two offsets:
            //   version (1 byte)
            //   augmentation string "" (null terminator only, 1 byte)
            //   code alignment factor (ULEB128, 1 byte)
            //   data alignment factor (ULEB128, 1 byte)
            //   return address register (ULEB128, 1 byte)
            //   initial instructions (1 byte)
            // Total body = 6 bytes, plus two 4-byte offsets = 14.

            byte[] data = new byte[]
            {
                // length = 14 bytes following this field
                0x0E, 0x00, 0x00, 0x00,

                // first offset (ignored for CIE)
                0x00, 0x00, 0x00, 0x00,

                // second offset = -1 (CIE sentinel)
                0xFF, 0xFF, 0xFF, 0xFF,

                // version = 3 (< 4)
                0x03,

                // empty augmentation string (just null terminator)
                0x00,

                // CodeAlignmentFactor = 1, DataAlignmentFactor = 2, ReturnAddressRegister = 3
                0x01, 0x02, 0x03,

                // initial instructions
                0xCC,
            };

            using var reader = new DwarfMemoryReader(data);

            DwarfCommonInformationEntry[] entries = DwarfCommonInformationEntry.ParseAll(reader, defaultAddressSize: 8);

            entries.Should().HaveCount(1);
            DwarfCommonInformationEntry entry = entries[0];

            entry.Version.Should().Be(3);
            entry.Augmentation.Should().BeEmpty();

            entry.AddressSize.Should().Be(8); // defaultAddressSize
            entry.SegmentSelectorSize.Should().Be(0);
            entry.CodeAlignmentFactor.Should().Be(1);
            entry.DataAlignmentFactor.Should().Be(2);
            entry.ReturnAddressRegister.Should().Be(3);

            entry.InitialInstructions.Should().Equal(0xCC);
        }

        [Fact]
        public void ParseAll_WithEmptyAugmentationAndVersionFour_ReadsExplicitAddressAndSegmentSizes()
        {
            // Version >= 4 with empty augmentation string should read AddressSize
            // and SegmentSelectorSize explicitly from the stream before the
            // alignment factors and return address register.

            // Body layout after the two offsets:
            //   version (1 byte)
            //   augmentation string "" (1 byte)
            //   address size (1 byte)
            //   segment selector size (1 byte)
            //   code alignment factor (ULEB128, 1 byte)
            //   data alignment factor (ULEB128, 1 byte)
            //   return address register (ULEB128, 1 byte)
            //   initial instructions (1 byte)
            // Total body = 8 bytes, plus two 4-byte offsets = 16.

            byte[] data = new byte[]
            {
                // length = 16 bytes following this field
                0x10, 0x00, 0x00, 0x00,

                // first offset (ignored for CIE)
                0x00, 0x00, 0x00, 0x00,

                // second offset = -1 (CIE sentinel)
                0xFF, 0xFF, 0xFF, 0xFF,

                // version = 4
                0x04,

                // empty augmentation string
                0x00,

                // explicit address and segment selector sizes
                0x08, // AddressSize
                0x01, // SegmentSelectorSize

                // CodeAlignmentFactor = 2, DataAlignmentFactor = 3, ReturnAddressRegister = 4
                0x02, 0x03, 0x04,

                // initial instructions
                0xDD,
            };

            using var reader = new DwarfMemoryReader(data);

            DwarfCommonInformationEntry[] entries = DwarfCommonInformationEntry.ParseAll(reader, defaultAddressSize: 4);

            entries.Should().HaveCount(1);
            DwarfCommonInformationEntry entry = entries[0];

            entry.Version.Should().Be(4);
            entry.Augmentation.Should().BeEmpty();

            entry.AddressSize.Should().Be(8);
            entry.SegmentSelectorSize.Should().Be(1);
            entry.CodeAlignmentFactor.Should().Be(2);
            entry.DataAlignmentFactor.Should().Be(3);
            entry.ReturnAddressRegister.Should().Be(4);

            entry.InitialInstructions.Should().Equal(0xDD);
        }

        [Fact]
        public void ParseAll_WithFdeFollowingCie_AttachesFrameDescriptionEntryAndParsesAddresses()
        {
            // This test crafts a stream with one Common Information Entry (CIE)
            // followed by a Frame Description Entry (FDE) that references it.
            // It verifies that:
            //   - the FDE is associated with the CIE via FrameDescriptionEntries
            //   - InitialLocation, AddressRange, and Instructions are parsed
            //     according to the CIE's AddressSize and the record length.

            // First record: CIE with version 3, empty augmentation, defaultAddressSize = 8.
            // Body after two offsets:
            //   version (1)
            //   augmentation "" (1)
            //   CodeAlignmentFactor (ULEB128 0)
            //   DataAlignmentFactor (ULEB128 0)
            //   ReturnAddressRegister (ULEB128 0)
            //   InitialInstructions (0 bytes)
            // Body length = 5, plus 8 bytes of offsets = 13.

            byte[] cie = new byte[]
            {
                // length = 13 bytes following this field
                0x0D, 0x00, 0x00, 0x00,

                // first offset (ignored for CIE)
                0x00, 0x00, 0x00, 0x00,

                // second offset = -1 (CIE sentinel)
                0xFF, 0xFF, 0xFF, 0xFF,

                // version = 3 (< 4)
                0x03,

                // empty augmentation
                0x00,

                // CodeAlignmentFactor = 0, DataAlignmentFactor = 0, ReturnAddressRegister = 0
                0x00, 0x00, 0x00,
            };

            // Second record: FDE that references the CIE at offset 0.
            // Body after two offsets:
            //   InitialLocation (8 bytes, LE)
            //   AddressRange (8 bytes, LE)
            //   Instructions (2 bytes)
            // Body length = 18, plus 8 bytes of offsets = 26.

            ulong initialLocation = 0x1122334455667788UL;
            ulong addressRange = 0x8877665544332211UL;

            byte[] fde = new byte[4 + 4 + 4 + 8 + 8 + 2];

            int index = 0;

            // length = 26
            fde[index++] = 0x1A; fde[index++] = 0x00; fde[index++] = 0x00; fde[index++] = 0x00;

            // first offset = 0 (reference to CIE start position)
            fde[index++] = 0x00; fde[index++] = 0x00; fde[index++] = 0x00; fde[index++] = 0x00;

            // second offset != -1 so this is treated as FDE
            fde[index++] = 0x00; fde[index++] = 0x00; fde[index++] = 0x00; fde[index++] = 0x00;

            // InitialLocation (8 bytes LE)
            Array.Copy(BitConverter.GetBytes(initialLocation), 0, fde, index, 8);
            index += 8;

            // AddressRange (8 bytes LE)
            Array.Copy(BitConverter.GetBytes(addressRange), 0, fde, index, 8);
            index += 8;

            // Instructions (2 bytes)
            fde[index++] = 0xAA;
            fde[index++] = 0xBB;

            // Combined stream: CIE followed by FDE.
            byte[] data = new byte[cie.Length + fde.Length];
            Array.Copy(cie, 0, data, 0, cie.Length);
            Array.Copy(fde, 0, data, cie.Length, fde.Length);

            using var reader = new DwarfMemoryReader(data);

            DwarfCommonInformationEntry[] entries = DwarfCommonInformationEntry.ParseAll(reader, defaultAddressSize: 8);

            entries.Should().HaveCount(1);
            DwarfCommonInformationEntry entry = entries[0];

            entry.AddressSize.Should().Be(8);
            entry.FrameDescriptionEntries.Should().HaveCount(1);

            DwarfFrameDescriptionEntry description = entry.FrameDescriptionEntries[0];

            description.CommonInformationEntry.Should().BeSameAs(entry);
            description.InitialLocation.Should().Be(initialLocation);
            description.AddressRange.Should().Be(addressRange);
            description.Instructions.Should().Equal(0xAA, 0xBB);
        }
    }
}
