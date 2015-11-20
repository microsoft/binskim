// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.BinSkim.Sdk
{
    public class BinaryAnalyzerContext
    {
        private PE _pe;
        private Uri _uri;

        public PE PE
        {
            get
            {
                if (_pe == null)
                {
                    _pe = new PE(Uri.LocalPath);
                }
                return _pe;
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

        private Pdb _pdb;
        private bool _pdbResolved;

        public Pdb Pdb
        {
            get
            {
                if (_pdbResolved) { return _pdb; }

                _pdbResolved = true;
                try
                {
                    _pdb = new Pdb(PE.FileName, Pdb.SymbolPath);
                }
                catch (PdbParseException ex)
                {
                    PdbParseException = ex;
                }
                return _pdb;
            }
        }

        public PdbParseException PdbParseException { get; internal set; }

        public IMessageLogger<BinaryAnalyzerContext> Logger { get; internal set; }

        public IRuleContext Rule { get; internal set; }

        public Version MinimumSupportedCompilerVersion { get; internal set; }

        public PropertyBag Policy { get; internal set; }
    }
}
