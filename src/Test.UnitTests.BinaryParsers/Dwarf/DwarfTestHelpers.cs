// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Shared encoding helpers for DWARF unit tests.
    /// </summary>
    internal static class DwarfTestHelpers
    {
        /// <summary>
        /// Encodes an unsigned LEB128 value into bytes.
        /// </summary>
        internal static byte[] EncodeULEB128(ulong value)
        {
            var bytes = new List<byte>();
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0)
                {
                    b |= 0x80;
                }

                bytes.Add(b);
            } while (value != 0);
            return bytes.ToArray();
        }

        /// <summary>
        /// Encodes a signed LEB128 value into bytes.
        /// </summary>
        internal static byte[] EncodeSLEB128(int value)
        {
            var bytes = new List<byte>();
            bool more = true;
            while (more)
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if ((value == 0 && (b & 0x40) == 0) || (value == -1 && (b & 0x40) != 0))
                {
                    more = false;
                }
                else
                {
                    b |= 0x80;
                }

                bytes.Add(b);
            }
            return bytes.ToArray();
        }
    }
}
