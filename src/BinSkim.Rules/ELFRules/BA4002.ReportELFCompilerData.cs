// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class ReportELFCompilerData : ELFBinarySkimmerBase
    {
        /// <summary>
        /// BA4002
        /// </summary>
        public override string Id => RuleIds.ReportELFCompilerData;

        /// <summary>
        /// This rule emits CSV data to the console 
        /// for every compiler/language/version combination that's observed.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA4002_ReportELFCompilerData_Description };

        public override bool EnabledByDefault => false;

        protected override IEnumerable<string> MessageResourceNames => Array.Empty<string>();

        private bool PrintHeader = true;

        public override AnalysisApplicability CanAnalyzeElf(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            ELFBinary elfBinary = context.ELFBinary();
            DwarfLanguage dwarfLanguage = elfBinary.GetLanguage();
            string dwarfCompilerCommand = elfBinary.GetDwarfCompilerCommand();

            if (PrintHeader)
            {
                Console.WriteLine("Target,Compiler Name,Compiler Version,Dwarf Version,Language,Compiler Command");
                PrintHeader = false;
            }
            foreach (ELFCompiler compiler in elfBinary.Compilers)
            {
                if (compiler.Compiler == ELFCompilerType.Unknown)
                {
                    continue;
                }
                Console.Write($"{context.TargetUri.LocalPath},");
                Console.Write($"{compiler.Compiler},");
                Console.Write($"{compiler.Version},");
                Console.Write($"{elfBinary.DwarfVersion},");
                Console.Write($"{dwarfLanguage},");
                Console.Write($"{dwarfCompilerCommand}");
                Console.WriteLine();
            }
        }
    }
}
