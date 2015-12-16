// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Driver.Sdk;
using Microsoft.CodeAnalysis.Sarif.Sdk;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public class BinaryAnalyzerContext : IAnalysisContext
    {
        private PE _pe;
        private Uri _uri;
        private Pdb _pdb;
        private bool _pdbResolved;

        public bool IsManagedAssembly { get; internal set; }

        public PE PE
        {
            get
            {
                if (_pe == null)
                {
                    PE = new PE(Uri.LocalPath);
                    IsManagedAssembly = _pe.IsManaged;
                }
                return _pe;
            }
            set
            {
                if (value != null && _pdbResolved)
                {
                    throw new InvalidOperationException("Attempt to access a previously disposed PE object.");
                }
                _pe = value;
            }
        }

        public Uri Uri
        {
            get
            {
                return _uri;
            }
            set
            {
                if (_uri != null)
                {
                    throw new InvalidOperationException(SdkResources.IllegalContextReuse);
                }
                _uri = value;
            }
        }

        public Pdb Pdb
        {
            get
            {
                if (_pdbResolved)
                {
                    return _pdb;
                }

                try
                {
                    Pdb = new Pdb(PE.FileName, Pdb.SymbolPath);
                }
                catch (PdbParseException ex)
                {
                    PdbParseException = ex;
                }

                _pdbResolved = true;
                return _pdb;
            }
            set
            {
                if (value != null && _pdbResolved)
                {
                    throw new InvalidOperationException("Attempt to access a previously disposed PE object.");
                }
                _pdb = value;
            }
        }

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

        public PdbParseException PdbParseException { get; internal set; }

        public IMessageLogger<BinaryAnalyzerContext> Logger { get; internal set; }

        public IRuleDescriptor Rule { get; internal set; }

        public Version MinimumSupportedCompilerVersion { get; internal set; }

        public PropertyBag Policy { get; internal set; }
    }
}
