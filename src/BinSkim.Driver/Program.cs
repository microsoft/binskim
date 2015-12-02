// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.CodeAnalysis.IL.Rules;

using CommandLine;


namespace Microsoft.CodeAnalysis.IL
{
    internal class Program
    {
        private static Assembly[] s_defaultCompositionAssemblies =
                                        new Assembly[] {
                                            typeof(MarkImageAsNXCompatible).Assembly
                                        };

        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<
                AnalyzeOptions,
                ExportOptions,
                DumpOptions>(args)
              .MapResult(
                (AnalyzeOptions analyzeOptions) => new AnalyzeCommand().Run(analyzeOptions),
                (ExportOptions exportOptions) => new ExportCommand().Run(exportOptions),
                (DumpOptions dumpOptions) => new DumpCommand().Run(dumpOptions),
                errs => 1);
        }
    }
}
