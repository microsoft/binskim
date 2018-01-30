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
    public class DoNotMarkStackAsExecutable : ELFBinarySkimmerBase
    {
        private const uint GNU_STACK_ID = 0x6474e551;

        /// <summary>
        /// "BA3002"
        /// </summary>
        public override string Id { get { return RuleIds.DoNotMarkStackAsExecutable; } }

        /// <summary>
        /// "This checks if a binary has an executable stack; an 
        /// executable stack allows attackers to redirect code flow 
        /// into stack memory, which is an easy place for an attacker 
        /// to store shellcode. Ensure you are compiling with '-z noexecstack'
        /// to mark the stack as non-executable."
        /// </summary>
        public override string FullDescription
        {
            get { return RuleResources.BA3002_DoNotMarkStackAsExecutable_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA3002_Pass),
                    nameof(RuleResources.BA3002_Error_StackExec),
                    nameof(RuleResources.BA3002_Error_NoStackSeg),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public override AnalysisApplicability CanAnalyzeELF(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            IELF elf = target.ELF;

            if (elf.Type == FileType.Core || elf.Type == FileType.None || elf.Type == FileType.Relocatable)
            {
                reasonForNotAnalyzing = reasonForNotAnalyzing = MetadataConditions.ELFIsCoreNoneOrObject;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            IELF elf = context.ELFBinary().ELF;
            // Look for the GNU_STACK segment
            foreach (var seg in elf.Segments)
            {
                if (((uint)seg.Type) == GNU_STACK_ID)
                {
                    // if we find it, we'll check if it's NX...
                    if ((seg.Flags & SegmentFlags.Execute) != 0)
                    {
                        //Fail execstack
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                                nameof(RuleResources.BA3002_Error_StackExec),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                    else
                    {
                        // Pass
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                                nameof(RuleResources.BA3002_Pass),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                }
            }

            // If the GNU_STACK isn't present, the stack is probably loaded as executable
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                    nameof(RuleResources.BA3002_Error_NoStackSeg),
                    context.TargetUri.GetFileName()));
        }
    }
}
