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
    // Delete the following attribute if this rule doesn't require special
    // configuration capabilities. Delete this code comment in all cases.
    //
    // You should extend this class from WindowsBinaryAndPdbSkimmerBase 
    // instead of PEBinarySkimmerBase if you require PDB parsing in 
    // your check. Extend ELFBinarySkimmerBase for *nix binary checks.
    // 
    [Export(typeof(IOptionsProvider))]
    [Export(typeof(ReportingDescriptor))]
    [Export(typeof(Skimmer<BinaryAnalyzerContext>))]
    public class RULEFRIENDLYNAME : PEBinarySkimmerBase, IOptionsProvider /* Delete this if no special configuration required */
    {
        /// <summary>
        /// BAXXXX
        /// </summary>
        public override string Id => RuleIds.RULEFRIENDLYNAME;

        /// <summary>
        /// Recapitulate the full text of the rule description returned below
        /// here as a summary comment.
        /// </summary>

        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BAXXXX_RULEFRIENDLYNAME_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BAXXXX_Pass),
                    nameof(RuleResources.BAXXXX_Error)
                };

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                MinimumRequiredLinkerVersion
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.RULEFRIENDLYNAMEId + "." + nameof(RULEFRIENDLYNAME);

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

            // Here's an example of parameterizing a rule from input XML. In this example,
            // we enforce that the linker is of a minimal version, otherwise the scan will
            // not occur (because the toolset producing the binary is not sufficiently 
            // current to enable the security mitigation).
            //
            Version minimumRequiredLinkerVersion = policy.GetProperty(MinimumRequiredLinkerVersion);

            if (portableExecutable.LinkerVersion < minimumRequiredLinkerVersion)
            {
                reasonForNotAnalyzing = string.Format(
                    MetadataConditions.ImageCompiledWithOutdatedTools,
                    portableExecutable.LinkerVersion,
                    minimumRequiredLinkerVersion);

                return result;
            }

            // If we get to this location, we've determined the binary is valid to analyze.
            // We clear the 'reasonForNotAnalyzing' output variable and return 
            // ApplicableToSpecifiedTarget.
            //
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;

            // Analysis may return one or more failures, each of which uses a format 
            // string stored in the MessageResourceNames array above to produce a result.
            // By convention, we recapitulate that format string when we return the 
            // associated result, to document the specific failure or pass condition.

            if (!this.IsSecure(target))
            {
                // '{0}' is not secure for some reaons. 
                // To resolve this issue, pass /beEvenMoreSecure on both the compiler
                // and linker command lines. Binaries also require the 
                // /beSecure option in order to enable the enhanced setting.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BAXXXX_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' enables /beEvenMoreSecure on both the compiler and linker
            // command-lines, preventing a broad range of conditions that 
            // bad actors can use to engage in their malignant, unfortunately
            // often-profitable foolishness.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BAXXXX_Pass),
                        context.TargetUri.GetFileName()));
        }

        // Not considered a meaningful method name. Be sure to do a better job.
        private bool IsSecure(PEBinary _)
        {
            // Add relevant PE-level examination 
            return false;
        }
    }
}