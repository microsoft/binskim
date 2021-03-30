// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public class CompilationUnitHeader
    {
        public int Id { get; }
        public uint Length { get; } // Byte length, not including this field
        public ushort Version { get; } // DWARF version
        public uint AbbrevOffset { get; } // Offset into .debug_abbrev
        public byte PtrSize { get; } // Size in bytes of an address

        public CompilationUnitHeader(int id, uint length, ushort version, uint offset, byte size)
        {
            Id = id;
            Length = length;
            Version = version;
            AbbrevOffset = offset;
            PtrSize = size;
        }

        // Parse compilation unit header from .debug_info
        public static CompilationUnitHeader Parse(List<byte> infoData, ref int index, int id)
        {
            int cuhLength = 11;
            byte[] cuhData = infoData.GetRange(index, cuhLength).ToArray();
            index += cuhLength;

            uint length = BitConverter.ToUInt32(cuhData, 0);
            ushort version = BitConverter.ToUInt16(cuhData, 4);
            uint offset = BitConverter.ToUInt32(cuhData, 6);
            byte size = cuhData[10];

            return new CompilationUnitHeader(id, length, version, offset, size);
        }
    }
}
