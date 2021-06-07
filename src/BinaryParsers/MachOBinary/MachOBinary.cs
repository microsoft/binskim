// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

using ELFSharp.MachO;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class MachOBinary : BinaryBase
    {
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

        public IEnumerable<Segment> Segments { get; }

        public IEnumerable<SymbolTable> SymbolTables { get; }

        public IEnumerable<IdDylib> IdDylibs { get; }

        public IEnumerable<LoadDylib> LoadDylibs { get; }

        public IEnumerable<LoadWeakDylib> LoadWeakDylib { get; }

        public IEnumerable<ReexportDylib> ReexportDylibs { get; }

        public IEnumerable<EntryPoint> EntryPoint { get; }
    }
}
