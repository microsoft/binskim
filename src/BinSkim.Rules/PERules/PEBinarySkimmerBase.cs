// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
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
                try
                {
                    return target.PE?.IsPEFile == true
                        ? this.CanAnalyzePE(target, context, out reasonForNotAnalyzing)
                        : AnalysisApplicability.NotApplicableToSpecifiedTarget;
                }
                catch (Exception ex)
                {
                    if (context.IgnorePELoadError)
                    {
                        LogExceptionInCanAnalyzeError(context, ex);
                        return AnalysisApplicability.NotApplicableToSpecifiedTarget;
                    }
                    throw;
                }

            }
            else
            {
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
        }

        public static void LogExceptionInCanAnalyzeError(IAnalysisContext context, Exception ex)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // The exception may have resulted from a problem related to parsing the analysis target and not specific to the rule, however..
            context.Logger.LogConfigurationNotification(
                Errors.CreateNotification(
                    context.CurrentTarget.Uri,
                    "ERR998.ExceptionInCanAnalyze",
                    string.Empty,
                    FailureLevel.Error,
                    ex,
                    persistExceptionStack: false,
                    RuleResources.ERR998_ExceptionInCanAnalyze,
                    context.CurrentTarget.Uri.GetFileName(),
                    ex.ToString()));

            if (context is BinaryAnalyzerContext binaryAnalyzerContext && !binaryAnalyzerContext.IgnorePELoadError)
            {
                context.RuntimeErrors |= RuntimeConditions.ExceptionRaisedInSkimmerCanAnalyze;
            }
        }

        public abstract AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing);
    }
}
