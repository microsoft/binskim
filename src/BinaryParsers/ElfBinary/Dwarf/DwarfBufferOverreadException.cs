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
            : base("Attempted to read past end of DWARF data buffer.")
        {
            Position = position;
            RequestedBytes = requestedBytes;
            BufferLength = bufferLength;
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
        /// Gets the total length of the underlying buffer.
        /// </summary>
        public int BufferLength { get; }
    }
}
