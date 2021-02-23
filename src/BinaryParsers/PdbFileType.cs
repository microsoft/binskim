// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    /// <summary>
    /// What is the format of a file.
    /// </summary>
    public enum PdbFileType
    {
        /// <summary>
        /// Unknown format.
        /// </summary>
        Unknown,

        /// <summary>
        /// Windows specific format.
        /// </summary>
        Windows,

        /// <summary>
        /// Portable OS format.
        /// </summary>
        Portable,
    }
}
