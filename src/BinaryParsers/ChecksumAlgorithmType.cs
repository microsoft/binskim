// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    /// <summary>
    /// Checksum Algorithm for managed binary.
    /// </summary>
    public enum ChecksumAlgorithmType
    {
        /// <summary>
        /// Unknown format.
        /// </summary>
        Unknown,

        /// <summary>
        /// MD5 algorithm.
        /// </summary>
        Md5,

        /// <summary>
        /// SHA1 algorithm.
        /// </summary>
        Sha1,

        /// <summary>
        /// SHA256 algorithm.
        /// </summary>
        Sha256,
    }
}
