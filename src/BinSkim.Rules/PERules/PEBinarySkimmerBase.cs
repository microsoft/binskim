using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class PEBinarySkimmerBase : BinarySkimmer
    {
        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = MetadataConditions.ImageIsNotPE;

            if (context.IsPE())
            {
                PEBinary target = context.PEBinary();
                return target.PE != null && target.PE.IsPEFile
                    ? this.CanAnalyzePE(target, context.Policy, out reasonForNotAnalyzing)
                    : AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
            else
            {
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
        }

        public abstract AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing);
    }
}
