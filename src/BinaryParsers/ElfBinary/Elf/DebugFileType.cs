// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Elf
{
    public enum DebugFileType
    {
        DebugIncluded,
        DebugOnlyFileDwo,
        DebugOnlyFileDebuglink,
        FromDwo,
        FromDebuglink,
        NoDebug,
        Unknown,
    }
}
