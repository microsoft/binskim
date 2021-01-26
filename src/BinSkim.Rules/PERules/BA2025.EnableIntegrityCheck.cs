// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableIntegrityCheck : PEBinarySkimmerBase
    {
        /// <summary>
        /// https://docs.microsoft.com/windows/win32/debug/pe-format#dll-characteristics
        /// </summary>
        private const string ImageDllCharacteristicsForceIntegrity = "128";

        /// <summary>
        /// BA2025
        /// </summary>
        public override string Id => RuleIds.EnableIntegrityCheck;

        /// <summary>
        /// TODO
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2025_EnableIntegrityCheck_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2025_Note_IntegrityCheckShouldBeEnabled),
            nameof(RuleResources.BA2025_Pass),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PEHeader peHeader = target.PE.PEHeaders.PEHeader;

            DllCharacteristics dllCharacterstics = peHeader.DllCharacteristics;
            if (!dllCharacterstics.HasFlag((DllCharacteristics)Enum.Parse(typeof(DllCharacteristics), ImageDllCharacteristicsForceIntegrity)))
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Note, context, null,
                        nameof(RuleResources.BA2025_Note_IntegrityCheckShouldBeEnabled),
                        context.TargetUri.GetFileName()));
                return;
            }

            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2025_Pass),
                        context.TargetUri.GetFileName()));
        }

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }
    }
}
