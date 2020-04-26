// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>
    /// Exception we throw when pdb operations fail.
    /// </summary>
    public class PdbException : Exception
    {
        public DiaHresult ExceptionCode { get; private set; }

        public string LoadTrace { get; set; }

        /// <summary>
        /// Ctor based on the ErrorCode (HRESULT) from COMException.
        /// If we don't recognize error code we rethrow the original COMException
        /// </summary>
        /// <param name="ce"></param>
        public PdbException(System.Runtime.InteropServices.COMException ce)
            : this(ce.ErrorCode, ce)
        {
            if ((ce.ErrorCode < (int)DiaHresult.E_PDB_OK) || (ce.ErrorCode >= (int)DiaHresult.E_PDB_MAX))
            {
                throw ce;
            }
        }

        public PdbException(int code, Exception innerException)
            : base(
                ((code >= (int)DiaHresult.E_PDB_OK) && (code < (int)DiaHresult.E_PDB_MAX)) 
                  ? ((DiaHresult)code).ToString()
                  : innerException.Message,
                innerException)
        {
            this.ExceptionCode = (DiaHresult)code;
        }

        public PdbException(DiaHresult hresult, Exception innerException)
            : this((int)hresult, innerException)
        {
            this.ExceptionCode = hresult;
        }

        public PdbException(DiaHresult code, string message, Exception innerException)
            : base(message, innerException)
        {
            this.ExceptionCode = code;
        }

        public PdbException(string message) : base(message)
        {
            this.ExceptionCode = DiaHresult.E_PDB_MAX;
        }

        public PdbException(string message, Exception innerException)
            : base(message, innerException)
        {
            this.ExceptionCode = DiaHresult.E_PDB_MAX;
        }
    }
}
