// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;

using ELFSharp.ELF;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableReadOnlyRelocations : ELFBinarySkimmerBase
    {
        private const uint GNU_RELRO_ID = 0x6474e552;

        /// <summary>
        /// BA3010
        /// </summary>
        public override string Id => RuleIds.EnableReadOnlyRelocations;

        /// <summary>
        /// This check ensures that some relocation data is marked as read only after
        /// the executable is loaded, and moved below the .data section in memory.
        /// This prevents them from being overwritten, which can redirect control flow.
        /// Use the compiler flags '-Wl,z,relro' to enable this.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3010_EnableReadOnlyRelocations_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA3010_Pass),
                    nameof(RuleResources.BA3010_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzeElf(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            IELF elf = target.ELF;

            if (elf.Type == FileType.Core || elf.Type == FileType.None || elf.Type == FileType.Relocatable)
            {
                reasonForNotAnalyzing = MetadataConditions.ElfIsCoreNoneOrObject;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            IELF elf = context.ELFBinary().ELF;

            foreach (ELFSharp.ELF.Segments.ISegment seg in elf.Segments)
            {
                if (((uint)seg.Type) == GNU_RELRO_ID)
                {
                    // Pass
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                            nameof(RuleResources.BA3010_Pass),
                            context.TargetUri.GetFileName()));
                    return;
                }
            }

            // Fail
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                    nameof(RuleResources.BA3010_Error),
                    context.TargetUri.GetFileName()));
        }
    }
}
