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
    [Export(typeof(ReportingDescriptor))]
    [Export(typeof(Skimmer<BinaryAnalyzerContext>))]
    public class EnableOptimizeReferences : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA6005
        /// </summary>
        public override string Id => RuleIds.EnableOptimizeReferences;

        /// <summary>
        /// Optimize References can significantly reduce binary size because it instructs the linker to remove
        /// unreferenced functions and data from the final binary.
        /// </summary>

        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA6005_EnableOptimizeReferences_Description };

        public override bool EnabledByDefault => false;


        protected override ICollection<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA6005_Pass),
                    nameof(RuleResources.BA6005_Warning),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        private const string AnalyzerName = RuleIds.EnableOptimizeReferences + "." + nameof(EnableOptimizeReferences);

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.CouldNotLoadPdb;
            if (target.Pdb == null) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsNotBuiltWithMsvc;
            if (!portableExecutable.IsTargetCompiledWithMsvc(target.Pdb)) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;
            Pdb pdb = target.Pdb;

            if (!pe.IsOptimizeReferencesEnabled(pdb))
            {
                // '{0}' was compiled with Optimize References (/OPT:REF) disabled, increasing binary size.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                    nameof(RuleResources.BA6005_Warning),
                    context.CurrentTarget.Uri.GetFileName()));
                return;
            }

            // '{0}' was compiled with Optimize References (/OPT:REF) enabled
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA6005_Pass),
                    context.CurrentTarget.Uri.GetFileName()));
        }
    }
}
