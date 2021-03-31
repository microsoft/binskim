// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
        private const uint DF_BIND_NOW = 0x8;
        private const uint DF_1_NOW = 0x1;

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
            nameof(RuleResources.BA3010_Error_No_BindNow),
            nameof(RuleResources.BA3010_Error_No_Relro_Segment),
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

        // Pwntools checksec calls out numerous ways an ELF can specify full RELRO to the
        // loader. All have the intended effect of eagerly resolving all plt functions and
        // subsequently marking the entire GOT read-only
        // https://github.com/Gallopsled/pwntools/blob/ec9dfe108b85ac47983e9a98808fcdbb50cb0cdd/pwnlib/elf/elf.py#L1578
        private bool HasBindNowFlag(IELF elf)
        {
            try
            {
                var dynamicSection = (ELFSharp.ELF.Sections.IDynamicSection)elf.GetSection(".dynamic");
                if (dynamicSection == null)
                {
                    return false;
                }

                foreach (ELFSharp.ELF.Sections.DynamicEntry<ulong> entry in dynamicSection.Entries)
                {
                    if ((entry.Tag == ELFSharp.ELF.Sections.DynamicTag.BindNow) ||
                        (entry.Tag == ELFSharp.ELF.Sections.DynamicTag.Flags && (entry.Value & DF_BIND_NOW) != 0) ||
                        (entry.Tag == ELFSharp.ELF.Sections.DynamicTag.Flags1 && (entry.Value & DF_1_NOW) != 0))
                    {
                        return true;
                    }
                }
            }
            catch (InvalidCastException)
            {
                return false;
            }

            return false;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            IELF elf = context.ELFBinary().ELF;

            foreach (ELFSharp.ELF.Segments.ISegment seg in elf.Segments)
            {
                if (((uint)seg.Type) == GNU_RELRO_ID)
                {
                    if (HasBindNowFlag(elf))
                    {
                        // Pass
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                                nameof(RuleResources.BA3010_Pass),
                                context.TargetUri.GetFileName()));
                    }
                    else
                    {
                        // Fail
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                                nameof(RuleResources.BA3010_Error_No_BindNow),
                                context.TargetUri.GetFileName()));
                    }
                    return;
                }
            }

            // Fail
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                    nameof(RuleResources.BA3010_Error_No_Relro_Segment),
                    context.TargetUri.GetFileName()));
        }
    }
}
