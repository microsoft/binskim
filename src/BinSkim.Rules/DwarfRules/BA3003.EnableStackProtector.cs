// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableStackProtector : DwarfSkimmerBase
    {
        /// <summary>
        /// BA3003
        /// </summary>
        public override string Id => RuleIds.EnableStackProtector;

        /// <summary>
        /// The stack protector ensures that all functions that use buffers over a certain size will
        /// use a stack cookie(and check it) to prevent stack based buffer overflows, exiting if stack
        /// smashing is detected.Use '--fstack-protector-strong' (all buffers of 4 bytes or more) or
        /// '--fstack-protector-all' (all functions) to enable this.
        /// </summary>
        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA3003_EnableStackProtector_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA3003_Pass),
            nameof(RuleResources.BA3003_Error),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzeDwarf(IDwarfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            CanAnalyzeDwarfResult result = default;

            if (target is ElfBinary elf)
            {
                result = this.VerifyDwarfBinary(elf);
            }
            else if (target is MachOBinary mainMacho)
            {
                foreach (SingleMachOBinary subMachO in mainMacho.MachOs)
                {
                    result = this.VerifyDwarfBinary(subMachO);
                    if (result.Result == AnalysisApplicability.ApplicableToSpecifiedTarget)
                    {
                        // if any machO is applicable
                        break;
                    }
                }
            }

            reasonForNotAnalyzing = result.Reason;
            return result.Result;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            IDwarfBinary binary = context.DwarfBinary();

            static bool analyze(IDwarfBinary binary)
            {
                return binary.CommandLineInfos.All(i =>
                (
                    i.CommandLine.Contains("-fstack-protector-all", StringComparison.OrdinalIgnoreCase)
                    || i.CommandLine.Contains("-fstack-protector-strong", StringComparison.OrdinalIgnoreCase)
                )
                && !i.CommandLine.Contains("-fno-stack-protector", StringComparison.OrdinalIgnoreCase));
            }

            if (binary is ElfBinary elf)
            {
                if (!analyze(elf))
                {
                    // The stack protector was not found in '{0}'.
                    // This may be because the binary has no stack-based arrays,
                    // or because '--stack-protector-strong' was not used.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                            nameof(RuleResources.BA3003_Error),
                            context.TargetUri.GetFileName()));
                    return;
                }

                // Stack protector was found on '{0}'.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA3003_Pass),
                        context.TargetUri.GetFileName()));
                return;
            }

            if (binary is MachOBinary mainBinary)
            {
                foreach (SingleMachOBinary subBinary in mainBinary.MachOs)
                {
                    if (!analyze(subBinary))
                    {
                        // The stack protector was not found in '{0}'.
                        // This may be because the binary has no stack-based arrays,
                        // or because '--stack-protector-strong' was not used.
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                                nameof(RuleResources.BA3003_Error),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                }

                // Stack protector was found on '{0}'.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA3003_Pass),
                        context.TargetUri.GetFileName()));
            }
        }

        private CanAnalyzeDwarfResult VerifyDwarfBinary(IDwarfBinary binary)
        {
            // We check for "any usage of non-gcc" as a default/standard compilation with clang leads to [GCC, Clang]
            // either because it links with a gcc-compiled object (cstdlib) or the linker also reading as GCC.
            // This has a potential for a False Negative if teams are using GCC and other tools.
            if (binary.Compilers.Any(c => c.Compiler != ElfCompilerType.GCC))
            {
                return new CanAnalyzeDwarfResult
                {
                    Reason = MetadataConditions.ElfNotBuiltWithGcc,
                    Result = AnalysisApplicability.NotApplicableToSpecifiedTarget
                };
            }
            else if (binary.Compilers.Any(c => c.Version.Major < 8))
            {
                return new CanAnalyzeDwarfResult
                {
                    Reason = MetadataConditions.ElfNotBuiltWithGccV8OrLater,
                    Result = AnalysisApplicability.NotApplicableToSpecifiedTarget
                };
            }
            else if (binary.CommandLineInfos.Count == 0)
            {
                return new CanAnalyzeDwarfResult
                {
                    Reason = MetadataConditions.ElfNotBuiltWithDwarfDebugging,
                    Result = AnalysisApplicability.NotApplicableToSpecifiedTarget
                };
            }

            return new CanAnalyzeDwarfResult
            {
                Reason = null,
                Result = AnalysisApplicability.ApplicableToSpecifiedTarget
            };
        }
    }
}
