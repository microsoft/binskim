// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Common information entry shared across frame description entries.
    /// </summary>
    public class DwarfCommonInformationEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfCommonInformationEntry"/> class.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="defaultAddressSize">Default size of the address.</param>
        /// <param name="endPosition">The end position in the memory stream.</param>
        private DwarfCommonInformationEntry(DwarfMemoryReader data, byte defaultAddressSize, int endPosition)
        {
            ParseData(data, defaultAddressSize, endPosition);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfCommonInformationEntry"/> class.
        /// </summary>
        protected DwarfCommonInformationEntry()
        {
        }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        public byte Version { get; set; }

        /// <summary>
        /// Gets or sets the augmentation string.
        /// </summary>
        public string Augmentation { get; set; }

        /// <summary>
        /// Gets or sets the size of the address.
        /// </summary>
        public byte AddressSize { get; set; }

        /// <summary>
        /// Gets or sets the size of the segment selector.
        /// </summary>
        public byte SegmentSelectorSize { get; set; }

        /// <summary>
        /// Gets or sets the code alignment factor.
        /// </summary>
        public ulong CodeAlignmentFactor { get; set; }

        /// <summary>
        /// Gets or sets the data alignment factor.
        /// </summary>
        public ulong DataAlignmentFactor { get; set; }

        /// <summary>
        /// Gets or sets the return address register.
        /// </summary>
        public ulong ReturnAddressRegister { get; set; }

        /// <summary>
        /// Gets or sets the initial instructions stream to be executed before instructions in frame description entry.
        /// </summary>
        public byte[] InitialInstructions { get; set; }

        /// <summary>
        /// Gets or sets the list of frame description entries that share this common information entry.
        /// </summary>
        public List<DwarfFrameDescriptionEntry> FrameDescriptionEntries { get; set; } = new List<DwarfFrameDescriptionEntry>();

        /// <summary>
        /// Parses the specified data for all common information entries and frame description entries.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="defaultAddressSize">Default size of the address.</param>
        /// <returns>All the parsed common information entries</returns>
        public static DwarfCommonInformationEntry[] ParseAll(DwarfMemoryReader data, byte defaultAddressSize)
        {
            Dictionary<int, DwarfCommonInformationEntry> entries = new Dictionary<int, DwarfCommonInformationEntry>();

            while (!data.IsEnd)
            {
                int startPosition = data.Position;
                ulong length = data.ReadLength(out bool is64bit);
                int endPosition = data.Position + (int)length;
                int offset = data.ReadOffset(is64bit);
                DwarfCommonInformationEntry entry;

                if (offset == -1)
                {
                    entry = new DwarfCommonInformationEntry(data, defaultAddressSize, endPosition);
                    entries.Add(startPosition, entry);
                }
                else
                {
                    if (!entries.TryGetValue(offset, out entry))
                    {
                        entry = ParseEntry(data, defaultAddressSize, offset);
                        entries.Add(offset, entry);
                    }

                    DwarfFrameDescriptionEntry description = new DwarfFrameDescriptionEntry(data, entry, endPosition);

                    entry.FrameDescriptionEntries.Add(description);
                }
            }

            return entries.Values.ToArray();
        }

        /// <summary>
        /// Parses the single entry from the specified data.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="defaultAddressSize">Default size of the address.</param>
        /// <param name="startPosition">The start position.</param>
        /// <returns>Parsed common information entry.</returns>
        private static DwarfCommonInformationEntry ParseEntry(DwarfMemoryReader data, byte defaultAddressSize, int startPosition)
        {
            int position = data.Position;

            data.Position = startPosition;

            ulong length = data.ReadLength(out bool is64bit);
            int endPosition = data.Position + (int)length;
            int offset = data.ReadOffset(is64bit);

            if (offset != -1)
            {
                throw new Exception("Expected CommonInformationEntry");
            }

            DwarfCommonInformationEntry entry = new DwarfCommonInformationEntry(data, defaultAddressSize, endPosition);

            data.Position = position;
            return entry;
        }

        /// <summary>
        /// Parses the data for this instance.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="defaultAddressSize">Default size of the address.</param>
        /// <param name="endPosition">The end position.</param>
        private void ParseData(DwarfMemoryReader data, byte defaultAddressSize, int endPosition)
        {
            Version = data.ReadByte();
            Augmentation = data.ReadString();
            if (!string.IsNullOrEmpty(Augmentation))
            {
                AddressSize = 4;
                SegmentSelectorSize = 0;
                CodeAlignmentFactor = 0;
                DataAlignmentFactor = 0;
                ReturnAddressRegister = 0;
            }
            else
            {
                if (Version >= 4)
                {
                    AddressSize = data.ReadByte();
                    SegmentSelectorSize = data.ReadByte();
                }
                else
                {
                    AddressSize = defaultAddressSize;
                    SegmentSelectorSize = 0;
                }
                CodeAlignmentFactor = data.ULEB128();
                DataAlignmentFactor = data.ULEB128();
                ReturnAddressRegister = data.ULEB128();
            }
            InitialInstructions = data.ReadBlock((uint)(endPosition - data.Position));
        }
    }

    /// <summary>
    /// Input data structure for parsing exception handling frames stream.
    /// </summary>
    internal struct DwarfExceptionHandlingFrameParsingInput
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfExceptionHandlingFrameParsingInput"/> class.
        /// </summary>
        /// <param name="image">The dwarf image used to initialize input data.</param>
        public DwarfExceptionHandlingFrameParsingInput(IDwarfBinary image)
            : this()
        {
            DefaultAddressSize = (byte)(image.Is64bit ? 8 : 4);
            PcRelativeAddress = image.EhFrameAddress;
            TextAddress = image.TextSectionAddress;
            DataAddress = image.DataSectionAddress;
        }

        /// <summary>
        /// Deafult address size that should be used.
        /// </summary>
        public byte DefaultAddressSize;

        /// <summary>
        /// Address of the .eh_frame section when loaded into memory.
        /// </summary>
        public ulong PcRelativeAddress;

        /// <summary>
        /// Address of the .text section when loaded into memory.
        /// </summary>
        public ulong TextAddress;

        /// <summary>
        /// Address of the .data section when loaded into memory.
        /// </summary>
        public ulong DataAddress;
    }

    /// <summary>
    /// Common information entry shared across frame description entries.
    /// This class is being parsed from <see cref="IDwarfBinary.EhFrame"/> stream.
    /// </summary>
    internal class DwarfExceptionHandlingCommonInformationEntry : DwarfCommonInformationEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfExceptionHandlingCommonInformationEntry"/> class.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="endPosition">The end position in the memory stream.</param>
        /// <param name="input">The input data for parsing configuration.</param>
        public DwarfExceptionHandlingCommonInformationEntry(DwarfMemoryReader data, int endPosition, DwarfExceptionHandlingFrameParsingInput input)
        {
            ParseData(data, endPosition, input);
        }

        /// <summary>
        /// Encoding used for storing language specific data area.
        /// </summary>
        public DwarfExceptionHandlingEncoding LanguageSpecificDataAreaEncoding { get; set; } = DwarfExceptionHandlingEncoding.Omit;

        /// <summary>
        /// Encoding used for storing frame description addresses.
        /// </summary>
        public DwarfExceptionHandlingEncoding FrameDescriptionAddressEncoding { get; set; } = DwarfExceptionHandlingEncoding.Omit;

        /// <summary>
        /// Encoding used for storing personality location.
        /// </summary>
        public DwarfExceptionHandlingEncoding PersonalityEncoding { get; set; } = DwarfExceptionHandlingEncoding.Omit;

        /// <summary>
        /// The personality location.
        /// </summary>
        public ulong PersonalityLocation { get; set; } = 0;

        /// <summary>
        /// Flag that indicates if CIE represents a stack frame for the invocation of a signal handler.
        /// When unwinding the stack, signal stack frames are handled slightly differently:
        /// the instruction pointer is assumed to be before the next instruction to execute rather than after it.
        /// </summary>
        public bool StackFrame { get; set; } = false;

        /// <summary>
        /// Parses the specified data for all common information entries and frame description entries.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="input">The input data for parsing configuration.</param>
        /// <returns>All the parsed common information entries</returns>
        public static DwarfExceptionHandlingCommonInformationEntry[] ParseAll(DwarfMemoryReader data, DwarfExceptionHandlingFrameParsingInput input)
        {
            Dictionary<int, DwarfExceptionHandlingCommonInformationEntry> entries = new Dictionary<int, DwarfExceptionHandlingCommonInformationEntry>();

            while (!data.IsEnd)
            {
                int startPosition = data.Position;
                ulong length = data.ReadLength(out bool is64bit);
                int endPosition = data.Position + (int)length;

                if (length == 0 || endPosition >= data.Data.Length)
                {
                    break;
                }

                int offsetBase = data.Position;
                int offset = data.ReadOffset(is64bit);
                DwarfExceptionHandlingCommonInformationEntry entry;

                if (offset == 0)
                {
                    entry = new DwarfExceptionHandlingCommonInformationEntry(data, endPosition, input);
                    entries.Add(startPosition, entry);
                }
                else
                {
                    int entryOffset = offsetBase - offset;

                    if (entryOffset < 0 || entryOffset >= data.Data.Length)
                    {
                        break;
                    }

                    if (!entries.TryGetValue(entryOffset, out entry))
                    {
                        entry = ParseEntry(data, entryOffset, input);
                        entries.Add(entryOffset, entry);
                    }

                    DwarfFrameDescriptionEntry description = ParseDescription(data, entry, endPosition, input);

                    entry.FrameDescriptionEntries.Add(description);
                }
            }

            return entries.Values.ToArray();
        }

        /// <summary>
        /// Parses frame description from the specified data memory reader.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="entry">Common information entry for parsed frame description.</param>
        /// <param name="endPosition">Position in the data reader where parsed frame description ends.</param>
        /// <param name="input">The input data for parsing configuration.</param>
        /// <returns>Parsed frame description.</returns>
        private static DwarfFrameDescriptionEntry ParseDescription(DwarfMemoryReader data, DwarfExceptionHandlingCommonInformationEntry entry, int endPosition, DwarfExceptionHandlingFrameParsingInput input)
        {
            DwarfFrameDescriptionEntry description = new DwarfFrameDescriptionEntry
            {
                InitialLocation = ReadEncodedAddress(data, entry.FrameDescriptionAddressEncoding, input),
                AddressRange = ReadEncodedAddress(data, entry.FrameDescriptionAddressEncoding & DwarfExceptionHandlingEncoding.Mask, input),
                CommonInformationEntry = entry
            };
            int instructionsStart = -1;
            if (entry.Augmentation.Length >= 1 && entry.Augmentation[0] == 'z')
            {
                ulong length = data.ULEB128();

                instructionsStart = data.Position + (int)length;
                if (entry.LanguageSpecificDataAreaEncoding != DwarfExceptionHandlingEncoding.Omit)
                {
                    ReadEncodedAddress(data, entry.LanguageSpecificDataAreaEncoding, input);
                }
            }
            if (instructionsStart >= 0)
            {
                data.Position = instructionsStart;
            }
            description.Instructions = data.ReadBlock((uint)(endPosition - data.Position));
            return description;
        }

        /// <summary>
        /// Reads encoded address from the specified data memory reader.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="encoding">Encoding used for storing address value.</param>
        /// <param name="input">The input data for parsing configuration.</param>
        /// <returns>Decoded address value.</returns>
        private static ulong ReadEncodedAddress(DwarfMemoryReader data, DwarfExceptionHandlingEncoding encoding, DwarfExceptionHandlingFrameParsingInput input)
        {
            bool signExtendValue = false;
            ulong baseAddress = 0;

            switch (encoding & DwarfExceptionHandlingEncoding.Modifiers)
            {
                case DwarfExceptionHandlingEncoding.PcRelative:
                    signExtendValue = true;
                    baseAddress = (ulong)data.Position;
                    if (input.PcRelativeAddress != ulong.MaxValue)
                    {
                        baseAddress += input.PcRelativeAddress;
                    }
                    break;

                case DwarfExceptionHandlingEncoding.TextRelative:
                    signExtendValue = true;
                    if (input.TextAddress != ulong.MaxValue)
                    {
                        baseAddress = input.TextAddress;
                    }
                    break;

                case DwarfExceptionHandlingEncoding.DataRelative:
                    signExtendValue = true;
                    if (input.DataAddress != ulong.MaxValue)
                    {
                        baseAddress = input.DataAddress;
                    }
                    break;

                case DwarfExceptionHandlingEncoding.FunctionRelative:
                    signExtendValue = true;
                    break;

                case DwarfExceptionHandlingEncoding.Aligned:
                {
                    int alignment = data.Position % input.DefaultAddressSize;

                    if (alignment > 0)
                    {
                        data.Position += input.DefaultAddressSize - alignment;
                    }
                }
                break;
            }

            ulong address = 0;

            switch (encoding & DwarfExceptionHandlingEncoding.Mask)
            {
                case DwarfExceptionHandlingEncoding.Signed:
                case DwarfExceptionHandlingEncoding.AbsolutePointer:
                    address = data.ReadUlong(input.DefaultAddressSize);
                    break;

                case DwarfExceptionHandlingEncoding.UnsignedData2:
                    address = data.ReadUshort();
                    break;

                case DwarfExceptionHandlingEncoding.UnsignedData4:
                    address = data.ReadUint();
                    break;

                case DwarfExceptionHandlingEncoding.SignedData8:
                case DwarfExceptionHandlingEncoding.UnsignedData8:
                    address = data.ReadUlong();
                    break;

                case DwarfExceptionHandlingEncoding.Uleb128:
                    address = data.ULEB128();
                    break;

                case DwarfExceptionHandlingEncoding.Sleb128:
                    address = data.SLEB128();
                    break;

                case DwarfExceptionHandlingEncoding.SignedData2:
                    address = (ulong)(long)(short)data.ReadUshort();
                    break;

                case DwarfExceptionHandlingEncoding.SignedData4:
                    address = (ulong)(long)(int)data.ReadUint();
                    break;
            }

            if (signExtendValue && input.DefaultAddressSize < System.Runtime.InteropServices.Marshal.SizeOf(address.GetType()))
            {
                ulong sign_bit = 1UL << ((input.DefaultAddressSize * 8) - 1);

                if ((sign_bit & address) != 0)
                {
                    ulong mask = ~sign_bit + 1;

                    address |= mask;
                }
            }

            return baseAddress + address;
        }

        /// <summary>
        /// Parses the single entry from the specified data.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="startPosition">The start position.</param>
        /// <param name="input">The input data for parsing configuration.</param>
        /// <returns>Parsed common information entry.</returns>
        private static DwarfExceptionHandlingCommonInformationEntry ParseEntry(DwarfMemoryReader data, int startPosition, DwarfExceptionHandlingFrameParsingInput input)
        {
            int position = data.Position;

            data.Position = startPosition;

            ulong length = data.ReadLength(out bool is64bit);
            int endPosition = data.Position + (int)length;
            int offset = data.ReadOffset(is64bit);

            if (offset != 0)
            {
                throw new Exception("Expected CommonInformationEntry");
            }

            DwarfExceptionHandlingCommonInformationEntry entry = new DwarfExceptionHandlingCommonInformationEntry(data, endPosition, input);

            data.Position = position;
            return entry;
        }

        /// <summary>
        /// Parses the data for this instance.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="endPosition">The end position.</param>
        /// <param name="input">The input data for parsing configuration.</param>
        private void ParseData(DwarfMemoryReader data, int endPosition, DwarfExceptionHandlingFrameParsingInput input)
        {
            Version = data.ReadByte();
            Augmentation = data.ReadString();
            CodeAlignmentFactor = data.ULEB128();
            DataAlignmentFactor = data.SLEB128();
            if (Version == 1)
            {
                ReturnAddressRegister = data.ReadByte();
            }
            else
            {
                ReturnAddressRegister = data.ULEB128();
            }
            AddressSize = input.DefaultAddressSize;
            SegmentSelectorSize = 0;
            int instructionsStart = -1;

            for (int i = 0; i < Augmentation.Length; i++)
            {
                if (Augmentation[i] == 'z')
                {
                    ulong length = data.ULEB128();
                    instructionsStart = data.Position + (int)length;
                }
                else if (Augmentation[i] == 'L')
                {
                    LanguageSpecificDataAreaEncoding = (DwarfExceptionHandlingEncoding)data.ReadByte();
                }
                else if (Augmentation[i] == 'R')
                {
                    FrameDescriptionAddressEncoding = (DwarfExceptionHandlingEncoding)data.ReadByte();
                }
                else if (Augmentation[i] == 'S')
                {
                    StackFrame = true;
                }
                else if (Augmentation[i] == 'P')
                {
                    PersonalityEncoding = (DwarfExceptionHandlingEncoding)data.ReadByte();
                    PersonalityLocation = ReadEncodedAddress(data, PersonalityEncoding, input);
                }
                else
                {
                    break;
                }
            }
            if (instructionsStart >= 0)
            {
                data.Position = instructionsStart;
            }
            InitialInstructions = data.ReadBlock((uint)(endPosition - data.Position));
        }
    }
}
