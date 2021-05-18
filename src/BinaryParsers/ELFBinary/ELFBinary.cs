// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    /// <summary>
    /// Simple ELF image reader.
    /// </summary>
    public class ELFBinary : BinaryBase, IDwarfBinary
    {
        public ELFBinary(Uri uri) : base(uri)
        {
            try
            {
                string path = Path.GetFullPath(uri.LocalPath);
                ELF = ELFReader.Load<ulong>(path);

                Compilers = ELFUtility.GetELFCompilers(this.ELF);

                foreach (Segment<ulong> segment in ELF.Segments)
                {
                    if (segment.Type == ELFSharp.ELF.Segments.SegmentType.ProgramHeader)
                    {
                        CodeSegmentOffset = segment.Address - (ulong)segment.Offset;
                        break;
                    }
                }

                var publicSymbols = new List<DwarfPublicSymbol>();

                if (!(ELF.Sections.FirstOrDefault(s => s.Type == SectionType.SymbolTable) is SymbolTable<ulong> symbols) || !symbols.Entries.Any())
                {
                    symbols = ELF.Sections.FirstOrDefault(s => s.Type == SectionType.DynamicSymbolTable) as SymbolTable<ulong>;
                }

                if (symbols != null)
                {
                    foreach (SymbolEntry<ulong> symbol in symbols.Entries)
                    {
                        publicSymbols.Add(new DwarfPublicSymbol(symbol.Name, symbol.Value - CodeSegmentOffset));
                    }
                }
                PublicSymbols = publicSymbols;
                SectionRegions = ELF.Sections.Where(s => s.LoadAddress > 0).OrderBy(s => s.LoadAddress).ToArray();

                CompilationUnits = DwarfSymbolProvider.ParseCompilationUnits(this, DebugData, DebugDataDescription, DebugDataStrings, NormalizeAddress);
                LineNumberPrograms = DwarfSymbolProvider.ParseLineNumberPrograms(DebugLine, NormalizeAddress);
                CommonInformationEntries = DwarfSymbolProvider.ParseCommonInformationEntries(DebugFrame, EhFrame, new DwarfExceptionHandlingFrameParsingInput(this));
                LoadDwo();
                this.Valid = true;
            }
            // At some point, we may want to better enumerate expected vs. unexpected exceptions.
            // For now, though, we'll generically catch any of them--ELFSharp can throw a number of different exceptions
            // if given an invalid ELF file.
            catch (Exception e)
            {
                this.LoadException = e;
                this.Valid = false;
            }
        }

        public static bool CanLoadBinary(Uri uri)
        {
            try
            {
                return ELFReader.CheckELFType(Path.GetFullPath(uri.LocalPath)) != Class.NotELF;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public int GetDwarfVersion()
        {
            return DwarfVersion;
        }

        public string GetDwarfCompilerCommand()
        {
            if (CompilationUnits == null || CompilationUnits.Count == 0)
            {
                return string.Empty;
            }
            KeyValuePair<DwarfAttribute, DwarfAttributeValue> producer = CompilationUnits
                .SelectMany(c => c.Symbols)
                .Where(s => s.Tag == DwarfTag.CompileUnit)
                .SelectMany(s => s.Attributes)
                .FirstOrDefault(a => a.Key == DwarfAttribute.Producer);
            return producer.Key == DwarfAttribute.None ? string.Empty : producer.Value.String;
        }

        public DwarfLanguage GetLanguage()
        {
            if (CompilationUnits == null || CompilationUnits.Count == 0)
            {
                return DwarfLanguage.Unknown;
            }
            KeyValuePair<DwarfAttribute, DwarfAttributeValue> language = CompilationUnits
                .SelectMany(c => c.Symbols)
                .Where(s => s.Tag == DwarfTag.CompileUnit)
                .SelectMany(s => s.Attributes)
                .FirstOrDefault(a => a.Key == DwarfAttribute.Language);
            return language.Key == DwarfAttribute.None ? DwarfLanguage.Unknown : ((DwarfLanguage)(language.Value.Constant));
        }

        /// <summary>
        /// Gets the public symbols.
        /// </summary>
        public IReadOnlyList<DwarfPublicSymbol> PublicSymbols { get; private set; }

        /// <summary>
        /// Gets the code segment offset.
        /// </summary>
        public ulong CodeSegmentOffset { get; private set; }

        /// <summary>
        /// Gets the image load offset.
        /// </summary>
        public ulong LoadOffset { get; private set; } = 0;

        /// <summary>
        /// Gets the debug data.
        /// </summary>
        public byte[] DebugData => LoadSection(".debug_info");

        /// <summary>
        /// Gets the debug data description.
        /// </summary>
        public byte[] DebugDataDescription => LoadSection(".debug_abbrev");

        /// <summary>
        /// Gets the debug data strings.
        /// </summary>
        public byte[] DebugDataStrings => LoadSection(".debug_str");

        /// <summary>
        /// Gets the debug frame.
        /// </summary>
        public byte[] DebugFrame => LoadSection(".debug_frame");

        /// <summary>
        /// Gets the exception handling frames used for unwinding (generated by usually GCC compiler).
        /// </summary>
        public byte[] EhFrame => LoadSection(".eh_frame");

        /// <summary>
        /// Gets the debug line.
        /// </summary>
        public byte[] DebugLine => LoadSection(".debug_line");

        /// <summary>
        /// Gets the address of exception handling frames stream after loading into memory.
        /// </summary>
        public ulong EhFrameAddress => GetSectionAddress(".eh_frame");

        /// <summary>
        /// Gets the address of text section after loading into memory.
        /// </summary>
        public ulong TextSectionAddress => GetSectionAddress(".text");

        /// <summary>
        /// Gets the address of data section after loading into memory.
        /// </summary>
        public ulong DataSectionAddress => GetSectionAddress(".data");

        /// <summary>
        /// Gets a value indicating whether this <see cref="IDwarfBinary" /> is 64 bit image.
        /// </summary>
        /// <value>
        ///   <c>true</c> if is 64 bit image; otherwise, <c>false</c>.
        /// </value>
        public bool Is64bit => ELF.Class == Class.Bit64;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            ELF.Dispose();
        }

        /// <summary>
        /// Gets address offset within module when it is loaded.
        /// </summary>
        /// <param name="address">Virtual address that points where something should be loaded.</param>
        public ulong NormalizeAddress(ulong address)
        {
            Section<ulong> section;

            section = ELF.Sections.FirstOrDefault(s => s.LoadAddress <= address && s.LoadAddress + s.Size > address);

            return section?.Flags.HasFlag(SectionFlags.Allocatable) == true
                ? address - CodeSegmentOffset + LoadOffset
                : address - CodeSegmentOffset;
        }

        /// <summary>
        /// Cache of ordered sections.
        /// </summary>
        public Section<ulong>[] SectionRegions { get; set; }

        /// <summary>
        /// Gets or sets the CompilationUnits.
        /// </summary>
        public List<DwarfCompilationUnit> CompilationUnits { get; set; }

        /// <summary>
        /// Gets or sets the LineNumberPrograms.
        /// </summary>
        public DwarfLineNumberProgram[] LineNumberPrograms { get; set; }

        /// <summary>
        /// Gets or sets the CommonInformationEntries.
        /// </summary>
        public DwarfCommonInformationEntry[] CommonInformationEntries { get; set; }

        /// <summary>
        /// Gets the Compilers.
        /// </summary>
        public ELFCompiler[] Compilers { get; }

        /// <summary>
        /// The ELF interface
        /// </summary>
        public readonly ELF<ulong> ELF;

        /// <summary>
        /// The version of Dwarf used.
        /// </summary>
        public int DwarfVersion { get; set; } = -1;

        /// <summary>
        /// The version of Dwarf used.
        /// </summary>
        public DwarfUnitType DwarfUnitType { get; set; } = DwarfUnitType.Unknown;

        /// <summary>
        /// Loads the section bytes specified by the name.
        /// </summary>
        /// <param name="sectionName">Name of the section.</param>
        /// <returns>Section bytes.</returns>
        private byte[] LoadSection(string sectionName)
        {
            foreach (ISection section in ELF.Sections)
            {
                if (section.Name == sectionName + ".dwo" || section.Name == sectionName)
                {
                    return section.GetContents();
                }
            }

            return Array.Empty<byte>();
        }

        /// <summary>
        /// Gets the section address after loading into memory.
        /// </summary>
        /// <param name="sectionName">Name of the section.</param>
        /// <returns>Address of section after loading into memory.</returns>
        private ulong GetSectionAddress(string sectionName)
        {
            foreach (Section<ulong> section in ELF.Sections)
            {
                if (section.Name == sectionName)
                {
                    ulong loadOffset = section.Flags.HasFlag(SectionFlags.Allocatable) ? LoadOffset : 0;

                    return section.Offset + CodeSegmentOffset + loadOffset;
                }
            }

            return ulong.MaxValue;
        }

        private void LoadDwo()
        {
            if (CompilationUnits == null || CompilationUnits.Count == 0)
            {
                return;
            }

            DwarfSymbol skeletonOrCompileSymbol = CompilationUnits
                .SelectMany(c => c.Symbols)
                .FirstOrDefault(s => s.Tag == DwarfTag.SkeletonUnit || s.Tag == DwarfTag.CompileUnit);
            KeyValuePair<DwarfAttribute, DwarfAttributeValue>? dwo = skeletonOrCompileSymbol?.Attributes?
                .FirstOrDefault(a => a.Key == DwarfAttribute.DwoName || a.Key == DwarfAttribute.GnuDwoName);
            string dwoName = dwo?.Value?.String ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dwoName))
            {
                var directory = new Uri(TargetUri, ".");
                var dwoUri = new Uri(Path.Combine(directory.AbsolutePath, dwoName));
                if (File.Exists(dwoUri.AbsolutePath))
                {
                    var dwoELFBinary = new ELFBinary(dwoUri);
                    DwarfCompilationUnit cwoCompileUnit = dwoELFBinary.CompilationUnits?.FirstOrDefault(c => c.Symbols.Any(s => s.Tag == DwarfTag.CompileUnit));

                    if (cwoCompileUnit != null)
                    {
                        this.CompilationUnits.Add(cwoCompileUnit);
                    }
                }
            }
        }
    }
}
