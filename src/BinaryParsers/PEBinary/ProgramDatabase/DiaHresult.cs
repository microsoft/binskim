// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    /// <summary>
    /// Exception codes
    /// </summary>
    public enum DiaHresult : int
    {
        None = 0,

        /// <summary>
        /// S_OK = 0.
        /// </summary>
        S_OK = 0,

        /// <summary>
        /// Catastrophic failure.
        /// E_UNEXPECTED = 0x8000FFFF.
        /// </summary>
        E_UNEXPECTED = unchecked((int)0x8000FFFF),

        /// <summary>
        /// Not implemented.
        /// E_NOTIMPL = 0x80004001.
        /// </summary>
        E_NOTIMPL = unchecked((int)0x80004001),

        /// <summary>
        /// Ran out of memory.
        /// E_OUTOFMEMORY = 0x8007000E.
        /// </summary>
        E_OUTOFMEMORY = unchecked((int)0x8007000E),

        /// <summary>
        /// One or more arguments are invalid.
        /// E_INVALIDARG = 0x80070057.
        /// </summary>
        E_INVALIDARG = unchecked((int)0x80070057),

        /// <summary>
        /// No such interface supported.
        /// E_NOINTERFACE = 0x80004002.
        /// </summary>
        E_NOINTERFACE = unchecked((int)0x80004002),

        /// <summary>
        /// Invalid pointer.
        /// E_POINTER = 0x80004003.
        /// </summary>
        E_POINTER = unchecked((int)0x80004003),

        /// <summary>
        /// Invalid handle.
        /// E_HANDLE = 0x80070006.
        /// </summary>
        E_HANDLE = unchecked((int)0x80070006),

        /// <summary>
        /// Operation aborted.
        /// E_ABORT = 0x80004004.
        /// </summary>
        E_ABORT = unchecked((int)0x80004004),

        /// <summary>
        /// Unspecified error.
        /// E_FAIL = 0x80004005.
        /// </summary>
        E_FAIL = unchecked((int)0x80004005),

        /// <summary>
        /// General access denied error.
        /// E_ACCESSDENIED = 0x80070005.
        /// </summary>
        E_ACCESSDENIED = unchecked((int)0x80070005),

        /// <summary>
        /// E_PDB_OK = 0x806d0001.
        /// </summary>
        E_PDB_OK = unchecked(1 << 31) | (0x6d << 16) | 1,

        /// <summary>
        /// E_PDB_USAGE = 0x806d0002.
        /// </summary>
        E_PDB_USAGE,

        /// <summary>
        /// Not used. Use OutOfMemory.
        /// E_PDB_OUT_OF_MEMORY = 0x806d0003.
        /// </summary>
        [Obsolete("Use E_OUTOFMEMORY", false)]
        E_PDB_OUT_OF_MEMORY,

        /// <summary>
        /// E_PDB_FILE_SYSTEM = 0x806d0004.
        /// </summary>
        E_PDB_FILE_SYSTEM,

        /// <summary>
        /// E_PDB_NOT_FOUND = 0x806d0005.
        /// </summary>
        E_PDB_NOT_FOUND,

        /// <summary>
        /// E_PDB_INVALID_SIG = 0x806d0006.
        /// </summary>
        E_PDB_INVALID_SIG,

        /// <summary>
        /// E_PDB_INVALID_AGE = 0x806d0007.
        /// </summary>
        E_PDB_INVALID_AGE,

        /// <summary>
        /// E_PDB_PRECOMP_REQUIRED = 0x806d0008.
        /// </summary>
        E_PDB_PRECOMP_REQUIRED,

        /// <summary>
        /// E_PDB_OUT_OF_TI = 0x806d0009.
        /// </summary>
        E_PDB_OUT_OF_TI,

        /// <summary>
        /// Not used. Use NotImpl.
        /// E_PDB_NOT_IMPLEMENTED = 0x806d000a.
        /// </summary>
        [Obsolete("Use E_NOTIMPL", false)]
        E_PDB_NOT_IMPLEMENTED,

        /// <summary>
        /// E_PDB_V1_PDB = 0x806d000b.
        /// </summary>
        E_PDB_V1_PDB,

        /// <summary>
        /// E_PDB_FORMAT = 0x806d000c.
        /// </summary>
        E_PDB_FORMAT,

        /// <summary>
        /// E_PDB_LIMIT = 0x806d000d.
        /// </summary>
        E_PDB_LIMIT,

        /// <summary>
        /// E_PDB_CORRUPT = 0x806d000e.
        /// </summary>
        E_PDB_CORRUPT,

        /// <summary>
        /// E_PDB_TI16 = 0x806d000f.
        /// </summary>
        E_PDB_TI16,

        /// <summary>
        /// Not used. Use AccessDenied.
        /// E_PDB_ACCESS_DENIED = 0x806d0010.
        /// </summary>
        [Obsolete("Use E_ACCESSDENIED", false)]
        E_PDB_ACCESS_DENIED,

        /// <summary>
        /// E_PDB_ILLEGAL_TYPE_EDIT = 0x806d0011.
        /// </summary>
        E_PDB_ILLEGAL_TYPE_EDIT,

        /// <summary>
        /// E_PDB_INVALID_EXECUTABLE = 0x806d0012.
        /// </summary>
        E_PDB_INVALID_EXECUTABLE,

        /// <summary>
        /// E_PDB_DBG_NOT_FOUND = 0x806d0013.
        /// </summary>
        E_PDB_DBG_NOT_FOUND,

        /// <summary>
        /// E_PDB_NO_DEBUG_INFO = 0x806d0014.
        /// </summary>
        E_PDB_NO_DEBUG_INFO,

        /// <summary>
        /// E_PDB_INVALID_EXE_TIMESTAMP = 0x806d0015.
        /// </summary>
        E_PDB_INVALID_EXE_TIMESTAMP,

        /// <summary>
        /// E_PDB_RESERVED = 0x806d0016.
        /// </summary>
        E_PDB_RESERVED,

        /// <summary>
        /// E_PDB_DEBUG_INFO_NOT_IN_PDB = 0x806d0017.
        /// </summary>
        E_PDB_DEBUG_INFO_NOT_IN_PDB,

        /// <summary>
        /// E_PDB_SYMSRV_BAD_CACHE_PATH = 0x806d0018.
        /// </summary>
        E_PDB_SYMSRV_BAD_CACHE_PATH,

        /// <summary>
        /// E_PDB_SYMSRV_CACHE_FULL = 0x806d0019.
        /// </summary>
        E_PDB_SYMSRV_CACHE_FULL,

        /// <summary>
        /// E_PDB_OBJECT_DISPOSED = 0x806d001a.
        /// </summary>
        E_PDB_OBJECT_DISPOSED,

        /// <summary>
        /// E_PDB_MAX = 0x806d001b.
        /// </summary>
        E_PDB_MAX
    }
}
