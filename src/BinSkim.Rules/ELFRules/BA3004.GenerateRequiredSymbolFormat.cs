// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class GenerateRequiredSymbolFormat : ELFBinarySkimmerBase
    {
        /// <summary>
        /// BA3004
        /// </summary>
        public override string Id => RuleIds.GenerateRequiredSymbolFormat;

        /// <summary>
        /// TODO: update
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3003_EnableStackProtector_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA3004_Pass),
            nameof(RuleResources.BA3004_Error),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzeElf(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            ELFBinary elfBinary = context.ELFBinary();
            int dwarfVersion = elfBinary.GetDwarfVersion();
            if (dwarfVersion == -1)
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.None, context, null,
                        nameof(RuleResources.NotApplicable_InvalidMetadata),
                        context.TargetUri.GetFileName()));
                return;
            }

            if (dwarfVersion < 5)
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA3004_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA3004_Pass),
                    context.TargetUri.GetFileName()));
        }
    }
}
