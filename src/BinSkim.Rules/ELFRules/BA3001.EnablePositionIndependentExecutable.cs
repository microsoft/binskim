// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Segments;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnablePositionIndependentExecutable : ELFBinarySkimmerBase
    {
        /// <summary>
        /// BA3001
        /// </summary>
        public override string Id => RuleIds.EnablePositionIndependentExecutable;

        /// <summary>
        /// "A Position Independent Executable (PIE) relocates all of its sections at load time, including the code section,
        ///  if ASLR is enabled in the Linux kernel (instead of just the stack/heap).  This makes ROP-style attacks more difficult.
        ///  This can be enabled by passing '-f pie' to clang/gcc."
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3001_EnablePositionIndependentExecutable_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA3001_Pass_Executable),
                    nameof(RuleResources.BA3001_Pass_Library),
                    nameof(RuleResources.BA3001_Error),
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
            if (elf.Type == FileType.Executable)
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA3001_Error),
                        context.TargetUri.GetFileName()));
                return;
            }
            else if (elf.Type == FileType.SharedObject)
            {
                // Check that it is an executable SO instead of a normal shared library
                // Looking for a program header segment seems to work well here.
                if (elf.Segments.Any(seg => seg.Type == SegmentType.ProgramHeader))
                {
                    // PIE enabled on executable '{0}'.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                            nameof(RuleResources.BA3001_Pass_Executable),
                            context.TargetUri.GetFileName()));
                    return;
                }
                else
                {
                    // '{0}' is a shared object library rather than an executable,
                    // and is automatically position independent.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                            nameof(RuleResources.BA3001_Pass_Library),
                            context.TargetUri.GetFileName()));
                    return;
                }
            }
        }
    }
}
