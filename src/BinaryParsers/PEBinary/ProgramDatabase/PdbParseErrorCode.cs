// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    /// <summary>
    /// Exception codes
    /// </summary>
    public enum PdbParseErrorCode : uint
    {
        E_PDB_OK = 0x806D0001,         // matching corresponding HRESULT codes
        E_PDB_USAGE,
        E_PDB_OUT_OF_MEMORY,
        E_PDB_FILE_SYSTEM,
        E_PDB_NOT_FOUND,
        E_PDB_INVALID_SIG,
        E_PDB_INVALID_AGE,
        E_PDB_PRECOMP_REQUIRED,
        E_PDB_OUT_OF_TI,
        E_PDB_NOT_IMPLEMENTED,
        E_PDB_V1_PDB,
        E_PDB_FORMAT,
        E_PDB_LIMIT,
        E_PDB_CORRUPT,
        E_PDB_TI16,
        E_PDB_ACCESS_DENIED,
        E_PDB_ILLEGAL_TYPE_EDIT,
        E_PDB_INVALID_EXECUTABLE,
        E_PDB_DBG_NOT_FOUND,
        E_PDB_NO_DEBUG_INFO,
        E_PDB_INVALID_EXE_TIMESTAMP,
        E_PDB_RESERVED,
        E_PDB_DEBUG_INFO_NOT_IN_PDB,
        E_PDB_SYMSRV_BAD_CACHE_PATH,
        E_PDB_SYMSRV_CACHE_FULL,
        E_PDB_MAX
    }
}
