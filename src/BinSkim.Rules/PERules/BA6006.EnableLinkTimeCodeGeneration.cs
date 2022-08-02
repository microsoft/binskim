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
    public class EnableLinkTimeCodeGeneration : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA6006
        /// </summary>
        public override string Id => RuleIds.EnableLinkTimeCodeGeneration;

        /// <summary>
        /// Enabling Link Time Code Generation (LTCG) performs whole-program optimization, which is able to better optimize code across
        /// translation units. LTCG is also a prerequisite for Profile-Guided Optimization (PGO) which can further improve performance.
        /// </summary>

        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA6006_EnableLinkTimeCodeGeneration_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA6006_Pass),
                    nameof(RuleResources.BA6006_Warning),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        private const string AnalyzerName = RuleIds.EnableLinkTimeCodeGeneration + "." + nameof(EnableLinkTimeCodeGeneration);

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.CouldNotLoadPdb;
            if (target.Pdb == null) { return result; }

            Pdb pdb = target.Pdb;
            reasonForNotAnalyzing = MetadataConditions.NotAReleaseBuild;
            if (!portableExecutable.IsMostlyOptimized(pdb)) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsNotBuiltWithMSVC;
            if (!portableExecutable.IsTargetCompiledWithMSVC(target.Pdb)) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;
            Pdb pdb = target.Pdb;

            if (!pe.IsLinkTimeCodeGenerationEnabled(pdb))
            {
                // '{0}' was compiled with without Link Time Code Generation (/LTCG). Enabling LTCG can improve optimizations and performance.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Fail, context, null,
                    nameof(RuleResources.BA6006_Warning),
                    context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' was compiled with LinkTimeCodeGeneration (/LTCG) enabled.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA6006_Pass),
                    context.TargetUri.GetFileName()));
        }
    }
}
