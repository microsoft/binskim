// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public struct CompilerData
    {
        public string Dialect { get; set; }
        public string Language { get; set; }
        public string BinaryType { get; set; }
        public string ModuleName { get; set; }
        public string CommandLine { get; set; }
        public string FileVersion { get; set; }
        public string CompilerName { get; set; }
        public string ModuleLibrary { get; set; }
        public string DebuggingFileName { get; set; }
        public string DebuggingFileGuid { get; set; }
        public string AssemblyReferences { get; set; }
        public string CompilerBackEndVersion { get; set; }
        public string CompilerFrontEndVersion { get; set; }

        public override string ToString()
        {
            return $"{CompilerName},{CompilerBackEndVersion},{CompilerFrontEndVersion},{FileVersion},{BinaryType},{Language}," +
                $"{DebuggingFileName},{DebuggingFileGuid},{CommandLine?.Replace(",", " ")},{Dialect},{ModuleName}," +
                $"{(ModuleLibrary == ModuleName ? string.Empty : ModuleLibrary)},{AssemblyReferences}";
        }
    }
}
