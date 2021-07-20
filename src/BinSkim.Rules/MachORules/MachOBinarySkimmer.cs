// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class MachOBinarySkimmer : BinarySkimmer
    {
        public sealed override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            if (context.IsMachO())
            {
                MachOBinary target = context.MachOBinary();
                return this.CanAnalyze(target, context.Policy, out reasonForNotAnalyzing);
            }

            reasonForNotAnalyzing = MetadataConditions.ImageIsNotMachO;
            return AnalysisApplicability.NotApplicableToSpecifiedTarget;
        }

        public abstract AnalysisApplicability CanAnalyze(MachOBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing);
    }
}
