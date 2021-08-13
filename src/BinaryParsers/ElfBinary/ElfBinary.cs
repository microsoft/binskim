// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;
using Microsoft.CodeAnalysis.BinaryParsers.Elf;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    /// <summary>
    /// Simple ELF image reader.
    /// </summary>
    public class ElfBinary : BinaryBase, IDwarfBinary
    {
        public ElfBinary(Uri uri, string localSymbolDirectories = null) : base(uri)
        {
            try
            {
                string path = Path.GetFullPath(uri.LocalPath);
                ELF = ELFReader.Load<ulong>(path);

                Compilers = ElfUtility.GetELFCompilers(this.ELF);

                foreach (Segment<ulong> segment in ELF.Segments)
                {
                    if (segment.Type == SegmentType.ProgramHeader)
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

                CompilationUnits = DwarfSymbolProvider.ParseAllCompilationUnits(this, DebugData, DebugDataDescription, DebugDataStrings, NormalizeAddress);
                commandLineInfos = new Lazy<List<DwarfCompileCommandLineInfo>>(()
                    => DwarfSymbolProvider.ParseAllCommandLineInfos(CompilationUnits));
                LineNumberPrograms = DwarfSymbolProvider.ParseLineNumberPrograms(DebugLine, NormalizeAddress);
                CommonInformationEntries = DwarfSymbolProvider.ParseCommonInformationEntries(DebugFrame, EhFrame, new DwarfExceptionHandlingFrameParsingInput(this));
                LoadDebug(localSymbolDirectories);
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

        public DwarfLanguage GetLanguage()
        {
            if (CompilationUnits.Count == 0)
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

        public List<SymbolEntry<ulong>> GetSymbolTableFiles()
        {
            SymbolTable<ulong> symbolTableSection = ELF.Sections.FirstOrDefault(s => s.Type == SectionType.SymbolTable) as SymbolTable<ulong>;

            return symbolTableSection == null
                ? new List<SymbolEntry<ulong>>()
                : symbolTableSection.Entries.Where(e => e.Type == SymbolType.File && !string.IsNullOrWhiteSpace(e.Name))
                .GroupBy(x => x.Name).Select(x => x.First()).ToList();
        }

        public SegmentFlags? GetSegmentFlags(ElfSegmentType segmentType)
        {
            ISegment segment = ELF.Segments?.FirstOrDefault(s => (uint)s.Type == (uint)segmentType);

            return segment == null ? null : (SegmentFlags?)segment.Flags;
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
        public byte[] DebugData => LoadSection(SectionName.DebugInfo);

        /// <summary>
        /// Gets the debug data description.
        /// </summary>
        public byte[] DebugDataDescription => LoadSection(SectionName.DebugAbbrev);

        /// <summary>
        /// Gets the debug data strings.
        /// </summary>
        public byte[] DebugDataStrings => LoadSection(SectionName.DebugStr);

        /// <summary>
        /// Gets the debug frame.
        /// </summary>
        public byte[] DebugFrame => LoadSection(SectionName.DebugFrame);

        /// <summary>
        /// Gets the exception handling frames used for unwinding (generated by usually GCC compiler).
        /// </summary>
        public byte[] EhFrame => LoadSection(SectionName.EhFrame);

        /// <summary>
        /// Gets the debug line.
        /// </summary>
        public byte[] DebugLine => LoadSection(SectionName.DebugLine);

        /// <summary>
        /// Gets the type of the file related to debug information
        /// </summary>
        public DebugFileType DebugFileType { get; private set; }

        /// <summary>
        /// Gets if the debug information loaded successfully
        /// </summary>
        public bool DebugFileLoaded { get; private set; }

        /// <summary>
        /// Gets the address of exception handling frames stream after loading into memory.
        /// </summary>
        public ulong EhFrameAddress => GetSectionAddress(SectionName.EhFrame);

        /// <summary>
        /// Gets the address of text section after loading into memory.
        /// </summary>
        public ulong TextSectionAddress => GetSectionAddress(SectionName.Text);

        /// <summary>
        /// Gets the address of data section after loading into memory.
        /// </summary>
        public ulong DataSectionAddress => GetSectionAddress(SectionName.Data);

        /// <summary>
        /// Gets a value indicating whether this <see cref="IDwarfBinary" /> is 64 bit image.
        /// </summary>
        /// <value>
        ///   <c>true</c> if is 64 bit image; otherwise, <c>false</c>.
        /// </value>
        public bool Is64bit => ELF.Class == Class.Bit64;

        /// <summary>
        /// Gets a value indicating whether this <see cref="IDwarfBinary" /> is debug only file.
        /// </summary>
        /// <value>
        ///   <c>true</c> if is debug only file; otherwise, <c>false</c>.
        /// </value>
        public bool IsDebugOnlyFile => DebugFileType == DebugFileType.DebugOnlyFileDebuglink || DebugFileType == DebugFileType.DebugOnlyFileDwo;

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
        public List<DwarfCompilationUnit> CompilationUnits { get; set; } = new List<DwarfCompilationUnit>();

        /// <summary>
        /// Gets or sets the CommandLineInfos.
        /// </summary>
        public List<DwarfCompileCommandLineInfo> CommandLineInfos => this.commandLineInfos.Value;

        /// <summary>
        /// Gets or sets the LineNumberPrograms.
        /// </summary>
        public IReadOnlyList<DwarfLineNumberProgram> LineNumberPrograms { get; set; }

        /// <summary>
        /// Gets or sets the CommonInformationEntries.
        /// </summary>
        public IReadOnlyList<DwarfCommonInformationEntry> CommonInformationEntries { get; set; }

        /// <summary>
        /// Gets the Compilers.
        /// </summary>
        public ICompiler[] Compilers { get; }

        /// <summary>
        /// The ELF interface
        /// </summary>
        public readonly ELF<ulong> ELF;

        /// <summary>
        /// The version of Dwarf used.
        /// </summary>
        public int DwarfVersion { get; set; } = -1;

        /// <summary>
        /// The unit type of Dwarf.
        /// </summary>
        public DwarfUnitType DwarfUnitType { get; set; } = DwarfUnitType.Unknown;

        /// <summary>
        /// Get if section exists
        /// </summary>
        private bool SectionExists(string sectionName)
        {
            ELF.TryGetSection(sectionName, out Section<ulong> sectionRetrieved);
            return sectionRetrieved != null;
        }

        /// <summary>
        /// Get if section exists and also has bits
        /// </summary>
        private bool SectionExistsAndHasBits(string sectionName, out Section<ulong> section)
        {
            ELF.TryGetSection(sectionName, out Section<ulong> sectionRetrieved);
            section = sectionRetrieved;
            return sectionRetrieved != null && sectionRetrieved.Type != SectionType.NoBits;
        }

        /// <summary>
        /// Get if section exists and also has bits
        /// </summary>
        private bool SectionExistsAndHasBits(string sectionName)
        {
            return SectionExistsAndHasBits(sectionName, out _);
        }

        /// <summary>
        /// Get if section exists and also has no bits
        /// </summary>
        private bool SectionExistsAndHasNoBits(string sectionName)
        {
            ELF.TryGetSection(sectionName, out Section<ulong> sectionRetrieved);
            return sectionRetrieved != null && sectionRetrieved.Type == SectionType.NoBits;
        }

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

        private void LoadDebug(string localSymbolDirectories = null)
        {
            DebugFileType = DebugFileType.Unknown;
            DebugFileLoaded = false;

            if (SectionExistsAndHasBits(SectionName.DebugInfoDwo))
            {
                DebugFileType = DebugFileType.DebugOnlyFileDwo;
                return;
            }

            string debugFileName = null;

            if (CompilationUnits.Count > 0)
            {
                // Load from Dwo
                DwarfSymbol skeletonOrCompileSymbol = CompilationUnits
                .SelectMany(c => c.Symbols)
                .FirstOrDefault(s => s.Tag == DwarfTag.SkeletonUnit || s.Tag == DwarfTag.CompileUnit);
                KeyValuePair<DwarfAttribute, DwarfAttributeValue>? dwo = skeletonOrCompileSymbol?.Attributes?
                    .FirstOrDefault(a => a.Key == DwarfAttribute.DwoName || a.Key == DwarfAttribute.GnuDwoName);
                debugFileName = (dwo == null || !dwo.HasValue) ? string.Empty : dwo.Value.Value?.String;
                if (!string.IsNullOrWhiteSpace(debugFileName))
                {
                    DebugFileType = DebugFileType.FromDwo;
                }
            }

            if (string.IsNullOrWhiteSpace(debugFileName))
            {
                // Load from gnu_debuglink
                if (SectionExistsAndHasBits(SectionName.GnuDebugLink, out Section<ulong> debugLinkSection))
                {
                    byte[] debugLinkSectionContents = debugLinkSection.GetContents();

                    if (debugLinkSectionContents != null && debugLinkSectionContents.Length > 0)
                    {
                        string debuglinkFileName = Encoding.ASCII.GetString(debugLinkSectionContents.TakeWhile(x => x != 0).ToArray());

                        if (!string.IsNullOrWhiteSpace(debuglinkFileName))
                        {
                            debugFileName = debuglinkFileName;
                            DebugFileType = DebugFileType.FromDebuglink;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(debugFileName))
            {
                var directory = new Uri(TargetUri, ".");
                Uri debugFileUri = GetFirstExistFile(debugFileName, directory.AbsolutePath, localSymbolDirectories);
                if (debugFileUri != null)
                {
                    var dwoBinary = new ElfBinary(debugFileUri);

                    if (dwoBinary != null && dwoBinary.CompilationUnits.Count > 0)
                    {
                        this.CompilationUnits.AddRange(dwoBinary.CompilationUnits);

                        if (dwoBinary.CommandLineInfos.Count > 0)
                        {
                            this.CommandLineInfos.AddRange(dwoBinary.CommandLineInfos);
                        }

                        DebugFileLoaded = true;
                        return;
                    }
                }
            }

            if (DebugFileType == DebugFileType.Unknown)
            {
                if (SectionExistsAndHasNoBits(SectionName.Interp) &&
                    SectionExistsAndHasNoBits(SectionName.Dynsym) &&
                    SectionExistsAndHasNoBits(SectionName.Init) &&
                    SectionExistsAndHasNoBits(SectionName.Data) &&
                    SectionExistsAndHasBits(SectionName.DebugInfo)
                    )
                {
                    DebugFileType = DebugFileType.DebugOnlyFileDebuglink;
                }
                else if (SectionExistsAndHasBits(SectionName.Interp) &&
                    SectionExistsAndHasBits(SectionName.Dynsym) &&
                    SectionExistsAndHasBits(SectionName.Init) &&
                    SectionExistsAndHasBits(SectionName.Data) &&
                    SectionExistsAndHasBits(SectionName.DebugInfo)
                    )
                {
                    DebugFileType = DebugFileType.DebugIncluded;
                    DebugFileLoaded = true;
                }
                else if (SectionExistsAndHasBits(SectionName.Interp) &&
                   SectionExistsAndHasBits(SectionName.Dynsym) &&
                   SectionExistsAndHasBits(SectionName.Init) &&
                   SectionExistsAndHasBits(SectionName.Data) &&
                   !SectionExistsAndHasBits(SectionName.DebugInfo)
                   )
                {
                    DebugFileType = DebugFileType.NoDebug;
                }
            }
        }

        private readonly Lazy<List<DwarfCompileCommandLineInfo>> commandLineInfos;

        private static Uri GetFirstExistFile(string dwoName, string sameDirectory, string localSymbolDirectories = null)
        {
            List<string> searchpathList = new List<string>();

            if (!string.IsNullOrWhiteSpace(localSymbolDirectories))
            {
                searchpathList.AddRange(localSymbolDirectories.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            }

            searchpathList.Add(sameDirectory);

            foreach (string path in searchpathList)
            {
                if (Directory.Exists(path))
                {
                    string file = Directory.GetFiles(path, dwoName, SearchOption.AllDirectories).FirstOrDefault();
                    if (file != null)
                    {
                        return new Uri(file);
                    }
                }
            }

            return null;
        }
    }
}
