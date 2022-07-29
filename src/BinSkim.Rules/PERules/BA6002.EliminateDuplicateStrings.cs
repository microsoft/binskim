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
    public class EliminateDuplicateStrings : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA6002
        /// </summary>
        public override string Id => RuleIds.EliminateDuplicateStrings;

        /// <summary>
        /// The /GF compiler option, also known as Eliminate Duplicate Strings or String Pooling, will combine identical strings
        /// in a program to a single readonly copy. This can significantly reduce binary size for programs with many string resources.
        /// </summary>

        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA6002_EliminateDuplicateStrings_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA6002_Pass),
                    nameof(RuleResources.BA6002_Warning),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        private const string AnalyzerName = RuleIds.EliminateDuplicateStrings + "." + nameof(EliminateDuplicateStrings);

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

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;
            Pdb pdb = target.Pdb;

            if (pe.IncrementalLinkingEnabled(pdb))
            {
                // '{0}' was compiled without Eliminate Duplicate Strings (/GF) enabled, increasing binary size.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Fail, context, null,
                    nameof(RuleResources.BA6002_Warning),
                    context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' was compiled with Eliminate Duplicate Strings (/GF) enabled.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA6002_Pass),
                    context.TargetUri.GetFileName()));
        }
    }
}
