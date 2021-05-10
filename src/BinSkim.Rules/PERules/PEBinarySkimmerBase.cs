using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class PEBinarySkimmerBase : BinarySkimmer
    {
        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            if (context.IsPE())
            {
                PEBinary target = context.PEBinary();
                return this.CanAnalyzePE(target, context.Policy, out reasonForNotAnalyzing);
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
