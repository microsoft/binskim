// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Dia2Lib;

using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>
    /// The main class
    /// </summary>
    public sealed class Pdb : IDisposable, IDiaLoadCallback2
    {
        private IDiaSession session;
        private StringBuilder loadTrace;
        private readonly Lazy<Symbol> globalScope;
        private bool restrictReferenceAndOriginalPathAccess;

        /// <summary>
        /// Load debug info from PE or PDB, using symbolPath to help find symbols
        /// </summary>
        /// <param name="pePath">The path to the portable executable.</param>
        /// <param name="symbolPath">The symsrv.dll symbol path.</param>
        /// <param name="localSymbolDirectories">An option collection of local directories that should be examined for PDBs.</param>
        public Pdb(
            string pePath,
            string symbolPath = null,
            string localSymbolDirectories = null,
            bool traceLoads = false)
        {
            this.loadTrace = traceLoads ? new StringBuilder() : null;
            this.globalScope = new Lazy<Symbol>(this.GetGlobalScope, LazyThreadSafetyMode.ExecutionAndPublication);
            this.writableSegmentIds = new Lazy<HashSet<uint>>(this.GenerateWritableSegmentSet);
            this.executableSectionContribCompilandIds = new Lazy<HashSet<uint>>(this.GenerateExecutableSectionContribIds);
            this.Init(pePath, symbolPath, localSymbolDirectories);
        }

        public string LoadTrace
        {
            get
            {
                return this.loadTrace?.ToString();
            }
            set
            {
                // We make this settable to allow the tool to clear the trace. This
                // prevents multiple reports (for every PDB loading rule) that the
                // PDB couldn't, in fact, be loaded.
                this.loadTrace = !string.IsNullOrEmpty(value)
                    ? new StringBuilder(value)
                    : null;
            }
        }

        /// <summary>
        /// Load debug info
        /// </summary>
        /// <param name="peOrPdbPath"></param>
        /// <param name="symbolPath"></param>
        private void Init(string peOrPdbPath, string symbolPath, string localSymbolDirectories)
        {
            try
            {
                PlatformSpecificHelpers.ThrowIfNotOnWindows();
                this.WindowsNativeLoadPdbUsingDia(peOrPdbPath, symbolPath, localSymbolDirectories);
            }
            catch (PlatformNotSupportedException ex)
            {
                throw new PdbException(message: BinaryParsersResources.PdbPlatformUnsupported, ex);
            }
            catch (COMException ce)
            {
                throw new PdbException(ce)
                {
                    LoadTrace = this.loadTrace?.ToString()
                };
            }
        }

        private void WindowsNativeLoadPdbUsingDia(string pePath, string symbolPath, string localSymbolDirectories)
        {
            IDiaDataSource diaSource = null;
            Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", "");
            Environment.SetEnvironmentVariable("_NT_ALT_SYMBOL_PATH", "");

            if (!string.IsNullOrEmpty(localSymbolDirectories))
            {
                // If we have one or more local symbol directories, we want
                // to probe them before any other default load behavior. If
                // this load code path fails, we fill fallback to these
                // defaults locations in the second load pass below.
                this.restrictReferenceAndOriginalPathAccess = true;
                try
                {
                    diaSource = MsdiaComWrapper.GetDiaSource();
                    diaSource.loadDataForExe(
                        pePath,
                        localSymbolDirectories,
                        this.loadTrace != null ? this : (object)IntPtr.Zero);
                }
                catch
                {
                    diaSource = null;
                }
            }

            if (diaSource == null)
            {
                this.restrictReferenceAndOriginalPathAccess = false;

                diaSource = MsdiaComWrapper.GetDiaSource();
                diaSource.loadDataForExe(
                    pePath,
                    symbolPath,
                    this.loadTrace != null ? this : (object)IntPtr.Zero);
            }

            diaSource.openSession(out this.session);
        }

        /// <summary>
        /// Returns the symbol for the global scope. Does not give up ownership of the symbol; callers
        /// must NOT dispose it.
        /// </summary>
        /// <value>The global scope.</value>
        public Symbol GlobalScope => this.globalScope.Value;

        public bool IsStripped => this.GlobalScope.IsStripped;

        /// <summary>
        /// Get the list of modules in this executable
        /// </summary>
        public DisposableEnumerable<Symbol> CreateObjectModuleIterator()
        {
            return this.GlobalScope.CreateChildIterator(SymTagEnum.SymTagCompiland);
        }

        /// <summary>
        /// Returns global variables defined in this executable
        /// </summary>
        public DisposableEnumerable<Symbol> CreateGlobalVariableIterator()
        {
            return this.GlobalScope.CreateChildIterator(SymTagEnum.SymTagData);
        }

        /// <summary>
        /// Returns global functions defined in this executable
        /// </summary>
        public DisposableEnumerable<Symbol> CreateGlobalFunctionIterator()
        {
            return this.GlobalScope.CreateChildIterator(SymTagEnum.SymTagFunction);
        }

        /// <summary>
        /// Returns global functions defined in this executable that meet the supplied filter
        /// </summary>
        public DisposableEnumerable<Symbol> CreateGlobalFunctionIterator(string functionName, NameSearchOptions searchOptions)
        {
            return this.GlobalScope.CreateChildren(SymTagEnum.SymTagFunction, functionName, searchOptions);
        }

        public DisposableEnumerable<SourceFile> CreateSourceFileIterator()
        {
            return this.CreateSourceFileIterator(null);
        }

        public DisposableEnumerable<SourceFile> CreateSourceFileIterator(Symbol inObjectModule)
        {
            return new DisposableEnumerable<SourceFile>(this.CreateSourceFileIteratorImpl(inObjectModule.UnderlyingSymbol));
        }

        private IEnumerable<SourceFile> CreateSourceFileIteratorImpl(IDiaSymbol inObjectModule)
        {
            IDiaEnumSourceFiles sourceFilesEnum = null;
            try
            {
                this.session.findFile(inObjectModule, null, 0, out sourceFilesEnum);

                while (true)
                {
                    sourceFilesEnum.Next(1, out IDiaSourceFile sourceFile, out uint celt);
                    if (celt != 1)
                    {
                        break;
                    }

                    yield return new SourceFile(sourceFile);
                }
            }
            finally
            {
                if (sourceFilesEnum != null)
                {
                    Marshal.ReleaseComObject(sourceFilesEnum);
                }
            }
        }

        private T CreateDiaTable<T>() where T : class
        {
            IDiaEnumTables enumTables = null;

            try
            {
                this.session.getEnumTables(out enumTables);
                if (enumTables == null)
                {
                    return null;
                }

                // GetEnumerator() fails in netcoreapp2.0--need to iterate without foreach.
                for (int i = 0; i < enumTables.Count; i++)
                {
                    IDiaTable table = enumTables.Item(i);
                    if (!(table is T result))
                    {
                        Marshal.ReleaseComObject(table);
                    }
                    else
                    {
                        return result;
                    }
                }
            }
            finally
            {
                if (enumTables != null)
                {
                    Marshal.ReleaseComObject(enumTables);
                }
            }

            return null;
        }

        private readonly Lazy<HashSet<uint>> writableSegmentIds;

        private HashSet<uint> GenerateWritableSegmentSet()
        {
            var result = new HashSet<uint>();
            IDiaEnumSegments enumSegments = null;

            try
            {
                enumSegments = this.CreateDiaTable<IDiaEnumSegments>();
            }
            catch (NotImplementedException) { }

            try
            {
                // GetEnumerator() fails in netcoreapp2.0--need to iterate without foreach.
                for (uint i = 0; i < (uint)enumSegments.Count; i++)
                {
                    IDiaSegment segment = enumSegments.Item(i);
                    try
                    {
                        if (segment.write != 0)
                        {
                            result.Add(segment.addressSection);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(segment);
                    }
                }
            }
            finally
            {
                if (enumSegments != null)
                {
                    Marshal.ReleaseComObject(enumSegments);
                }
            }

            return result;
        }

        public bool IsSegmentWithIdWritable(uint addressSection)
        {
            return this.writableSegmentIds.Value.Contains(addressSection);
        }

        private readonly Lazy<HashSet<uint>> executableSectionContribCompilandIds;

        private HashSet<uint> GenerateExecutableSectionContribIds()
        {
            var result = new HashSet<uint>();
            IDiaEnumSectionContribs enumSectionContribs = null;

            try
            {
                enumSectionContribs = this.CreateDiaTable<IDiaEnumSectionContribs>();
            }
            catch (NotImplementedException) { }

            if (enumSectionContribs == null)
            {
                return result;
            }

            try
            {
                // GetEnumerator() fails in netcoreapp2.0--need to iterate without foreach.
                for (uint i = 0; i < (uint)enumSectionContribs.Count; i++)
                {
                    IDiaSectionContrib sectionContrib = enumSectionContribs.Item(i);
                    try
                    {
                        if (sectionContrib.execute != 0)
                        {
                            result.Add(sectionContrib.compilandId);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sectionContrib);
                    }
                }
            }
            finally
            {
                if (enumSectionContribs != null)
                {
                    Marshal.ReleaseComObject(enumSectionContribs);
                }
            }
            return result;
        }

        public bool CompilandWithIdIsInExecutableSectionContrib(uint segmentId)
        {
            return this.executableSectionContribCompilandIds.Value.Contains(segmentId);
        }

        public bool ContainsExecutableSectionContribs()
        {
            return this.executableSectionContribCompilandIds.Value.Count != 0;
        }

        /// <summary>
        /// Returns the location of the PDB for this module
        /// </summary>
        public string PdbLocation => this.session.globalScope.symbolsFileName;

        public void Dispose()
        {
            if (this.globalScope.IsValueCreated)
            {
                this.globalScope.Value.Dispose();
            }

            if (this.session != null)
            {
                Marshal.ReleaseComObject(this.session);
            }
        }

        private Symbol GetGlobalScope()
        {
            return Symbol.Create(this.session.globalScope);
        }

        public void NotifyDebugDir([MarshalAs(UnmanagedType.Bool)] bool executable, int dataSize, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data)
        {
        }

        public void NotifyOpenDbg([MarshalAs(UnmanagedType.LPWStr)] string dbgPath, DiaHresult resultCode)
        {
            this.loadTrace.AppendLine($"  Examined DBG path: '{dbgPath}'. HResult: {resultCode}.");
        }

        public void NotifyOpenPdb([MarshalAs(UnmanagedType.LPWStr)] string pdbPath, DiaHresult resultCode)
        {
            this.loadTrace.AppendLine($"  Examined PDB path: '{pdbPath}'. HResult: {resultCode}.");
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        public bool RestrictRegistryAccess()
        {
            return true;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        public bool RestrictSymbolServerAccess()
        {
            return false;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        public bool RestrictOriginalPathAccess()
        {
            return this.restrictReferenceAndOriginalPathAccess;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        public bool RestrictReferencePathAccess()
        {
            return this.restrictReferenceAndOriginalPathAccess;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        public bool RestrictDbgAccess()
        {
            return false;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        public bool RestrictSystemRootAccess()
        {
            return true;
        }
    }
}
