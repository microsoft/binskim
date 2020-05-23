/// <summary>
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(IOptionsProvider))]
    [Export(typeof(ReportingDescriptor))]
    [Export(typeof(Skimmer<BinaryAnalyzerContext>))]
    public class EnableControlEnforcementTechnologyShadowStack : PEBinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2025
        /// </summary>
        public override string Id => RuleIds.EnableControlEnforcementTechnologyShadowStack;

        /// <summary>
        /// Recapitulate the full text of the rule description returned below
        /// here as a summary comment.
        /// </summary>

        public override MultiformatMessageString FullDescription => 
            new MultiformatMessageString { Text = RuleResources.BA2025_EnableControlEnforcementTechnologyShadowStack_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2025_Pass),
                    nameof(RuleResources.BA2025_Error)
                };

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                MinimumRequiredLinkerVersion
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.EnableControlEnforcementTechnologyShadowStack + "." + nameof(EnableControlEnforcementTechnologyShadowStack);

        public static PerLanguageOption<Version> MinimumRequiredLinkerVersion { get; } =
            new PerLanguageOption<Version>(
                AnalyzerName, nameof(MinimumRequiredLinkerVersion), defaultValue: () => new Version("14.0"));

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            // Review the range of metadata conditions and return NotApplicableToSpecifiedTarget
            // from this method for all cases where a binary is detected that is not valid to scan.
            //
            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            Version minimumRequiredLinkerVersion = policy.GetProperty(MinimumRequiredLinkerVersion);

            if (portableExecutable.LinkerVersion < minimumRequiredLinkerVersion)
            {
                reasonForNotAnalyzing = string.Format(
                    MetadataConditions.ImageCompiledWithOutdatedTools,
                    portableExecutable.LinkerVersion,
                    minimumRequiredLinkerVersion);

                return result;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;

            if (!this.IsCETShadowStackEnabled(target))
            {
                // '{0}' is not secure for some reaons. 
                // To resolve this issue, pass /beEvenMoreSecure on both the compiler
                // and linker command lines. Binaries also require the 
                // /beSecure option in order to enable the enhanced setting.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Note, context, null,
                        nameof(RuleResources.BA2025_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' enables /beEvenMoreSecure on both the compiler and linker
            // command-lines, preventing a broad range of conditions that 
            // bad actors can use to engage in their malignant, unfortunately
            // often-profitable foolishness.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2025_Pass),
                        context.TargetUri.GetFileName()));
        }

        private static bool IsCETShadowStackEnabled(PEBinary _)
        {
            // Add relevant PE-level examination 
            return false;
        }
    }
}
