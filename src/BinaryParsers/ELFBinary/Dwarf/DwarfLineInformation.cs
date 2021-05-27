// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Information about line containing compiled code.
    /// </summary>
    public class DwarfLineInformation
    {
        /// <summary>
        /// Gets or sets the file information.
        /// </summary>
        public DwarfFileInformation File { get; set; }

        /// <summary>
        /// Gets or sets the relative module address.
        /// </summary>
        public uint Address { get; set; }

        /// <summary>
        /// Gets or sets the line.
        /// </summary>
        public uint Line { get; set; }

        /// <summary>
        /// Gets or sets the column.
        /// </summary>
        public uint Column { get; set; }
    }
}
