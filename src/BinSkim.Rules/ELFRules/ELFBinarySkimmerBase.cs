// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class ELFBinarySkimmerBase : BinarySkimmerBase
    {
        public sealed override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            if (context.IsELF())
            {
                ELFBinary target = context.ELFBinary();
                return CanAnalyzeELF(target, context.Policy, out reasonForNotAnalyzing);
            }
            else
            {
                // TODO--Resources file
                reasonForNotAnalyzing = "TODO--Resources file \"Not supported on this binary type.\"";
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
        }

        public abstract AnalysisApplicability CanAnalyzeELF(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing);
    }
    
}
