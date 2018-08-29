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
        /// BA3010
        /// </summary>
        public override string Id { get { return RuleIds.EnableReadOnlyRelocations; } }

        /// <summary>
        /// This check ensures that some relocation data is marked as read only after 
        /// the executable is loaded, and moved below the .data section in memory. 
        /// This prevents them from being overwritten, which can redirect control flow. 
        /// Use the compiler flags '-Wl,z,relro' to enable this.
        /// </summary>
        public override Message FullDescription
        {
            get { return new Message { Text = RuleResources.BA3010_EnableReadOnlyRelocations_Description }; }
        }

        protected override IEnumerable<string> MessageResourceNames
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA3010_Pass),
                    nameof(RuleResources.BA3010_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public override AnalysisApplicability CanAnalyzeELF(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            IELF elf = target.ELF;

            if (elf.Type == FileType.Core || elf.Type == FileType.None || elf.Type == FileType.Relocatable)
            {
                reasonForNotAnalyzing = MetadataConditions.ELFIsCoreNoneOrObject;
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
                            nameof(RuleResources.BA3010_Pass),
                            context.TargetUri.GetFileName()));
                    return;
                }
            }

            // Fail
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                    nameof(RuleResources.BA3010_Error),
                    context.TargetUri.GetFileName()));
        }
    }
}
