// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PEBinary : BinaryBase
    {
        private Lazy<Pdb> pdb;
        private readonly bool tracePdbLoad;
        private readonly string symbolPath;
        private readonly string localSymbolDirectories;

        public PEBinary(
            Uri uri,
            string symbolPath = null,
            string localSymbolDirectories = null,
            bool tracePdbLoad = false) : base(uri)
        {
            // We actively verify our ability to parse this binary as a PE.
            this.PE = new PE(this.TargetUri.LocalPath);

            // We defer attempting to load PDBs, as this won't be necessary
            // for every binary we analyze, depending on the binary itself
            // (managed vs. native) or the current scan rules configuration.
            this.pdb = new Lazy<Pdb>(this.LoadPdb, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

            if (this.TargetUri.LocalPath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                // If someone has asked us to analyze a PDB, we'll force it to load
                Pdb pdb = this.pdb?.Value;
                this.Valid = pdb != null;
                return;
            }

            this.symbolPath = symbolPath;
            this.Valid = this.PE.IsPEFile;
            this.tracePdbLoad = tracePdbLoad;
            this.LoadException = this.PE.LoadException;
            this.localSymbolDirectories = localSymbolDirectories;
        }

        private Pdb LoadPdb()
        {
            Pdb pdb = null;
            try
            {
                pdb = new Pdb(
                    this.PE?.FileName ?? this.TargetUri.LocalPath,
                    this.symbolPath,
                    this.localSymbolDirectories,
                    this.tracePdbLoad);
            }
            catch (PdbException ex)
            {
                this.PdbParseException = ex;
            }

            if (pdb != null && pdb.IsStripped)
            {
                this.StrippedPdb = pdb;
                pdb = null;
                this.PdbParseException = new PdbException(BinaryParsersResources.PdbStripped)
                {
                    LoadTrace = this.StrippedPdb.LoadTrace
                };
            }
            return pdb;
        }

        public PE PE { get; private set; }

        public Pdb Pdb => this.pdb?.Value;

        public PdbException PdbParseException { get; internal set; }

        public Pdb StrippedPdb { get; private set; }

        public void DisposePortableExecutableData()
        {
            if (this.pdb != null &&
                this.pdb.IsValueCreated &&
                this.pdb.Value != null)
            {
                this.pdb.Value.Dispose();
            }
            this.pdb = null;

            if (this.PE != null)
            {
                this.PE.Dispose();
                this.PE = null;
            }
        }

        public override void Dispose()
        {
            this.DisposePortableExecutableData();
        }

        public static bool CanLoadBinary(Uri uri)
        {
            // TODO: replace this with an actual sniff of PDB binary data.
            if (uri.LocalPath.EndsWith(".pdb"))
            {
                return true;
            }

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
