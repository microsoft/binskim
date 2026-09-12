// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public class DwarfFileStringReaderTests
    {
        [Fact]
        public void ReadString_ReadsFromTheRequestedSectionPosition()
        {
            byte[] data = Encoding.UTF8.GetBytes("prefix\0first\0second\0");
            using var stream = new MemoryStream(data);
            using var reader = new DwarfFileStringReader(
                stream,
                sectionOffset: 7,
                sectionSize: (ulong)data.Length - 7,
                leaveOpen: true);

            reader.ReadString(6).Should().Be("second");
        }

        [Fact]
        public void ReadString_ReadsPastTheInt32BoundaryWithoutAllocatingTheWholeSection()
        {
            uint position = (uint)int.MaxValue + 34;
            const string expected = "past-limit";
            byte[] value = Encoding.UTF8.GetBytes(expected + "\0");
            var contents = new Dictionary<long, byte>();
            for (int index = 0; index < value.Length; index++)
            {
                contents[position + index] = value[index];
            }

            using var stream = new SparseReadStream((long)position + value.Length, contents);
            using var reader = new DwarfFileStringReader(
                stream,
                sectionOffset: 0,
                sectionSize: (ulong)stream.Length,
                leaveOpen: true);

            reader.ReadString(position).Should().Be(expected);
        }

        [Fact]
        public void ReadString_ReturnsEmptyWhenPositionIsOutsideTheSection()
        {
            using var stream = new MemoryStream(new byte[] { 0 });
            using var reader = new DwarfFileStringReader(
                stream,
                sectionOffset: 0,
                sectionSize: 1,
                leaveOpen: true);

            reader.ReadString(1).Should().BeEmpty();
        }

        [Fact]
        public void FileBackedReader_IsDisabledWhenThresholdIsNotSpecified()
        {
            ElfBinary.ShouldUseFileBackedDwarfStringReader(
                sectionSize: (ulong)int.MaxValue + 1,
                fileReadThreshold: null).Should().BeFalse();
        }

        [Theory]
        [InlineData(1023, 1024, false)]
        [InlineData(1024, 1024, true)]
        [InlineData(1025, 1024, true)]
        public void FileBackedReader_UsesConfiguredThreshold(
            ulong sectionSize,
            ulong fileReadThreshold,
            bool expected)
        {
            ElfBinary.ShouldUseFileBackedDwarfStringReader(
                sectionSize,
                fileReadThreshold).Should().Be(expected);
        }

        private sealed class SparseReadStream : Stream
        {
            internal SparseReadStream(long length, IReadOnlyDictionary<long, byte> contents)
            {
                this.Length = length;
                this.contents = contents;
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length { get; }

            public override long Position { get; set; }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesRead = 0;
                while (bytesRead < count && this.Position < this.Length)
                {
                    buffer[offset + bytesRead] = this.contents.TryGetValue(this.Position, out byte value)
                        ? value
                        : (byte)0;
                    this.Position++;
                    bytesRead++;
                }

                return bytesRead;
            }

            public override int ReadByte()
            {
                if (this.Position >= this.Length)
                {
                    return -1;
                }

                int value = this.contents.TryGetValue(this.Position, out byte result)
                    ? result
                    : 0;
                this.Position++;
                return value;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                this.Position = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => checked(this.Position + offset),
                    SeekOrigin.End => checked(this.Length + offset),
                    _ => throw new ArgumentOutOfRangeException(nameof(origin)),
                };
                return this.Position;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            private readonly IReadOnlyDictionary<long, byte> contents;
        }
    }
}
