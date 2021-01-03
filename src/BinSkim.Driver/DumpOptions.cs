// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using CommandLine;

namespace Microsoft.CodeAnalysis.IL
{
    [Verb("dump", HelpText = "Dump metadata for one or more binary files.")]
    internal class DumpOptions
    {
        [Value(0,
               HelpText = "One or more specifiers to a file, directory, or filter pattern that resolves to one or more binaries to report against.")]
        public IEnumerable<string> BinaryFileSpecifiers { get; internal set; }

        [Option(
            'v',
            "verbose",
            HelpText = "Emit verbose output. The resulting comprehensive report is designed to provide appropriate evidence for compliance scenarios.")]
        public bool Verbose { get; internal set; }

        [Option(
            'r',
            "recurse",
            HelpText = "Recurse into subdirectories when evaluating file specifier arguments.")]
        public bool Recurse { get; internal set; }
    }
}

