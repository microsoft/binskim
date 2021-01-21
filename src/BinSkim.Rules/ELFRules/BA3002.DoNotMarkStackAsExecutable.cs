// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;

using ELFSharp.ELF;
using ELFSharp.ELF.Segments;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class DoNotMarkStackAsExecutable : ELFBinarySkimmerBase
    {
        private const uint GNU_STACK_ID = 0x6474e551;

        /// <summary>
        /// "BA3002"
        /// </summary>
        public override string Id => RuleIds.DoNotMarkStackAsExecutable;

        /// <summary>
        /// "This checks if a binary has an executable stack; an
        /// executable stack allows attackers to redirect code flow
        /// into stack memory, which is an easy place for an attacker
        /// to store shellcode. Ensure you are compiling with '-z noexecstack'
        /// to mark the stack as non-executable."
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3002_DoNotMarkStackAsExecutable_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA3002_Pass),
                    nameof(RuleResources.BA3002_Error_StackExec),
                    nameof(RuleResources.BA3002_Error_NoStackSeg),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzeElf(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            IELF elf = target.ELF;

            if (elf.Type == FileType.Core || elf.Type == FileType.None || elf.Type == FileType.Relocatable)
            {
                reasonForNotAnalyzing = reasonForNotAnalyzing = MetadataConditions.ElfIsCoreNoneOrObject;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            IELF elf = context.ELFBinary().ELF;
            // Look for the GNU_STACK segment
            foreach (ISegment seg in elf.Segments)
            {
                if (((uint)seg.Type) == GNU_STACK_ID)
                {
                    // if we find it, we'll check if it's NX...
                    if ((seg.Flags & SegmentFlags.Execute) != 0)
                    {
                        // Fail -- stack seg is marked executable
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                                nameof(RuleResources.BA3002_Error_StackExec),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                    else
                    {
                        // Pass -- stack segment isn't executable
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                                nameof(RuleResources.BA3002_Pass),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                }
            }

            // If the GNU_STACK isn't present, the stack is probably loaded as executable
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                    nameof(RuleResources.BA3002_Error_NoStackSeg),
                    context.TargetUri.GetFileName()));
        }
    }
}
