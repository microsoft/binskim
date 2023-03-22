﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        private static readonly object sync = new object();
        private static ConcurrentDictionary<string, string> s_cachedPdbLocation;

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

            if (this.tracePdbLoad)
            {
                this.PdbLoadTrace = new StringBuilder();
            }

            this.LoadException = this.PE.LoadException;
            this.localSymbolDirectories = localSymbolDirectories;

            if (s_cachedPdbLocation == null)
            {
                lock (sync)
                {
                    if (s_cachedPdbLocation == null)
                    {
                        MapPdbs(localSymbolDirectories);
                    }
                }
            }
        }

        public static void ClearLocalSymbolDirectoriesCache()
        {
            lock (sync)
            {
                if (s_cachedPdbLocation != null)
                {
                    s_cachedPdbLocation = null;
                }
            }
        }

        public PE PE { get; private set; }

        public Pdb Pdb => this.pdb?.Value;

        public StringBuilder PdbLoadTrace { get; set; }

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
            catch (ArgumentException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private Pdb LoadPdb()
        {
            const string pdbExtension = ".pdb";
            string peOrPdbPath = this.PE?.FileName ?? this.TargetUri.LocalPath;
            string extension = Path.GetExtension(peOrPdbPath);

            // Trying to load exe and pdb
            if (!TryLoadPdb(peOrPdbPath, extension, this.symbolPath, this.localSymbolDirectories, this.tracePdbLoad, out Pdb pdb)
                && !extension.Equals(pdbExtension, StringComparison.OrdinalIgnoreCase)
                && this.PdbParseException?.ExceptionCode == DiaHresult.E_PDB_NOT_FOUND)
            {
                AddToPdbLoadTraceForFailedAttempt(pdb);

                peOrPdbPath = peOrPdbPath.Replace(extension, pdbExtension, StringComparison.OrdinalIgnoreCase);

                // If Pdb exists side-by-side with exe, let's try to read
                if (File.Exists(peOrPdbPath))
                {
                    TryLoadPdb(peOrPdbPath, pdbExtension, this.symbolPath, this.localSymbolDirectories, this.tracePdbLoad, out pdb);
                    AddToPdbLoadTrace(pdb);
                }
                else
                {
                    this.PdbLoadTrace?.AppendLine($"  Examined PDB path: '{peOrPdbPath}'. HResult: {DiaHresult.E_PDB_NOT_FOUND}.");
                }

                if (pdb == null)
                {
                    string fileName = Path.GetFileName(peOrPdbPath);

                    // Let's search in localSymbolDirectories for the pdb
                    peOrPdbPath = RetrievePdbPath(fileName);
                    if (!string.IsNullOrEmpty(peOrPdbPath))
                    {
                        if (File.Exists(peOrPdbPath))
                        {
                            TryLoadPdb(peOrPdbPath, pdbExtension, this.symbolPath, this.localSymbolDirectories, this.tracePdbLoad, out pdb);
                            AddToPdbLoadTrace(pdb);
                        }
                        else
                        {
                            this.PdbLoadTrace?.AppendLine($"  Examined PDB path: '{peOrPdbPath}'. HResult: {DiaHresult.E_PDB_NOT_FOUND}.");
                        }
                    }
                }
            }
            else
            {
                AddToPdbLoadTraceForSuccessfulAttempt(pdb);
            }

            if (pdb != null && pdb.IsStripped)
            {
                this.StrippedPdb = pdb;
                pdb = null;
                this.PdbParseException = new PdbException(BinaryParsersResources.PdbStripped)
                {
                    LoadTrace = this.StrippedPdb.LoadTrace
                };
                AddToPdbLoadTraceForFailedAttempt(pdb);
            }

            return pdb;
        }

        private void AddToPdbLoadTraceForSuccessfulAttempt(Pdb currentPdb)
        {
            if (!string.IsNullOrWhiteSpace(currentPdb?.LoadTrace))
            {
                this.PdbLoadTrace?.Append(currentPdb.LoadTrace);
            }
        }

        private void AddToPdbLoadTraceForFailedAttempt(Pdb currentPdb)
        {
            if (currentPdb == null && !string.IsNullOrWhiteSpace(this.PdbParseException?.LoadTrace))
            {
                this.PdbLoadTrace?.Append(this.PdbParseException.LoadTrace);
            }
        }

        private void AddToPdbLoadTrace(Pdb currentPdb)
        {
            if (currentPdb != null)
            {
                AddToPdbLoadTraceForSuccessfulAttempt(currentPdb);
            }
            else
            {
                AddToPdbLoadTraceForFailedAttempt(currentPdb);
            }
        }

        private bool TryLoadPdb(string peOrPdbPath, string extension, string symbolPath, string localSymbolDirectories, bool tracePdbLoad, out Pdb pdb)
        {
            pdb = null;

            try
            {
                pdb = extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase)
                    ? new Pdb(peOrPdbPath, tracePdbLoad)
                    : new Pdb(peOrPdbPath, symbolPath, localSymbolDirectories, tracePdbLoad);
                this.PdbParseException = null;
                return true;
            }
            catch (PdbException ex)
            {
                this.PdbParseException = ex;
                return false;
            }
        }

        private string RetrievePdbPath(string pdbName)
        {
            if (s_cachedPdbLocation?.IsEmpty != false)
            {
                return null;
            }

            s_cachedPdbLocation.TryGetValue(pdbName, out string pdbPath);
            return pdbPath;
        }

        private static void MapPdbs(string paths)
        {
            if (string.IsNullOrEmpty(paths))
            {
                return;
            }

            string[] symbolDirectories = paths.Split(';');
            s_cachedPdbLocation = new ConcurrentDictionary<string, string>();
            foreach (string symbolDirectory in symbolDirectories)
            {
                IEnumerable<string> files = Directory.EnumerateFiles(symbolDirectory, "*.pdb", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    s_cachedPdbLocation[fileName] = file;
                }
            }
        }
    }
}
