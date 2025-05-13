// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// DWARF frame description entry.
    /// </summary>
    public class DwarfFrameDescriptionEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfFrameDescriptionEntry"/> class.
        /// </summary>
        /// <param name="data">The data memory reader.</param>
        /// <param name="commonInformationEntry">The common information entry.</param>
        /// <param name="endPosition">The end position.</param>
        public DwarfFrameDescriptionEntry(DwarfMemoryReader data, DwarfCommonInformationEntry commonInformationEntry, uint endPosition)
        {
            CommonInformationEntry = commonInformationEntry;
            ParseData(data, endPosition);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfFrameDescriptionEntry"/> class.
        /// </summary>
        internal DwarfFrameDescriptionEntry()
        {
        }

        /// <summary>
        /// Gets or sets the initial location.
        /// </summary>
        public ulong InitialLocation { get; set; }

        /// <summary>
        /// Gets or sets the address range.
        /// </summary>
        public ulong AddressRange { get; set; }

        /// <summary>
        /// Gets or sets the instructions.
        /// </summary>
        public byte[] Instructions { get; set; }

        /// <summary>
        /// Gets or sets the common information entry.
        /// </summary>
        public DwarfCommonInformationEntry CommonInformationEntry { get; set; }

        /// <summary>
        /// Parses the data for this instance.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="endPosition">The end position.</param>
        private void ParseData(DwarfMemoryReader data, uint endPosition)
        {
            InitialLocation = data.ReadUlong(CommonInformationEntry.AddressSize);
            AddressRange = data.ReadUlong(CommonInformationEntry.AddressSize);
            Instructions = data.ReadBlock((uint)(endPosition - data.Position));
        }
    }
}
