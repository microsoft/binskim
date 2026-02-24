// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Exception that represents a malformed or unexpectedly truncated DWARF data stream.
    /// </summary>
    public class DwarfParseException : Exception
    {
        public DwarfParseException()
        {
        }

        public DwarfParseException(string message)
            : base(message)
        {
        }

        public DwarfParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
