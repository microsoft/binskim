// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PEBinary : BinaryBase
    {
        private Lazy<Pdb> _pdb;
        private string _symbolPath;

        public PEBinary(Uri uri, string symbolPath = null) : base(uri)
        {
            PE = new PE(TargetUri.LocalPath);
            IsManagedAssembly = PE.IsManaged;
            LoadException = PE.LoadException;
            Valid = PE.IsPEFile;
            _symbolPath = symbolPath;

            _pdb = new Lazy<Pdb>(LoadPdb, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private Pdb LoadPdb()
        {
            // We should never be required to load a PDB for a managed assembly that does
            // not incorporate native code, as no managed-relevant rule currently crawls
            // PDBs for its analysis.
            Debug.Assert(!PE.IsManaged || PE.IsMixedMode);

            Pdb pdb = null;
            try
            {
                pdb = new Pdb(PE.FileName, _symbolPath);
            }
            catch (PdbParseException ex)
            {
                PdbParseException = ex;
            }

            if (pdb != null && pdb.IsStripped)
            {
                StrippedPdb = pdb;
                pdb = null;
                PdbParseException = new PdbParseException(BinaryParsersResources.PdbStripped);
            }
            return pdb;
        }

        public PdbParseException PdbParseException { get; internal set; }

        public bool IsManagedAssembly { get; internal set; }

        public PE PE { get; private set; }

        public Pdb Pdb
        {
            get
            {
                return _pdb?.Value;
            }            
        }

        public Pdb StrippedPdb { get; private set; }

        public void DisposePortableExecutableData()
        {
            if (_pdb != null &&
                _pdb.IsValueCreated &&
                _pdb.Value != null)
            {
                _pdb.Value.Dispose();
            }
            _pdb = null;

            if (PE != null)
            {
                PE.Dispose();
                PE = null;
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
