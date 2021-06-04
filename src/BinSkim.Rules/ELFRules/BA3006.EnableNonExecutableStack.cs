// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;

using ELFSharp.ELF.Segments;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ELF;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableNonExecutableStack : ELFBinarySkimmerBase
    {
        /// <summary>
        /// BA3006
        /// </summary>
        public override string Id => RuleIds.EnableNonExecutableStack;

        /// <summary>
        /// This check ensures that non-executable stack is enabled. A common type of exploit is the stack buffer overflow. 
        /// An application receives, from an attacker, more data than it is prepared for and stores this information on its stack, 
        /// writing beyond the space reserved for it. This can be designed to cause execution of the data written on the stack. 
        /// One mechanism to mitigate this vulnerability is for the system to not allow the execution of instructions in sections 
        /// of memory identified as part of the stack. Use the compiler flags '-z noexecstack' to enable this.
        /// </summary>
        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA3006_EnableNonExecutableStack_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA3006_Pass),
            nameof(RuleResources.BA3006_Error),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzeElf(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;

            if (target.GetSegmentFlags(ELFSegmentType.PT_GNU_STACK) == null)
            {
                reasonForNotAnalyzing = MetadataConditions.ElfNotContainSegment;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            ELFBinary elfBinary = context.ELFBinary();

            if ((elfBinary.GetSegmentFlags(ELFSegmentType.PT_GNU_STACK) & SegmentFlags.Execute) != 0)
            {
                // The non-executable stack is not enabled for this binary,
                // so '{0}' can have a vulnerability of execution of the data written on the stack.
                // Ensure you are compiling with the flag '-z noexecstack' to address this.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA3006_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            // The enable non-executable stack flag was present, so '{0}' is protected.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA3006_Pass),
                    context.TargetUri.GetFileName()));
        }
    }
}
