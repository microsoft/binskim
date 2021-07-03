// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Public symbol defined in image container (<see cref="IDwarfBinary"/>).
    /// </summary>
    public class DwarfPublicSymbol
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfPublicSymbol"/> class.
        /// </summary>
        /// <param name="name">The symbol name.</param>
        /// <param name="address">The address.</param>
        public DwarfPublicSymbol(string name, ulong address)
        {
            Name = name;
            Address = address;
        }

        /// <summary>
        /// Gets the public symbol name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the public symbol address.
        /// </summary>
        public ulong Address { get; private set; }
    }
}
