// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>
    /// Exception we throw when bad stuff happens
    /// </summary>
    public class PdbParseException : Exception
    {
        public PdbParseErrorCode ExceptionCode;

        /// <summary>
        /// Ctor based on the ErrorCode (HRESULT) from COMException.
        /// If we don't recognize error code we rethrow the original COMException
        /// </summary>
        /// <param name="ce"></param>
        public PdbParseException(System.Runtime.InteropServices.COMException ce)
            : this(ce.ErrorCode, ce)
        {
            if (((uint)ce.ErrorCode < (uint)PdbParseErrorCode.E_PDB_OK) || ((uint)ce.ErrorCode >= (uint)PdbParseErrorCode.E_PDB_MAX))
            {
                throw ce;
            }
        }

        public PdbParseException(int code, Exception innerException)
            : base(
                (((uint)code >= (uint)PdbParseErrorCode.E_PDB_OK) && ((uint)code < (uint)PdbParseErrorCode.E_PDB_MAX)) ? String.Format("{0} ({1})", ((PdbParseErrorCode)code).ToString(), PdbParseExceptionStrings[(uint)code - (uint)PdbParseErrorCode.E_PDB_OK]) : innerException.Message,
                innerException)
        {
            ExceptionCode = (PdbParseErrorCode)(uint)code;
        }

        public PdbParseException(PdbParseErrorCode code, Exception innerException)
            : base(String.Format("{0} : {1}", code.ToString(), PdbParseExceptionStrings[(uint)code - (uint)PdbParseErrorCode.E_PDB_OK]), innerException)
        {
            ExceptionCode = code;
        }

        public PdbParseException(PdbParseErrorCode code, string message, Exception innerException)
            : base(message, innerException)
        {
            ExceptionCode = code;
        }
        
        public PdbParseException(string message) : base(message)
        {
            ExceptionCode = PdbParseErrorCode.E_PDB_MAX;
        }
        
        public PdbParseException(string message, Exception innerException)
            : base(message, innerException)
        {
            ExceptionCode = PdbParseErrorCode.E_PDB_MAX;
        }

        public static string[] PdbParseExceptionStrings = {
                                                               BinaryParsersResources.Ok,                // E_PDB_OK
                                                               BinaryParsersResources.InvalidParameters, // E_PDB_USAGE
                                                               BinaryParsersResources.OutOfMemory,       // E_PDB_OUT_OF_MEMORY
                                                               BinaryParsersResources.DriveNotReady,     // E_PDB_FILE_SYSTEM
                                                               BinaryParsersResources.FileNotFound,      // E_PDB_NOT_FOUND
                                                               BinaryParsersResources.MismatchedPdbSig,  // E_PDB_INVALID_SIG
                                                               BinaryParsersResources.MismatchedPdbAge,  // E_PDB_INVALID_AGE
                                                               BinaryParsersResources.ContactSupport,    // E_PDB_PRECOMP_REQUIRED
                                                               BinaryParsersResources.ContactSupport,    // E_PDB_OUT_OF_TI
                                                               BinaryParsersResources.ContactSupport,    // E_PDB_NOT_IMPLEMENTED
                                                               BinaryParsersResources.OldPdbVersion,     // E_PDB_V1_PDB
                                                               BinaryParsersResources.FileNetError,      // E_PDB_FORMAT
                                                               BinaryParsersResources.ContactSupport,    // E_PDB_LIMIT
                                                               BinaryParsersResources.PdbCorrupted,      // E_PDB_CORRUPT
                                                               BinaryParsersResources.ContactSupport,    // E_PDB_TI16
                                                               BinaryParsersResources.AccessDenied,      // E_PDB_ACCESS_DENIED
                                                               BinaryParsersResources.ContactSupport,    // E_PDB_ILLEGAL_TYPE_EDIT
                                                               BinaryParsersResources.InvalidExecutable, // E_PDB_INVALID_EXECUTABLE
                                                               BinaryParsersResources.DbgNotFound,       // E_PDB_DBG_NOT_FOUND
                                                               BinaryParsersResources.PdbStripped,       // E_PDB_NO_DEBUG_INFO
                                                               BinaryParsersResources.InvalidTimestamp,  // E_PDB_INVALID_EXE_TIMESTAMP
                                                               BinaryParsersResources.ContactSupport,    // E_PDB_RESERVED
                                                               BinaryParsersResources.PdbHasNoSymbols,   // E_PDB_DEBUG_INFO_NOT_IN_PDB
                                                               BinaryParsersResources.ContactSupport,    // E_PDB_SYMSRV_BAD_CACHE_PATH
                                                               BinaryParsersResources.ContactSupport     // E_PDB_SYMSRV_CACHE_FULL
        };
    }
}
