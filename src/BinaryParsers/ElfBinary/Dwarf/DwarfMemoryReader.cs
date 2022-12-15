// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Simple memory reader that provides specific functionality to read DWARF streams.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class DwarfMemoryReader : IDisposable
    {
        /// <summary>
        /// The pinned data
        /// </summary>
        private GCHandle pinnedData;

        /// <summary>
        /// The pointer of pinned data
        /// </summary>
        private IntPtr pointer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfMemoryReader"/> class.
        /// </summary>
        /// <param name="data">The data.</param>
        public DwarfMemoryReader(byte[] data)
        {
            Data = data;
            Position = 0;
            pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
            pointer = pinnedData.AddrOfPinnedObject();
        }

        /// <summary>
        /// Gets the data buffer.
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// Gets or sets the current position in the stream.
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Gets a value indicating whether stream has reached the end.
        /// </summary>
        /// <value>
        ///   <c>true</c> if stream reached the end; otherwise, <c>false</c>.
        /// </value>
        public bool IsEnd
        {
            get
            {
                return Position >= Data.Length;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            pinnedData.Free();
            pointer = IntPtr.Zero;
        }

        /// <summary>
        /// Peeks next byte in the stream.
        /// </summary>
        public byte Peek()
        {
            return Data[Position];
        }

        /// <summary>
        /// Reads the specified structure from the current position in the stream.
        /// </summary>
        /// <typeparam name="T">Type of the structure to be read</typeparam>
        public T ReadStructure<T>()
        {
            T result = Marshal.PtrToStructure<T>(pointer + Position);

            Position += Marshal.SizeOf<T>();
            return result;
        }

        /// <summary>
        /// Reads the offset from the current position in the stream.
        /// </summary>
        /// <param name="is64bit">if set to <c>true</c> offset is 64 bit.</param>
        public int ReadOffset(bool is64bit)
        {
            return is64bit ? (int)ReadUlong() : (int)ReadUint();
        }

        /// <summary>
        /// Reads the unit length from the current position in the stream.
        /// </summary>
        /// <param name="is64bit">if set to <c>true</c> length was 64 bit.</param>
        public ulong ReadLength(out bool is64bit)
        {
            ulong length = ReadUint();

            if (length == uint.MaxValue)
            {
                is64bit = true;
                length = ReadUlong();
            }
            else
            {
                is64bit = false;
            }

            return length;
        }

        /// <summary>
        /// Reads the string from the current position in the stream.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public string ReadString()
        {
            string result = Marshal.PtrToStringAnsi(pointer + Position);

            Position += result.Length + 1;
            return result;
        }

        /// <summary>
        /// Reads the byte from the current position in the stream.
        /// </summary>
        public byte ReadByte()
        {
            return Data[Position++];
        }

        /// <summary>
        /// Reads the unsigned short from the current position in the stream.
        /// </summary>
        public ushort ReadUshort()
        {
            ushort result = (ushort)Marshal.ReadInt16(pointer, Position);

            Position += 2;
            return result;
        }

        public uint ReadThreeBytes()
        {
            uint firstTwo = (uint)Marshal.ReadInt16(pointer, Position);
            Position += 2;

            return (firstTwo << 16) + ReadByte();
        }

        /// <summary>
        /// Reads the unsigned int from the current position in the stream.
        /// </summary>
        public uint ReadUint()
        {
            uint result = (uint)Marshal.ReadInt32(pointer, Position);

            Position += 4;
            return result;
        }

        /// <summary>
        /// Reads the unsigned long from the current position in the stream.
        /// </summary>
        public ulong ReadUlong()
        {
            ulong result = (ulong)Marshal.ReadInt64(pointer, Position);

            Position += 8;
            return result;
        }

        /// <summary>
        /// Reads the unsigned long of the specified size from the current position in the stream.
        /// </summary>
        /// <param name="size">The size.</param>
        public ulong ReadUlong(uint size)
        {
            return size switch
            {
                1 => ReadByte(),
                2 => ReadUshort(),
                4 => ReadUint(),
                8 => ReadUlong(),
                _ => throw new Exception("Unexpected read size"),
            };
        }

        /// <summary>
        /// Reads unsigned LEB 128 value from the current position in the stream.
        /// </summary>
        public uint LEB128()
        {
            uint x = 0;
            int shift = 0;

            while ((Data[Position] & 0x80) != 0)
            {
                x |= (uint)((Data[Position] & 0x7f) << shift);
                shift += 7;
                Position++;
            }
            x |= (uint)(Data[Position] << shift);
            Position++;
            return x;
        }

        /// <summary>
        /// Reads signed LEB 128 value from the current position in the stream.
        /// </summary>
        public uint SLEB128()
        {
            int x = 0;
            int shift = 0;

            while ((Data[Position] & 0x80) != 0)
            {
                x |= (Data[Position] & 0x7f) << shift;
                shift += 7;
                Position++;
            }
            x |= Data[Position] << shift;
            if ((Data[Position] & 0x40) != 0)
            {
                x |= -(1 << (shift + 7)); // sign extend
            }
            Position++;
            return (uint)x;
        }

        /// <summary>
        /// Reads the byte block of the specified size from the current position in the stream.
        /// </summary>
        /// <param name="size">The size of block.</param>
        public byte[] ReadBlock(uint size)
        {
            byte[] block = new byte[size];

            Array.Copy(Data, Position, block, 0, block.Length);
            Position += block.Length;
            return block;
        }

        /// <summary>
        /// Reads the byte block of the specified size from the specified position in the stream.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="position">The position.</param>
        public byte[] ReadBlock(uint size, int position)
        {
            if (position < 0 || position >= Data.Length)
            {
                return Array.Empty<byte>();
            }

            int originalPosition = Position;
            Position = position;
            byte[] result = ReadBlock(size);
            Position = originalPosition;
            return result;
        }

        /// <summary>
        /// Reads the string from the specified position in the stream.
        /// </summary>
        /// <param name="position">The position.</param>
        public string ReadString(int position)
        {
            if (position < 0 || position >= Data.Length)
            {
                return string.Empty;
            }

            int originalPosition = Position;
            Position = position;
            string result = ReadString();
            Position = originalPosition;
            return result;
        }

        /// <summary>
        /// Reads the unsigned int from the specified position in the stream.
        /// </summary>
        /// <param name="position">The position.</param>
        public uint ReadUint(int position)
        {
            if (position < 0 || position >= Data.Length)
            {
                return 0;
            }

            int originalPosition = Position;
            Position = position;
            uint result = ReadUint();
            Position = originalPosition;
            return result;
        }

        /// <summary>
        /// Reads the specified structure from the specified position in the stream.
        /// </summary>
        /// <typeparam name="T">Type of the structure to be read.</typeparam>
        /// <param name="position">The position.</param>
        public T ReadStructure<T>(int position)
        {
            if (position < 0 || position >= Data.Length)
            {
                return default;
            }

            int originalPosition = Position;
            Position = position;
            T result = ReadStructure<T>();
            Position = originalPosition;
            return result;
        }
    }
}
