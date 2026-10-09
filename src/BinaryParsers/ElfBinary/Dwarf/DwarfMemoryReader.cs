// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Simple memory reader that provides specific functionality to read DWARF streams.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class DwarfMemoryReader : IDwarfStringReader
    {
        /// <summary>
        /// The pinned data
        /// </summary>
        private GCHandle pinnedData;

        /// <summary>
        /// The pointer of pinned data
        /// </summary>
        private IntPtr pointer;

        private MemoryMappedFile mappedFile;
        private MemoryMappedViewAccessor mappedView;
        private bool mappedPointerAcquired;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfMemoryReader"/> class.
        /// </summary>
        /// <param name="data">The data.</param>
        public DwarfMemoryReader(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Length = data.LongLength;
            Position = 0;
            pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
            pointer = pinnedData.AddrOfPinnedObject();
        }

        /// <summary>
        /// Maps a read-only section without allocating a byte array for its contents.
        /// DWARF offsets in this parser are unsigned 32-bit section-relative offsets.
        /// </summary>
        internal unsafe DwarfMemoryReader(string path, ulong sectionOffset, ulong sectionSize)
        {
            if (sectionSize > uint.MaxValue)
            {
                throw new NotSupportedException("DWARF sections larger than the unsigned 32-bit offset range are not supported.");
            }

            Data = Array.Empty<byte>();
            Length = (long)sectionSize;
            var stream = File.OpenRead(path);
            try
            {
                if (sectionOffset > (ulong)stream.Length || sectionSize > (ulong)stream.Length - sectionOffset)
                {
                    throw new InvalidDataException("DWARF section extends past the end of the file.");
                }

                if (sectionSize == 0)
                {
                    stream.Dispose();
                    return;
                }

                mappedFile = MemoryMappedFile.CreateFromFile(stream, null, 0,
                    MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
                mappedView = mappedFile.CreateViewAccessor((long)sectionOffset, Length, MemoryMappedFileAccess.Read);
                byte* mappedPointer = null;
                mappedView.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedPointer);
                mappedPointerAcquired = true;
                pointer = (IntPtr)(mappedPointer + mappedView.PointerOffset);
            }
            catch
            {
                Dispose();
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Gets the data buffer.
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// Gets the section length, including for file-backed sections larger than Int32.MaxValue.
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Gets or sets the current position in the stream.
        /// </summary>
        public uint Position { get; set; }

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
                return Position >= Length;
            }
        }

        private void EnsureAvailable(uint bytesToRead)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (bytesToRead > Length || Position > Length - bytesToRead)
            {
                throw new DwarfBufferOverreadException(Position, bytesToRead, Length);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (mappedPointerAcquired)
            {
                mappedView.SafeMemoryMappedViewHandle.ReleasePointer();
            }
            mappedView?.Dispose();
            mappedFile?.Dispose();
            if (pinnedData.IsAllocated)
            {
                pinnedData.Free();
            }
            pointer = IntPtr.Zero;
            disposed = true;
        }

        /// <summary>
        /// Peeks next byte in the stream.
        /// </summary>
        public byte Peek()
        {
            EnsureAvailable(1);
            return Marshal.ReadByte((nint)(pointer + Position));
        }

        /// <summary>
        /// Reads the specified structure from the current position in the stream.
        /// </summary>
        /// <typeparam name="T">Type of the structure to be read</typeparam>
        public T ReadStructure<T>()
        {
            EnsureAvailable((uint)Marshal.SizeOf<T>());
            T result = Marshal.PtrToStructure<T>((nint)(pointer + Position));
            Position += (uint)Marshal.SizeOf<T>();
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
            EnsureAvailable(1);
            uint start = Position;
            while (Position < Length && Peek() != 0)
            {
                Position++;
            }
            EnsureAvailable(1); // A string must terminate within this section, not the next one.
            string result = Marshal.PtrToStringAnsi((nint)(pointer + start), checked((int)(Position - start)));
            Position++;
            return result;
        }

        /// <summary>
        /// Reads the byte from the current position in the stream.
        /// </summary>
        public byte ReadByte()
        {
            EnsureAvailable(1);
            return Marshal.ReadByte((nint)(pointer + Position++));
        }

        /// <summary>
        /// Reads the unsigned short from the current position in the stream.
        /// </summary>
        public ushort ReadUshort()
        {
            EnsureAvailable(2);
            ushort result = (ushort)Marshal.ReadInt16((nint)(pointer + Position));

            Position += 2;
            return result;
        }

        public uint ReadThreeBytes()
        {
            EnsureAvailable(3);
            uint b0 = ReadByte();
            uint b1 = ReadByte();
            uint b2 = ReadByte();
            return b0 | (b1 << 8) | (b2 << 16);
        }

        /// <summary>
        /// Reads the unsigned int from the current position in the stream.
        /// </summary>
        public uint ReadUint()
        {
            EnsureAvailable(4);
            uint result = (uint)Marshal.ReadInt32((nint)(pointer + Position));

            Position += 4;
            return result;
        }

        /// <summary>
        /// Reads the unsigned long from the current position in the stream.
        /// </summary>
        public ulong ReadUlong()
        {
            EnsureAvailable(8);
            ulong result = (ulong)Marshal.ReadInt64((nint)(pointer + Position));

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
            ulong x = 0;
            int shift = 0;

            while (true)
            {
                byte b = Peek();

                if ((b & 0x80) == 0)
                {
                    break;
                }

                x |= (uint)((b & 0x7f) << shift);
                shift += 7;
                Position++;
            }

            x |= (uint)(Peek() << shift);
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

            while (true)
            {
                byte b = Peek();

                if ((b & 0x80) == 0)
                {
                    break;
                }

                x |= (b & 0x7f) << shift;
                shift += 7;
                Position++;
            }

            byte last = Peek();
            x |= last << shift;
            if ((last & 0x40) != 0)
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
        public byte[] ReadBlock(ulong size)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (Position >= Length)
            {
                return Array.Empty<byte>();
            }

            size = Math.Min(size, (ulong)(Length - Position));
            if (size > (ulong)Array.MaxLength)
            {
                throw new InvalidOperationException("A single DWARF block exceeds the maximum array length.");
            }
            byte[] block = new byte[size];

            Marshal.Copy((nint)(pointer + Position), block, 0, block.Length);
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
            if (position >= Length)
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
            if (position >= Length)
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
            if (position >= Length)
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
            if (position >= Length)
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
