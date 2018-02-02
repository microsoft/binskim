// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dia2Lib;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>
    /// Class <see cref="Symbol"/> hides a number of COM interface details from .NET callers of the
    /// Debug Interface Access (DIA) SDK. For example, this type hides the IEnumXxx COM pattern from
    /// callers when enumerating child symbols. For example, this type implements IDisposable and
    /// destroys the symbol (thereby potentially unlocking the underlying PDB) when dispose is called.
    /// </summary>
    /// <seealso cref="T:System.IDisposable"/>
    public sealed class Symbol : IDisposable
    {
        private readonly IDiaSymbol _sym;
        private bool _disposed;

        /// <summary>Creates a new <see cref="Symbol"/> around a COM <see cref="IDiaSymbol"/> implementation.</summary>
        /// <param name="symbol">The symbol to wrap, or null. This method takes ownership of the <see cref="IDiaSymbol"/> COM RCW.</param>
        /// <returns>If <paramref name="symbol"/> is null, null; otherwise a <see cref="Symbol"/> wrapper around <paramref name="symbol"/>.</returns>
        public static Symbol Create(IDiaSymbol symbol)
        {
            // The handling (or not) of null in callers appears to be inconsistent. We may want
            // to just have people not bother with the null check at all here eventually.
            if (symbol == null)
            {
                return null;
            }

            return new Symbol(symbol);
        }

        /// <summary>
        /// Unique id for the symbol
        /// </summary>
        public uint SymIndexId
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.symIndexId;
            }
        }

        /// <summary>Gets the symbol's name.</summary>
        /// <value>The symbol's name.</value>
        public string Name
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.name;
            }
        }

        /// <summary>Gets the symbol's undecorated name.</summary>
        /// <value>The symbol's undecorated name.</value>
        public string RawCxxUndecoratedName
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.undecoratedName;
            }
        }

        /// <summary>
        /// Gets the undecorated name with some C fixups. (The variable "name" returns the decorated name for C++, but the undecorated name for C; and for C++ this is reversed)
        /// </summary>
        /// <returns>If <see cref="P:Name"/> is a C++ mangled name, returns <see cref="P:RawCxxUndecoratedName"/>. Otherwise, returns <see cref="P:Name"/>.</returns>
        public string GetUndecoratedName()
        {
            string name = this.Name;
            return (name.Length != 0 && name[0] == '?')
                ? this.RawCxxUndecoratedName
                : name;
        }

        /// <summary>
        /// The lib it came from (if any)
        /// </summary>
        public string Lib
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.libraryName;
            }
        }

        public override string ToString()
        {
            this.AssertNotDisposed();

            string typeName;
            using (Symbol type = this.CreateType())
            {
                typeName = (type == null) ? "<none>" : type.Name;
            }

            string result = String.Format("Tag:{0} Location:{1} Type:{2} Name:{3}", this.SymbolTag, this.LocationType, typeName, this.Name);

            if (this.SymbolTag == SymTagEnum.SymTagData)
            {
                result += " (" + DataKind + ")";
            }

            return result;
        }

        /// <summary>Gets compiland record for this symbol.</summary>
        /// <returns>The compiland record for this symbol.</returns>
        public CompilandRecord CreateCompilandRecord()
        {
            return CompilandRecord.CreateSanitized(this.Name, this.Lib);
        }

        /// <summary>Creates compiland record for this symbol with the supplied suffix.</summary>
        /// <param name="suffix">The suffix to use.</param>
        /// <returns>The new compiland record with suffix.</returns>
        public CompilandRecord CreateCompilandRecordWithSuffix(string suffix)
        {
            return CompilandRecord.CreateSanitized(this.Name, this.Lib, suffix);
        }

        /// <summary>
        /// Symbol's Relative Virtual Address
        /// </summary>
        public uint RelativeVirtualAddress
        {
            get
            {
                this.AssertNotDisposed();

                if (LocationType != LocationType.LocIsStatic)
                {
                    throw new InvalidOperationException("RVA is only valid for LocIsStatic location types");
                }

                return _sym.relativeVirtualAddress;
            }
        }

        /// <summary>
        /// Symbol's section number
        /// </summary>
        public uint AddressSection
        {
            get
            {
                this.AssertNotDisposed();

                if (LocationType != LocationType.LocIsStatic)
                {
                    throw new InvalidOperationException("AddressSection is only valid for LocIsStatic location types");
                }

                return _sym.addressSection;
            }
        }

        /// <summary>
        /// Symbol's offset within section
        /// </summary>
        public uint AddressOffset
        {
            get
            {
                this.AssertNotDisposed();

                if (LocationType != LocationType.LocIsStatic)
                {
                    throw new InvalidOperationException("AddressOffset is only valid for LocIsStatic location types");
                }

                return _sym.addressOffset;
            }
        }

        /// <summary>
        /// Symbol's offset relative to parent. Valid for relative symbols and bit fields.
        /// </summary>
        public int Offset
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.offset;
            }
        }

        /// <summary>
        /// Symbol's location type
        /// </summary>
        public LocationType LocationType
        {
            get
            {
                this.AssertNotDisposed();
                return (LocationType)_sym.locationType;
            }
        }

        /// <summary>
        /// Symbol's tag, determines the symbol's type
        /// </summary>
        public SymTagEnum SymbolTag
        {
            get
            {
                this.AssertNotDisposed();
                return (SymTagEnum)_sym.symTag;
            }
        }

        /// <summary>
        /// Returns the type for a variable, the signature for the function, and so forth.
        /// </summary>
        public Symbol CreateType()
        {
            this.AssertNotDisposed();
            return Symbol.Create(_sym.type);
        }

        /// <summary>Creates all symbols which are children of this symbol.</summary>
        /// <returns>The symbols that are a child of this symbol.</returns>
        public DisposableEnumerable<Symbol> CreateChildren()
        {
            return this.CreateChildIterator(SymTagEnum.SymTagNull);
        }

        /// <summary>Creates symbols which are children of this symbol of the specified type.</summary>
        /// <param name="symbolTagType">Type of the symbols to retrieve.</param>
        /// <returns>The symbols that are a child of this symbol and have tag <paramref name="symbolTagType"/>.</returns>
        public DisposableEnumerable<Symbol> CreateChildIterator(SymTagEnum symbolTagType)
        {
            return this.CreateChildren(symbolTagType, null, NameSearchOptions.nsNone);
        }

        public DisposableEnumerable<Symbol> CreateChildren(SymTagEnum symbolTagType, string symbolName, NameSearchOptions searchOptions)
        {
            this.AssertNotDisposed();
            return new DisposableEnumerable<Symbol>(this.CreateChildrenImpl(symbolTagType, symbolName, searchOptions));
        }

        /// <summary>
        /// Symbol length
        /// </summary>
        public ulong Length
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.length;
            }
        }

        /// <summary>
        /// The function suppresses GS security checks ( __declspec(safebuffers) was used  )
        /// </summary>
        public bool IsSafeBuffers
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.isSafeBuffers != 0;
            }
        }

        public bool HasSecurityChecks
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.hasSecurityChecks != 0;
            }
        }

        /// <summary>
        /// The code is managed
        /// </summary>
        public bool IsManaged
        {
            get
            {
                this.AssertNotDisposed();
                // sometimes DIA reports managed functions as not managed, but the managed token is present,
                // so we can use that to determine managed functions (managed method tokens start with 0x06......)
                return _sym.managed != 0 || (_sym.locationType == (uint)LocationType.LocInMetaData); //  ((this.sym.token & 0xff000000) == 0x06000000);
            }
        }

        /// <summary>
        /// True is the data was generated by the compiler
        /// </summary>
        public bool IsCompilerGenerated
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.compilerGenerated != 0;
            }
        }

        /// <summary>
        /// Data kind
        /// </summary>
        public DataKind DataKind
        {
            get
            {
                this.AssertNotDisposed();
                return (DataKind)_sym.dataKind;
            }
        }

        /// <summary>
        /// Returns True if the object module has debug info
        /// </summary>
        public bool HasDebugInfo
        {
            get
            {
                this.AssertNotDisposed();
                return _sym.hasDebugInfo != 0;
            }
        }

        private void AssertNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Symbol");
            }
        }

        private Symbol(IDiaSymbol sym)
        {
            _sym = sym;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Marshal.ReleaseComObject(_sym);
            }

            _disposed = true;
        }

        public ObjectModuleDetails GetObjectModuleDetails()
        {
            this.AssertNotDisposed();

            Version backEndVersion = null;
            Version frontEndVersion = null;
            string compilerName = null;
            Language language = Language.Unknown;
            bool hasSecurityChecks = false;
            bool debugInfo = false;
            foreach (DisposableEnumerableView<Symbol> compilandDetails in this.CreateChildIterator(SymTagEnum.SymTagCompilandDetails))
            {
                IDiaSymbol compilandSymbol = compilandDetails.Value.UnderlyingSymbol;
                checked
                {
                    backEndVersion = new Version(
                        (int)compilandSymbol.backEndMajor,
                        (int)compilandSymbol.backEndMinor,
                        (int)compilandSymbol.backEndBuild,
                        (int)compilandSymbol.backEndQFE
                        );

                    frontEndVersion = new Version(
                        (int)compilandSymbol.frontEndMajor,
                        (int)compilandSymbol.frontEndMinor,
                        (int)compilandSymbol.frontEndBuild,
                        (int)compilandSymbol.frontEndQFE
                        );
                }

                compilerName = compilandSymbol.compilerName;
                language = (Language)compilandSymbol.language;
                hasSecurityChecks = compilandSymbol.hasSecurityChecks != 0;
                debugInfo = compilandSymbol.hasDebugInfo != 0;

                break; // For now we only look at the first compiland details record
            }

            string commandLine = null;
            foreach (DisposableEnumerableView<Symbol> compilandEnv in this.CreateChildIterator(SymTagEnum.SymTagCompilandEnv))
            {
                Symbol env = compilandEnv.Value;
                if (env.Name == "cmd")
                {
                    commandLine = env.UnderlyingSymbol.value as string;
                    break;
                }
            }

            return new ObjectModuleDetails(frontEndVersion, backEndVersion, commandLine, language, compilerName, hasSecurityChecks, debugInfo);
        }

        /// <summary>
        /// The library "hash". This is an XOR of the 1st 16 bytes of all source file hashes.
        /// </summary>
        public byte[] GetObjectFileHash(Pdb session)
        {
            bool hashPresent = false;
            byte[] res = new byte[16];

            // go through all source files in the lib and XOR their hashes (if present)
            // NB: there are a couple of interesting cases worth considering:
            //      1. .h files may be used to build more than one object files in the lib
            //          and when XOR'ed they cancel each other out;
            //      2. Some source files may not have hashes, in which case our results won't be totally reliable.
            foreach (DisposableEnumerableView<SourceFile> sfView in session.CreateSourceFileIterator(this))
            {
                SourceFile sf = sfView.Value;
                if (sf.HashType != HashType.None)
                {
                    byte[] hash = sf.Hash;

                    if ((hash == null) || (hash.Length < 16))
                    {
                        throw new PdbParseException("Unexpected hash length for file " + sf);
                    }

                    for (int j = 0; j < 16; j++)
                    {
                        res[j] ^= hash[j];
                    }

                    hashPresent = true;
                }
            }

            if (!hashPresent)
            {
                return null;
            }

            return res;
        }

        internal IDiaSymbol UnderlyingSymbol
        {
            get
            {
                this.AssertNotDisposed();
                return _sym;
            }
        }

        private IEnumerable<Symbol> CreateChildrenImpl(SymTagEnum symbolTagType, string symbolName, NameSearchOptions searchOptions)
        {
            IDiaEnumSymbols enumSymbols = null;

            try
            {
                try
                {
                    _sym.findChildren(symbolTagType, symbolName, (uint)searchOptions, out enumSymbols);
                }
                catch (NotImplementedException) { }

                if (enumSymbols == null)
                {
                    yield break;
                }

                while (true)
                {
                    uint celt = 0;
                    IDiaSymbol symbol;
                    enumSymbols.Next(1, out symbol, out celt);
                    if (celt != 1) break; //No more symbols
                    yield return Symbol.Create(symbol);
                }
            }
            finally
            {
                if (enumSymbols != null)
                {
                    Marshal.ReleaseComObject(enumSymbols);
                }
            }
        }
    }


    /// <summary>
    /// Symbol location type enum
    /// </summary>
    public enum LocationType : uint
    {
        LocIsNull,
        LocIsStatic,
        LocIsTLS,
        LocIsRegRel,
        LocIsThisRel,
        LocIsEnregistered,
        LocIsBitField,
        LocIsSlot,
        LocIsIlRel,
        LocInMetaData,
        LocIsConstant,
        LocTypeMax
    };

    /// <summary>
    /// Represents the data kind
    /// </summary>
    public enum DataKind : uint
    {
        DataIsUnknown,
        DataIsLocal,
        DataIsStaticLocal,
        DataIsParam,
        DataIsObjectPtr,
        DataIsFileStatic,
        DataIsGlobal,
        DataIsMember,
        DataIsStaticMember,
        DataIsConstant
    };

    // http://msdn.microsoft.com/en-us/library/yat28ads.aspx
    public enum NameSearchOptions : uint
    {
        nsNone,
        nsfCaseSensitive = 0x1,
        nsfCaseInsensitive = 0x2,
        nsfFNameExt = 0x4,
        nsfRegularExpression = 0x8,
        nsfUndecoratedName = 0x10
    }
}
