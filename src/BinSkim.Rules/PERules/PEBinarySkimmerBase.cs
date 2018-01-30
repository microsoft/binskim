using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class PEBinarySkimmerBase : BinarySkimmerBase
    {
        public sealed override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            if (context.IsPE())
            {
                PEBinary target = context.PEBinary();
                return CanAnalyzePE(target, context.Policy, out reasonForNotAnalyzing);
            }
            else
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsNotPE;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
        }

        public abstract AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing);
    }
}
