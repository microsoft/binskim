// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// File metadata with line information
    /// </summary>
    public class DwarfFileInformation
    {
        /// <summary>
        /// Gets or sets the file name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the directory.
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the last modification timestamp or 0 if not available.
        /// </summary>
        public byte[] Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the size of the file in bytes.
        /// </summary>
        public ulong Size { get; set; }

        // Gets or sets a 16-byte MD5 digest of the file contents.
        public byte[] MD5 { get; set; }

        /// <summary>
        /// Gets or sets the lines information.
        /// </summary>
        public List<DwarfLineInformation> Lines { get; set; } = new List<DwarfLineInformation>();

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"{Path}";
        }
    }
}
