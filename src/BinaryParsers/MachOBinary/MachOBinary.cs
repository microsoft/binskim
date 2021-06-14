// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ELFSharp.MachO;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class MachOBinary : BinaryBase, IDwarfBinary
    {
        private const string SECTIONNAME_DEBUG_INFO = "__debug_info";
        private const string SECTIONNAME_DEBUG_ABBREV = "__debug_abbrev";
        private const string SECTIONNAME_DEBUG_STR = "__debug_str";
        private const string SECTIONNAME_DEBUG_LINE = "__debug_line";
        private const string SECTIONNAME_DEBUG_FRAME = "__debug_frame";
        private const string SECTIONNAME_EH_FRAME = "__eh_frame";
        private const string SECTIONNAME_TEXT = "__text";
        private const string SECTIONNAME_DATA = "__data";

        public MachOBinary(Uri uri) : base(uri)
        {
            try
            {
                this.MachO = MachOReader.Load(Path.GetFullPath(uri.LocalPath));

                this.Segments = this.MachO.GetCommandsOfType<Segment>();
                this.IdDylibs = this.MachO.GetCommandsOfType<IdDylib>();
                this.LoadDylibs = this.MachO.GetCommandsOfType<LoadDylib>();
                this.EntryPoint = this.MachO.GetCommandsOfType<EntryPoint>();
                this.SymbolTables = this.MachO.GetCommandsOfType<SymbolTable>();
                this.LoadWeakDylib = this.MachO.GetCommandsOfType<LoadWeakDylib>();
                this.ReexportDylibs = this.MachO.GetCommandsOfType<ReexportDylib>();

                this.Compilers = this.GetCompilers();

                this.Valid = true;
            }
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
                return MachOReader.TryLoad(Path.GetFullPath(uri.LocalPath), out _) == MachOResult.OK;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public MachO MachO { get; }

        public ICompiler[] Compilers { get; }

        public IEnumerable<Segment> Segments { get; }

        public IEnumerable<SymbolTable> SymbolTables { get; }

        public IEnumerable<IdDylib> IdDylibs { get; }

        public IEnumerable<LoadDylib> LoadDylibs { get; }

        public IEnumerable<LoadWeakDylib> LoadWeakDylib { get; }

        public IEnumerable<ReexportDylib> ReexportDylibs { get; }

        public IEnumerable<EntryPoint> EntryPoint { get; }

        public List<DwarfCompilationUnit> CompilationUnits
        {
            get
            {
                byte[] debugData = this.LoadSection(SECTIONNAME_DEBUG_INFO);
                byte[] debugAbbrev = this.LoadSection(SECTIONNAME_DEBUG_ABBREV);
                byte[] debugStr = this.LoadSection(SECTIONNAME_DEBUG_STR);
                return DwarfSymbolProvider.ParseCompilationUnits(this, debugData, debugAbbrev, debugStr, NormalizeAddress);
            }
        }

        public List<DwarfLineNumberProgram> LineNumberPrograms
        {
            get
            {
                byte[] debugData = this.LoadSection(SECTIONNAME_DEBUG_LINE);
                return DwarfSymbolProvider.ParseLineNumberPrograms(debugData, NormalizeAddress);
            }
        }

        public List<DwarfCommonInformationEntry> CommonInformationEntries
        {
            get
            {
                byte[] debugFrame = this.LoadSection(SECTIONNAME_DEBUG_FRAME);
                byte[] ehFrame = this.LoadSection(SECTIONNAME_EH_FRAME);
                return DwarfSymbolProvider.ParseCommonInformationEntries(debugFrame, ehFrame, new DwarfExceptionHandlingFrameParsingInput(this));
            }
        }

        #region IDwarfBinary interface
        /// <summary>
        /// The version of Dwarf used.
        /// </summary>
        public int DwarfVersion { get; set; } = -1;

        /// <summary>
        /// Unit type of Dwarf used..
        /// </summary>
        public DwarfUnitType DwarfUnitType { get; set; } = DwarfUnitType.Unknown;

        /// <summary>
        /// Gets address offset within module when it is loaded.
        /// </summary>
        /// <param name="address">Virtual address that points where something should be loaded.</param>
        public ulong NormalizeAddress(ulong address)
        {
            ulong codeSegmentOffset = 0;
            Section section = null;
            foreach (Segment seg in this.Segments)
            {
                section = seg.Sections.FirstOrDefault(sec => sec.Address <= address && sec.Address + sec.Size > address);
                if (section != null)
                {
                    codeSegmentOffset = seg.Address - seg.FileOffset;
                    break;
                }
            }

            return section != null ? address - codeSegmentOffset : 0;
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

        byte[] IDwarfBinary.DebugData => throw new NotImplementedException();

        byte[] IDwarfBinary.DebugDataDescription => throw new NotImplementedException();

        byte[] IDwarfBinary.DebugDataStrings => throw new NotImplementedException();

        byte[] IDwarfBinary.DebugLine => throw new NotImplementedException();

        byte[] IDwarfBinary.DebugFrame => throw new NotImplementedException();

        byte[] IDwarfBinary.EhFrame => throw new NotImplementedException();

        ulong IDwarfBinary.CodeSegmentOffset => throw new NotImplementedException();

        ulong IDwarfBinary.EhFrameAddress => this.GetSectionAddress(SECTIONNAME_EH_FRAME);

        ulong IDwarfBinary.TextSectionAddress => this.GetSectionAddress(SECTIONNAME_TEXT);

        ulong IDwarfBinary.DataSectionAddress => this.GetSectionAddress(SECTIONNAME_DATA);

        IReadOnlyList<DwarfPublicSymbol> IDwarfBinary.PublicSymbols => throw new NotImplementedException();

        bool IDwarfBinary.Is64bit => this.MachO.Is64;

        #endregion

        private byte[] LoadSection(string sectionName)
        {
            Section section = this.Segments.SelectMany(seg => seg.Sections.ToList())
                                           .Where(sec => sec.Name == sectionName || sec.Name == sectionName + ".dwo")
                                           .FirstOrDefault();

            return section != null ? section.GetData() : Array.Empty<byte>();
        }

        private ulong GetSectionAddress(string sectionName)
        {
            foreach (Segment segment in this.Segments)
            {
                Section section = segment.Sections.Where(sec => sec.Name == sectionName).FirstOrDefault();
                if (section != null)
                {
                    ulong CodeSegmentOffset = segment.Address - (ulong)segment.FileOffset;
                    ulong LoadOffset = 0;
                    ulong loadOffset = ((0 /*section.Flags*/ & 0x2) > 0) ? LoadOffset : 0;

                    return section.Offset + CodeSegmentOffset + loadOffset;
                }
            }

            return ulong.MaxValue;
        }

        private MachOCompiler[] GetCompilers()
        {
            try
            {
                string compilerString = this.GetDwarfCompilerCommand(); // return empty string if not found
                return new MachOCompiler[] { new MachOCompiler(compilerString) };
            }
            // Catch cases when the compiler string is not formatted the way we expect it to be.
            catch (Exception ex) when (ex is ArgumentException || ex is ArgumentNullException)
            {
                return new MachOCompiler[] { new MachOCompiler(string.Empty) };
            }
        }
    }
}
