// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif.Driver;

using CommandLine;

namespace Microsoft.CodeAnalysis.IL
{
    internal class BinSkim
    {        
        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<
                AnalyzeOptions,
                ExportRulesMetadataOptions,
                ExportConfigurationOptions,
                DumpOptions>(args)
              .MapResult(
                (AnalyzeOptions analyzeOptions) => new AnalyzeCommand().Run(analyzeOptions),
                (ExportRulesMetadataOptions exportRulesMetadataOptions) => new ExportRulesMetadataCommand().Run(exportRulesMetadataOptions),
                (ExportConfigurationOptions exportConfigurationOptions) => new ExportConfigurationCommand().Run(exportConfigurationOptions),
                (DumpOptions dumpOptions) => new DumpCommand().Run(dumpOptions),
                errs => 1);
        }
    }
}
