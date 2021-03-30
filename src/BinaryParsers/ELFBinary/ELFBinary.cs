// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ELFSharp.ELF;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class ELFBinary : BinaryBase
    {
        public ELFBinary(Uri uri) : base(uri)
        {
            try
            {
                this.ELF = ELFReader.Load(Path.GetFullPath(uri.LocalPath));
                this.Compilers = ELFUtility.GetELFCompilers(this.ELF);
                ELFUtility.GetDebugInfo(this.ELF, out List<byte> debugStr, out List<Abbreviation> abbreviations, out List<CompilationUnit> compilationUnits);

                DebugStr = debugStr;
                Abbreviations = abbreviations;
                CompilationUnits = compilationUnits;

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
            if (CompilationUnits == null || CompilationUnits.Count == 0)
            {
                return -1;
            }

            return CompilationUnits[0].CompilationUnitHeader?.Version ?? -1;
        }

        public string GetDwarfCompilerCommand()
        {
            if (CompilationUnits == null || CompilationUnits.Count == 0 ||
                DebugStr == null || DebugStr.Count == 0)
            {
                return null;
            }

            CompilationUnit compilationUnit = CompilationUnits.FirstOrDefault(c => c.DebuggingInformationEntries.Any(d => d.AttributeList.Any(a => a.Name == DW_AT.Producer)));
            if (compilationUnit == null)
            {
                return null;
            }

            DebuggingInformationEntry debuggingInformationEntry = compilationUnit.DebuggingInformationEntries.First(d => d.AttributeList.Any(a => a.Name == DW_AT.Producer));
            Dwarf.Attribute attribute = debuggingInformationEntry.AttributeList.First(a => a.Name == DW_AT.Producer);

            return debuggingInformationEntry.GetName(DebugStr, attribute);
        }

        public string GetLanguage()
        {
            if (CompilationUnits == null || CompilationUnits.Count == 0)
            {
                return null;
            }

            CompilationUnit compilationUnit = CompilationUnits.FirstOrDefault(c => c.DebuggingInformationEntries.Any(d => d.AttributeList.Any(a => a.Name == DW_AT.Language)));
            if (compilationUnit == null)
            {
                return null;
            }

            DebuggingInformationEntry debuggingInformationEntry = compilationUnit.DebuggingInformationEntries.First(d => d.AttributeList.Any(a => a.Name == DW_AT.Language));
            Dwarf.Attribute attribute = debuggingInformationEntry.AttributeList.First(a => a.Name == DW_AT.Language);

            return ((DW_LANGUAGE)attribute.Value[0]).ToString();
        }

        public IELF ELF { get; }
        public ELFCompiler[] Compilers { get; }
        public List<Abbreviation> Abbreviations { get; }
        public List<CompilationUnit> CompilationUnits { get; }
        private List<byte> DebugStr { get; }
    }
}
