// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Sarif.Driver;

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

        public string GetDialect()
        {
            if (ElfUtility.GetDwarfCommandLineType(CommandLine) != DwarfCommandLineType.Gcc)
            {
                return string.Empty;
            }

            List<string> args = ArgumentSplitter.CommandLineToArgvW(this.CommandLine);
            return args.Count > 2 ? args[1] : string.Empty;
        }

        public override string ToString()
        {
            return FileName;
        }
    }
}
