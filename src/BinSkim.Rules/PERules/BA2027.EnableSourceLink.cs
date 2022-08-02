// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableSourceLink : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA2027
        /// </summary>
        public override string Id => RuleIds.EnableSourceLink;

        /// <summary>
        /// Enable SourceLink.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2027_EnableSourceLink_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[]
        {
            nameof(RuleResources.BA2027_Pass),
            nameof(RuleResources.BA2027_Warning)
        };

        public override void Initialize(BinaryAnalyzerContext context)
        {
            base.Initialize(context);
        }

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            // Source Link is supported on the C# compiler and MSVC only.
            if (!target.PE.IsManaged && target.Pdb != null && !target.PE.IsTargetCompiledWithMSVC(target.Pdb))
            {
                // Wrong language
                reasonForNotAnalyzing = MetadataConditions.ImageIsNotBuiltWithMSVC;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            if (target.PE.IsResourceOnly)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            if (target.PE.IsManagedResourceOnly)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyAssembly;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            if (HasSourceLink(context))
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2027_Pass),
                    context.TargetUri.GetFileName()));
            }
            else
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                    nameof(RuleResources.BA2027_Warning),
                    context.TargetUri.GetFileName()));
            }
        }

        private static bool HasSourceLink(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            // We're just checking for the presence of SourceLink document(s),
            // not whether they can be read.
            if (pdb.FileType == PdbFileType.Portable)
            {
                string sourceLinkDocument = target.PE.ManagedPdbGetSourceLinkDocument(pdb);
                return !string.IsNullOrEmpty(sourceLinkDocument);
            }
            else
            {
                IEnumerable<string> sourceLinkDocuments = pdb.WindowsPdbGetSourceLinkDocuments();
                return sourceLinkDocuments != null && sourceLinkDocuments.Any();
            }
        }
    }
}
