// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public static class StreamExtensionMethods
    {
        public static void Read<T>(this Stream stream, Span<T> buffer)
            where T : unmanaged
        {
            Span<byte> bytesBuffer = MemoryMarshal.AsBytes(buffer);

            if (stream.Read(bytesBuffer) != bytesBuffer.Length)
            {
                if (buffer.Length == 1)
                { throw new InvalidOperationException($"The stream does not contain enough data to read in a {typeof(T)}"); }
                else
                { throw new InvalidOperationException($"The stream does not contain enough data to read in {buffer.Length} {typeof(T)} elements"); }
            }
        }

        public unsafe static T Read<T>(this Stream stream)
            where T : unmanaged
        {
            Span<T> result = stackalloc T[1];
            stream.Read(result);
            return result[0];
        }

        public static T[] Read<T>(this Stream stream, int count)
            where T : unmanaged
        {
            T[] result = new T[count];
            stream.Read<T>(result);
            return result;
        }

        public static string ReadAscii(this Stream stream, int byteCount)
        {
            Span<byte> bytes = stackalloc byte[byteCount];
            stream.Read<byte>(bytes);
            return Encoding.ASCII.GetString(bytes);
        }

        public static string ReadUtf8(this Stream stream, int byteCount)
        {
            Span<byte> bytes = stackalloc byte[byteCount];
            stream.Read<byte>(bytes);
            return Encoding.UTF8.GetString(bytes);
        }

        public static string ReadAsciiNullTerminated(this Stream stream)
        {
            long start = stream.Position;
            int stringLength = 0;

            while (true)
            {
                int byteValue = stream.ReadByte();

                if (byteValue == 0)
                { break; }
                else if (byteValue == -1)
                { throw new InvalidOperationException("The stream does not contain a null-terminated ASCII string."); }

                stringLength++;
            }

            stream.Position = start;
            string ret = stringLength == 0 ? string.Empty : stream.ReadAscii(stringLength);
            int nullTerminator = stream.ReadByte();
            Debug.Assert(nullTerminator == 0);
            return ret;
        }

        public static string ReadUtf8NullTerminated(this Stream stream)
        {
            long start = stream.Position;
            int stringLength = 0;

            while (true)
            {
                int byteValue = stream.ReadByte();

                if (byteValue == 0)
                { break; }
                else if (byteValue == -1)
                { throw new InvalidOperationException("The stream does not contain a null-terminated ASCII string."); }

                stringLength++;
            }

            stream.Position = start;
            string ret = stringLength == 0 ? string.Empty : stream.ReadUtf8(stringLength);
            int nullTerminator = stream.ReadByte();
            Debug.Assert(nullTerminator == 0);
            return ret;
        }
    }
}
