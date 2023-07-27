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
            Pdb pdb = target.Pdb;

            var compilandsBinaryWithoutStringPooling = new List<ObjectModuleDetails>();
            var compilandsLibraryWithoutStringPooling = new List<ObjectModuleDetails>();

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                if (omDetails.Language != Language.C &&
                    omDetails.Language != Language.Cxx &&
                    omDetails.Language != Language.MASM)
                {
                    continue;
                }

                if (!omDetails.HasDebugInfo)
                {
                    continue;
                }

                bool isMsvc = (omDetails.WellKnownCompiler == WellKnownCompilers.MicrosoftC ||
                               omDetails.WellKnownCompiler == WellKnownCompilers.MicrosoftCxx);
                if (isMsvc)
                {
                    if (!omDetails.EliminateDuplicateStringsEnabled)
                    {
                        CompilandRecord record = om.CreateCompilandRecord();
                        if (!string.IsNullOrEmpty(record.Library))
                        {
                            compilandsLibraryWithoutStringPooling.Add(omDetails);
                        }
                        else
                        {
                            compilandsBinaryWithoutStringPooling.Add(omDetails);
                        }
                    }
                }
            }

            if (compilandsLibraryWithoutStringPooling.Count > 0 || compilandsBinaryWithoutStringPooling.Count > 0)
            {
                if (compilandsLibraryWithoutStringPooling.Count > 0)
                {
                    GenerateCompilandsAndLog(context, compilandsLibraryWithoutStringPooling);
                }

                if (compilandsBinaryWithoutStringPooling.Count > 0)
                {
                    GenerateCompilandsAndLog(context, compilandsBinaryWithoutStringPooling);
                }

                return;
            }

            //// '{0}' was compiled with Eliminate Duplicate Strings (/GF) enabled.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA6002_Pass),
                    context.CurrentTarget.Uri.GetFileName()));
        }

        private void GenerateCompilandsAndLog(BinaryAnalyzerContext context, List<ObjectModuleDetails> compilandsWithOneOrMoreInsecureFileHashes)
        {
            string compilands = compilandsWithOneOrMoreInsecureFileHashes.CreateOutputCoalescedByCompiler();

            // '{0}' was compiled without Eliminate Duplicate Strings (/GF) enabled, increasing binary size.
            // The following modules do not specify that policy: {1}
            context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Warning,
                                              context,
                                              null,
                                              nameof(RuleResources.BA6002_Warning),
                                              context.CurrentTarget.Uri.GetFileName(),
                                              compilands));
        }
    }
}
