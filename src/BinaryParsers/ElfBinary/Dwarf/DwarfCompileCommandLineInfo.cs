// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// class for Dwarf Compile Command line info
    /// </summary>
    public class DwarfCompileCommandLineInfo
    {
        public DwarfTag Type { get; set; }
        public string FullName { get; set; }
        public string FileName { get; set; }
        public string CommandLine { get; set; }
        public DwarfLanguage Language { get; set; }
        public string CompileDirectory { get; set; }
    }
}
