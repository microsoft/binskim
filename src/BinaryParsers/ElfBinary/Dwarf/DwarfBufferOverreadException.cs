// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Exception thrown when an attempt is made to read past the end of a DWARF data buffer.
    /// </summary>
    public class DwarfBufferOverreadException : InvalidOperationException
    {
        public DwarfBufferOverreadException(uint position, uint requestedBytes, int bufferLength)
            : this(position, requestedBytes, (long)bufferLength)
        {
        }

        internal DwarfBufferOverreadException(uint position, uint requestedBytes, long bufferLength)
            : base("Attempted to read past end of DWARF data buffer.")
        {
            Position = position;
            RequestedBytes = requestedBytes;
            BufferLength = (int)Math.Min(bufferLength, int.MaxValue);
            LongBufferLength = bufferLength;
        }

        /// <summary>
        /// Gets the position in the buffer at which the over-read was detected.
        /// </summary>
        public uint Position { get; }

        /// <summary>
        /// Gets the number of bytes that were requested.
        /// </summary>
        public uint RequestedBytes { get; }

        /// <summary>
        /// Gets the length capped at Int32.MaxValue for compatibility with array-backed readers.
        /// </summary>
        public int BufferLength { get; }

        /// <summary>
        /// Gets the full length of the underlying array or file-backed section.
        /// </summary>
        public long LongBufferLength { get; }
    }
}
