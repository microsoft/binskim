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
    public class DisableIncrementalLinking : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA6001
        /// </summary>
        public override string Id => RuleIds.DisableIncrementalLinking;

        /// <summary>
        /// Incremental linking support increases binary size and can reduce runtime performance. Fully optimized 
        /// release builds should not specify incremental linking.
        /// </summary>

        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA6001_DisableIncrementalLinking_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA6001_Pass),
                    nameof(RuleResources.BA6001_Warning),
                    nameof(RuleResources.BA6001_Pass_NonReleaseBuild)
                };

        private const string AnalyzerName = RuleIds.DisableIncrementalLinking + "." + nameof(DisableIncrementalLinking);

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

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
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
