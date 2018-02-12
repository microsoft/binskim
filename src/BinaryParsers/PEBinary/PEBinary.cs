// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PEBinary : BinaryBase
    {
        private PE _pe;
        private Pdb _pdb;

        public PEBinary(Uri uri) : base(uri)
        {
            PE = new PE(TargetUri.LocalPath);
            IsManagedAssembly = PE.IsManaged;
            LoadException = PE.LoadException;
            Valid = PE.IsPEFile;

            try
            {
                Pdb = new Pdb(PE.FileName, Pdb.SymbolPath);
            }
            catch (PdbParseException ex)
            {
                PdbParseException = ex;
            }
        }
        
        public PdbParseException PdbParseException { get; internal set; }

        public bool IsManagedAssembly { get; internal set; }

        public PE PE { get; private set; }

        public Pdb Pdb { get; private set; }

        public void DisposePortableExecutableData()
        {
            if (_pdb != null)
            {
                _pdb.Dispose();
                _pdb = null;
            }

            if (_pe != null)
            {
                _pe.Dispose();
                _pe = null;
            }
        }

        public override void Dispose()
        {
            DisposePortableExecutableData();
        }

        public static bool CanLoadBinary(Uri uri)
        {
            try
            {
                using (FileStream fs = File.OpenRead(Path.GetFullPath(uri.LocalPath)))
                {
                    return PE.CheckPEMagicBytes(fs);
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
