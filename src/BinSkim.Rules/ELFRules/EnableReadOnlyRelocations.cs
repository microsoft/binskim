// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.BinaryParsers;
using ELFSharp.ELF;
using System.Linq;
using ELFSharp.ELF.Segments;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule))]
    public class EnableReadOnlyRelocations : ELFBinarySkimmerBase
    {
        private const uint GNU_RELRO_ID = 0x6474e552;
        
        /// <summary>
        /// TBDBA3020
        /// </summary>
        public override string Id { get { return RuleIds.EnableReadOnlyRelocations; } }

        /// <summary>
        /// This check ensures that some relocation data is marked as read only, and moved below 
        /// the .data section in memory. This prevents them from being overwritten, 
        /// which can redirect control flow. Use the compiler flags '-Wl,z,relro' to enable this. // todo
        /// </summary>
        public override string FullDescription
        {
            get { return RuleResources.TBDBA3020_EnableReadOnlyRelocations_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.TBDBA3020_Pass),
                    nameof(RuleResources.TBDBA3020_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public override AnalysisApplicability CanAnalyzeELF(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            IELF elf = target.ELF;

            if (elf.Type == FileType.Core || elf.Type == FileType.None || elf.Type == FileType.Relocatable)
            {
                reasonForNotAnalyzing = "ELF is not a shared object or executable";
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            IELF elf = context.ELFBinary().ELF;
            
            foreach (var seg in elf.Segments)
            {
                if (((uint)seg.Type) == GNU_RELRO_ID)
                {
                    // Pass
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                            nameof(RuleResources.TBDBA3020_Pass),
                            context.TargetUri.GetFileName()));
                    return;
                }
            }

            // Fail
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                    nameof(RuleResources.TBDBA3020_Error),
                    context.TargetUri.GetFileName()));
        }
    }
}
