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
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
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
    public class DisableIncrementalLinking : PEBinarySkimmerBase, IOptionsProvider /* Delete this if no special configuration required */
    {
        /// <summary>
        /// BA6001
        /// </summary>
        public override string Id => RuleIds.DisableIncrementalLinking;

        /// <summary>
        /// Recapitulate the full text of the rule description returned below
        /// here as a summary comment.
        /// </summary>

        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA6001_DisableIncrementalLinking_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA6001_Pass),
                    nameof(RuleResources.BA6001_Warning),
                    nameof(RuleResources.BA6001_Pass_NonReleaseBuild)
                };

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                MinimumRequiredLinkerVersion
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.DisableIncrementalLinking + "." + nameof(DisableIncrementalLinking);

        public static PerLanguageOption<Version> MinimumRequiredLinkerVersion { get; } =
            new PerLanguageOption<Version>(
                AnalyzerName, nameof(MinimumRequiredLinkerVersion), defaultValue: () => new Version("14.0"));

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            //reasonForNotAnalyzing = MetadataConditions.ImageIsUnoptimized;
            //if (portableExecutable.IsMostlyOptimized(target.Pdb)) { return result; }

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
            Pdb pdb = target.Pdb;

            if (!pe.IsMostlyOptimized(pdb))
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.NotApplicable, context, null,
                    nameof(RuleResources.BA6001_Pass_NonReleaseBuild),
                    context.TargetUri.GetFileName()));
                return;
            }

            if (pe.IncrementalLinkingEnabled(pdb))
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Fail, context, null,
                    nameof(RuleResources.BA6001_Warning),
                    context.TargetUri.GetFileName()));
                return;
            }

            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA6001_Pass),
                    context.TargetUri.GetFileName()));
        }
    }
}
