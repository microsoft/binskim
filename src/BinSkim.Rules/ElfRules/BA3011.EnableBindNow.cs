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
    public class EnableBindNow : ElfBinarySkimmer
    {
        private const uint DF_1_NOW = 0x1;
        private const uint DF_BIND_NOW = 0x8;

        /// <summary>
        /// BA3011
        /// </summary>
        public override string Id => RuleIds.EnableBindNow;

        /// <summary>
        /// This check ensures that some relocation data is marked as read only after
        /// the executable is loaded, and moved below the .data section in memory.
        /// This prevents them from being overwritten, which can redirect control flow.
        /// Use the compiler flags '-Wl,z,now' to enable this.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3011_EnableBindNow_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA3011_Pass),
            nameof(RuleResources.BA3011_Error),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzeElf(ElfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
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
            IELF elf = context.ElfBinary().ELF;

            if (HasBindNowFlag(elf))
            {
                // Pass
                context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA3011_Pass),
                    context.TargetUri.GetFileName()));
            }
            else
            {
                // Fail
                context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                    nameof(RuleResources.BA3011_Error),
                    context.TargetUri.GetFileName()));
            }
        }

        // Pwntools checksec calls out numerous ways an ELF can specify full RELRO to the
        // loader. All have the intended effect of eagerly resolving all plt functions and
        // subsequently marking the entire GOT read-only (at least, when there
        // is also a GNU_RELRO section)
        // https://github.com/Gallopsled/pwntools/blob/ec9dfe108b85ac47983e9a98808fcdbb50cb0cdd/pwnlib/elf/elf.py#L1578
        private static bool HasBindNowFlag(IELF elf)
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
    }
}
