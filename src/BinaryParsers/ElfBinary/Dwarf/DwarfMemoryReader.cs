// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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
        public uint Position { get; set; }

        /// <summary>
        /// Ensures that the requested number of bytes is available from the current position.
        /// Throws <see cref="DwarfParseException"/> if the request would read past the end
        /// of the underlying buffer.
        /// </summary>
        /// <param name="bytesRequested">The number of bytes to read.</param>
        private void EnsureAvailable(int bytesRequested)
        {
            if (bytesRequested < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesRequested));
            }

            // Position is uint, Data.Length is int. Use long arithmetic to avoid overflow.
            if (Position > Data.Length || (long)Position + bytesRequested > Data.Length)
            {
                throw new DwarfParseException("Unexpected end of DWARF data.");
            }
        }

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
            EnsureAvailable(1);
            return Data[Position];
        }

        /// <summary>
        /// Reads the specified structure from the current position in the stream.
        /// </summary>
        /// <typeparam name="T">Type of the structure to be read</typeparam>
        public T ReadStructure<T>()
        {
            int size = Marshal.SizeOf<T>();
            EnsureAvailable(size);
            T result = Marshal.PtrToStructure<T>((nint)(pointer + Position));
            Position += (uint)size;
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
        public string ReadString()
        {
            if (IsEnd)
            {
                throw new DwarfParseException("Unexpected end of DWARF data while reading string.");
            }

            int start = (int)Position;
            int end = Array.IndexOf(Data, (byte)0, start);

            if (end == -1)
            {
                throw new DwarfParseException("Unterminated string in DWARF data.");
            }

            string result = Encoding.ASCII.GetString(Data, start, end - start);
            Position = (uint)(end + 1);
            return result;
        }

        /// <summary>
        /// Reads the byte from the current position in the stream.
        /// </summary>
        public byte ReadByte()
        {
            EnsureAvailable(1);
            return Data[Position++];
        }

        /// <summary>
        /// Reads the unsigned short from the current position in the stream.
        /// </summary>
        public ushort ReadUshort()
        {
            EnsureAvailable(2);
            ushort result = (ushort)Marshal.ReadInt16(pointer, (int)Position);
            Position += 2;
            return result;
        }

        public uint ReadThreeBytes()
        {
            EnsureAvailable(3);
            uint firstTwo = (uint)Marshal.ReadInt16(pointer, (short)Position);
            Position += 2;

            return (firstTwo << 16) + ReadByte();
        }

        /// <summary>
        /// Reads the unsigned int from the current position in the stream.
        /// </summary>
        public uint ReadUint()
        {
            EnsureAvailable(4);
            uint result = (uint)Marshal.ReadInt32(pointer, (int)Position);
            Position += 4;
            return result;
        }

        /// <summary>
        /// Reads the unsigned long from the current position in the stream.
        /// </summary>
        public ulong ReadUlong()
        {
            EnsureAvailable(8);
            ulong result = (ulong)Marshal.ReadInt64(pointer, (int)Position);
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
        public ulong ULEB128()
        {
            ulong result = 0;
            int shift = 0;

            while (true)
            {
                if (IsEnd)
                {
                    throw new DwarfParseException("Unexpected end of DWARF data while reading ULEB128.");
                }

                byte b = Data[Position++];
                result |= ((ulong)(b & 0x7f)) << shift;

                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;

                if (shift >= 64)
                {
                    throw new DwarfParseException("ULEB128 value is too large.");
                }
            }

            return result;
        }

        /// <summary>
        /// Reads signed LEB 128 value from the current position in the stream.
        /// </summary>
        public uint SLEB128()
        {
            int result = 0;
            int shift = 0;
            byte b;

            while (true)
            {
                if (IsEnd)
                {
                    throw new DwarfParseException("Unexpected end of DWARF data while reading SLEB128.");
                }

                b = Data[Position++];
                result |= (b & 0x7f) << shift;
                shift += 7;

                if ((b & 0x80) == 0)
                {
                    break;
                }

                if (shift >= 32)
                {
                    throw new DwarfParseException("SLEB128 value is too large.");
                }
            }

            // If the sign bit of the final byte is set, sign-extend.
            if ((shift < 32) && ((b & 0x40) != 0))
            {
                result |= - (1 << shift);
            }

            return (uint)result;
        }

        /// <summary>
        /// Reads the byte block of the specified size from the current position in the stream.
        /// </summary>
        /// <param name="size">The size of block.</param>
        public byte[] ReadBlock(ulong size)
        {
            if (size > int.MaxValue)
            {
                throw new DwarfParseException("Requested DWARF block size is too large.");
            }

            int bytes = (int)size;
            EnsureAvailable(bytes);

            byte[] block = new byte[bytes];
            Array.Copy(Data, Position, block, 0, block.Length);
            Position += (uint)block.Length;
            return block;
        }

        /// <summary>
        /// Reads the byte block of the specified size from the specified position in the stream.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="position">The position.</param>
        public byte[] ReadBlock(uint size, uint position)
        {
            if (position < 0 || position >= Data.Length)
            {
                return Array.Empty<byte>();
            }

            uint originalPosition = Position;
            Position = position;
            byte[] result = ReadBlock(size);
            Position = originalPosition;
            return result;
        }

        /// <summary>
        /// Reads the string from the specified position in the stream.
        /// </summary>
        /// <param name="position">The position.</param>
        public string ReadString(uint position)
        {
            if (position >= Data.Length)
            {
                return string.Empty;
            }

            uint originalPosition = Position;
            Position = position;
            string result = ReadString();
            Position = originalPosition;
            return result;
        }

        /// <summary>
        /// Reads the unsigned int from the specified position in the stream.
        /// </summary>
        /// <param name="position">The position.</param>
        public uint ReadUint(uint position)
        {
            if (position < 0 || position >= Data.Length)
            {
                return 0;
            }

            uint originalPosition = Position;
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
        public T ReadStructure<T>(uint position)
        {
            if (position < 0 || position >= Data.Length)
            {
                return default;
            }

            uint originalPosition = Position;
            Position = position;
            T result = ReadStructure<T>();
            Position = originalPosition;
            return result;
        }
    }
}
