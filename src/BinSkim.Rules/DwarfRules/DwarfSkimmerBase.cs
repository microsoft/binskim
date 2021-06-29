// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            if (context.IsELF())
            {
                ElfBinary target = context.ELFBinary();
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

        public abstract AnalysisApplicability CanAnalyzeDwarf(IDwarfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing);
    }
}
