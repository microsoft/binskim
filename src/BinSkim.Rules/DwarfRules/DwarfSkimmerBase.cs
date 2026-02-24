// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class DwarfSkimmerBase : BinarySkimmer
    {
        public sealed override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            try
            {
                if (context.IsELF())
                {
                    ElfBinary target = context.ElfBinary();

                    if (target.IsDebugOnlyFile)
                    {
                        reasonForNotAnalyzing = MetadataConditions.ImageIsDebugOnly;
                        return AnalysisApplicability.NotApplicableToSpecifiedTarget;
                    }

                    return this.CanAnalyzeDwarf(target, context.Policy, out reasonForNotAnalyzing);
                }
                else if (context.IsMachO())
                {
                    MachOBinary target = context.MachOBinary();
                    return this.CanAnalyzeDwarf(target, context.Policy, out reasonForNotAnalyzing);
                }

                reasonForNotAnalyzing = MetadataConditions.ImageIsNotElf; // ImageIsNotDwarfCompatible
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
            catch (Exception)
            {
                // If anything goes wrong while loading or analyzing DWARF,
                // treat the rule as not applicable instead of surfacing
                // an ERR998-style exception to the command line.
                reasonForNotAnalyzing = MetadataConditions.ElfNotBuiltWithDwarfDebugging;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
        }

        public abstract AnalysisApplicability CanAnalyzeDwarf(IDwarfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing);
    }
}
