// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable
{
    public class CoffStringTable
    {
        public CoffStringTable(Stream stream)
        {
            uint stringTableSize = stream.Read<uint>();
            stream.Position -= sizeof(uint); // Offsets and the size include the size field
            StringTable = stream.Read<byte>(checked((int)stringTableSize));

            if (stringTableSize > sizeof(uint) && StringTable[stringTableSize - 1] != 0)
            {
                // The string table is malformed 
                this.StringTable = Array.Empty<byte>();
            }
        }

        public byte[] StringTable { get; set; } = Array.Empty<byte>();

        public string GetString(int offset)
        {
            if (StringTable.Length == 0 || offset < 0 || offset >= StringTable.Length)
            {
                return null;
            }

            int end = offset;
            for (; end < StringTable.Length && StringTable[end] != 0; end++)
            { }

            int length = end - offset;
            return Encoding.ASCII.GetString(StringTable, offset, length);
        }
    }
}
