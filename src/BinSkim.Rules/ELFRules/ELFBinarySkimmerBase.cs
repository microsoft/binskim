// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class ELFBinarySkimmerBase : BinarySkimmer
    {
        public sealed override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            if (context.IsELF())
            {
                ELFBinary target = context.ELFBinary();
                return this.CanAnalyzeElf(target, context.Policy, out reasonForNotAnalyzing);
            }
            else
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsNotElf;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
        }

        public abstract AnalysisApplicability CanAnalyzeElf(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing);
    }
}
