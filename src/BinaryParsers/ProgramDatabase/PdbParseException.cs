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


        public static string[] PdbParseExceptionStrings = {
                                                               BinaryParsersResources.Ok,
                                                               BinaryParsersResources.InvalidParameters,
                                                               BinaryParsersResources.OutOfMemory,
                                                               BinaryParsersResources.DriveNotReady,
                                                               BinaryParsersResources.FileNotFound,
                                                               BinaryParsersResources.MismatchedPdbSig,
                                                               BinaryParsersResources.MismatchedPdbAge,
                                                               BinaryParsersResources.ContactSupport,
                                                               BinaryParsersResources.ContactSupport,
                                                               BinaryParsersResources.ContactSupport,
                                                               BinaryParsersResources.OldPdbVersion,
                                                               BinaryParsersResources.FileNetError,
                                                               BinaryParsersResources.ContactSupport,
                                                               BinaryParsersResources.PdbCorrupted,
                                                               BinaryParsersResources.ContactSupport,
                                                               BinaryParsersResources.AccessDenied,
                                                               BinaryParsersResources.ContactSupport,
                                                               BinaryParsersResources.InvalidExecutable,
                                                               BinaryParsersResources.DbgNotFound,
                                                               BinaryParsersResources.PdbStripped,
                                                               BinaryParsersResources.InvalidTimestamp,
                                                               BinaryParsersResources.ContactSupport,
                                                               BinaryParsersResources.PdbHasNoSymbols,
                                                               BinaryParsersResources.ContactSupport
                                                           };
    }
}
