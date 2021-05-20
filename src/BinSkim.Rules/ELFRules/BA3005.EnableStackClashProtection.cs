// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableStackClashProtection : ELFBinarySkimmerBase
    {
        /// <summary>
        /// BA3005
        /// </summary>
        public override string Id => RuleIds.EnableStackClashProtection;

        /// <summary>
        /// This check ensures that stack clash protection is enabled. 
        /// Each program running on a computer uses a special memory region called the stack. 
        /// This memory region is special because it grows automatically when the program needs more stack memory. 
        /// But if it grows too much and gets too close to another memory region, the program may confuse the stack with the other memory region. 
        /// An attacker can exploit this confusion to overwrite the stack with the other memory region, or the other way around. 
        /// Use the compiler flags '-fstack-clash-protection' to enable this.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3005_EnableStackClashProtection_Description };

        protected string dwarfCompilerCommand;

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA3005_Pass),
            nameof(RuleResources.BA3005_Error),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzeElf(ELFBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;

            // We check for "any usage of non-gcc" as a default/standard compilation with clang leads to [GCC, Clang]
            // either because it links with a gcc-compiled object (cstdlib) or the linker also reading as GCC.
            // This has a potential for a False Negative if teams are using GCC and other tools.
            if (target.Compilers.Any(c => c.Compiler != ELFCompilerType.GCC))
            {
                reasonForNotAnalyzing = MetadataConditions.ElfNotBuiltWithGcc;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
            else if (target.Compilers.Any(c => c.Version.Major < 8))
            {
                reasonForNotAnalyzing = MetadataConditions.ElfNotBuiltWithGccV8OrLater;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }
            else
            {
                dwarfCompilerCommand = target.GetDwarfCompilerCommand();

                if (string.IsNullOrWhiteSpace(dwarfCompilerCommand))
                {
                    reasonForNotAnalyzing = MetadataConditions.ElfNotBuiltWithDwarfDebugging;
                    return AnalysisApplicability.NotApplicableToSpecifiedTarget;
                }
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            if (!dwarfCompilerCommand.ToLower().Contains("-fstack-clash-protection")
                || dwarfCompilerCommand.ToLower().Contains("-fno-stack-clash-protection"))
            {
                // The Stack Clash Protection is missing from this binary,
                // so the stack from '{0}' can clash/colide with another memory region.
                // Ensure you are compiling with the compiler flags '-fstack-clash-protection' to address this.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA3005_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            // The Stack Clash Protection was present, so '{0}' is protected.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA3005_Pass),
                    context.TargetUri.GetFileName()));
        }
    }
}
