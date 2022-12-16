// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Elf
{
    public static class SectionName
    {
        public const string Data = ".data";
        public const string Init = ".init";
        public const string Text = ".text";
        public const string Interp = ".interp";
        public const string Dynsym = ".dynsym";
        public const string EhFrame = ".eh_frame";
        public const string DebugStr = ".debug_str";
        public const string DebugInfo = ".debug_info";
        public const string DebugLine = ".debug_line";
        public const string DebugFrame = ".debug_frame";
        public const string DebugAbbrev = ".debug_abbrev";
        public const string GnuDebugLink = ".gnu_debuglink";
        public const string DebugLineStr = ".debug_line_str";
        public const string DebugInfoDwo = ".debug_info.dwo";
        public const string DebugStrOffsets = ".debug_str_offsets";
    }
}
