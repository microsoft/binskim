// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    internal static class LEB128Helper
    {
        public static ulong ReadUnsigned(List<byte> data, ref int index)
        {
            var input = new List<byte>();
            byte chunk;

            do
            {
                chunk = data[index];
                index++;

                input.Add(chunk);
            } while ((chunk & 0x80) > 0);

            return LEB128Helper.DecodeUnsigned(input.ToArray());
        }

        public static long ReadSigned(List<byte> data, ref int index)
        {
            var input = new List<byte>();
            byte chunk;

            do
            {
                chunk = data[index];
                index++;

                input.Add(chunk);
            } while ((chunk & 0x80) > 0);

            return LEB128Helper.DecodeSigned(input.ToArray());
        }

        private static byte[] EncodeUnsigned(ulong input)
        {
            var output = new List<byte>();

            while (input != 0)
            {
                byte chunk = (byte)(input & 0x7F);
                input >>= 7;

                if (input != 0)
                {
                    chunk |= 0x80;
                }

                output.Add(chunk);
            }

            return output.ToArray();
        }

        private static byte[] EncodeSigned(long input)
        {
            var output = new List<byte>();
            bool more = true;

            while (more)
            {
                byte chunk = (byte)(input & 0x7F);
                input >>= 7;

                // Sign bit of byte is 2nd high order bit (0x40)
                if ((input == 0 && (chunk & 0x40) == 0) ||
                    (input == -1 && (chunk & 0x40) > 0))
                {
                    more = false;
                }
                else
                {
                    chunk |= 0x80;
                }

                output.Add(chunk);
            }

            return output.ToArray();
        }

        private static ulong DecodeUnsigned(byte[] input)
        {
            ulong output = 0;
            int shift = 0;

            for (int i = 0; i < input.Length; i++)
            {
                byte chunk = input[i];
                output |= ((ulong)chunk & 0x7F) << shift;
                shift += 7;

                if ((chunk & 0x80) == 0) { break; }
            }

            return output;
        }

        private static long DecodeSigned(byte[] input)
        {
            long output = 0;
            int shift = 0;
            int size = 64;
            byte chunk = 0;

            for (int i = 0; i < input.Length; i++)
            {
                chunk = input[i];
                output |= ((long)chunk & 0x7F) << shift;
                shift += 7;

                if ((chunk & 0x80) == 0) { break; }
            }

            // Sign bit of byte is 2nd high order bit (0x40)
            if ((shift < size) && ((chunk & 0x40) != 0))
            {
                // Sign extend
                output |= -((long)1 << shift);
            }

            return output;
        }
    }
}
