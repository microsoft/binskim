// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Reads individual null-terminated strings directly from a DWARF section
    /// without loading the entire section into a single byte array.
    /// </summary>
    internal sealed class DwarfFileStringReader : IDwarfStringReader
    {
        internal DwarfFileStringReader(string path, ulong sectionOffset, ulong sectionSize)
            : this(File.OpenRead(path), sectionOffset, sectionSize, leaveOpen: false)
        {
        }

        internal DwarfFileStringReader(Stream stream, ulong sectionOffset, ulong sectionSize, bool leaveOpen)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.sectionOffset = checked((long)sectionOffset);
            this.sectionSize = sectionSize;
            this.leaveOpen = leaveOpen;
        }

        public string ReadString(uint position)
        {
            if (position >= this.sectionSize)
            {
                return string.Empty;
            }

            this.stream.Seek(checked(this.sectionOffset + position), SeekOrigin.Begin);

            var bytes = new List<byte>();
            while ((ulong)position + (ulong)bytes.Count < this.sectionSize)
            {
                int value = this.stream.ReadByte();
                if (value <= 0)
                {
                    break;
                }

                bytes.Add((byte)value);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public void Dispose()
        {
            if (!this.leaveOpen)
            {
                this.stream.Dispose();
            }
        }

        private readonly bool leaveOpen;
        private readonly long sectionOffset;
        private readonly ulong sectionSize;
        private readonly Stream stream;
    }
}
