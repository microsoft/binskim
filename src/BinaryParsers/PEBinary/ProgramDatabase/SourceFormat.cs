// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>
    /// The Injected Source Marshalling structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SourceFormat
    {
        /// <summary>
        /// The identifier of the source language.
        /// </summary>
        public Guid Language;

        /// <summary>
        /// The identifier of the language vendor.
        /// </summary>
        public Guid LanguageVendor;

        /// <summary>
        /// The identifier of the document type.
        /// </summary>
        public Guid DocumentType;

        /// <summary>
        /// The identifier of the hashing algorithm used.
        /// </summary>
        public Guid AlgorithmId;

        /// <summary>
        /// The size of the checksum data that follows this header.
        /// </summary>
        public uint CheckSumSize;

        /// <summary>
        /// The size of the source data that follows the checksum field.
        /// </summary>
        public uint SourceSize;

        // followed by 'checkSumSize' bytes of checksum
        // followed by 'sourceSize' source bytes
    }
}
