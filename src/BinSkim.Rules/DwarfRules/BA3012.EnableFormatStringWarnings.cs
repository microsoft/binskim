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
    public class EnableFormatStringWarnings : DwarfSkimmerBase
    {
        /// <summary>
        /// BA3012
        /// </summary>
        public override string Id => RuleIds.EnableFormatStringWarnings;

        /// <summary>
        /// Binaries should be compiled with warning flags that enables format string security-relevant checks. 
        /// To resolve this issue, compile with flag -Wformat -Wformat-security -Werror=format-security or higher level.
        /// </summary>
        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA3012_EnableFormatStringWarnings_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA3012_Pass),
            nameof(RuleResources.BA3012_Warning),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzeDwarf(IDwarfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            CanAnalyzeDwarfResult result = default;

            if (target is ELFBinary elf)
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
                bool isPassed = false;
                string dwarfCompilerCommand = binary.GetDwarfCompilerCommand();
                var paras = dwarfCompilerCommand.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Replace(" ", "")).ToList();

                // check from most specific to more generic. first check for "-Werror=format-security"
                if (paras.Contains("-Werror=format-security") || (paras.Contains("-Werror") && !paras.Contains("-Wno-error=format-security")))
                {
                    // even with all warning as error enabled, each can be disabled separately.
                    // GCC doc: for example -Wno-error=switch makes -Wswitch warnings not be errors, even when -Werror is in effect.
                    // if enable warning as error for all or the one we are looking for, continue with next check for "-Wformat-security".
                    if (paras.Contains("-Wformat-security") || paras.Contains("-Werror=format-security") || paras.Contains("-Wformat=2"))
                    {
                        // GCC doc: Note that specifying -Werror=foo automatically implies -Wfoo. However, -Wno-error=foo does not imply anything.
                        // so -Werror=format-security also enables -Wformat-security
                        // GCC doc: -Wformat=2 Currently equivalent to -Wformat -Wformat-nonliteral -Wformat-security -Wformat-y2k
                        // so -Wformat=2 also enables -Wformat-security
                        // continue with next check for "-Wformat".
                        if (paras.Contains("-Wall") || paras.Contains("-Wformat") || paras.Contains("-Wformat=1") || paras.Contains("-Wformat=2"))
                        {
                            // GCC doc: -Wformat is enabled by -Wall.
                            // GCC doc: -Wformat is equivalent to -Wformat=1
                            // GCC doc: -Wno-format is equivalent to -Wformat=0
                            isPassed = true;
                        }
                    }
                }

                return isPassed;
            }

            if (binary is ELFBinary elf)
            {
                if (!analyze(elf))
                {
                    // '{0}' does not enable the recommended format string security-relevant checks.
                    // To Enable the recommended format string security-relevant checks,
                    // compile with flag -Wformat -Wformat-security -Werror=format-security or higher level.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                            nameof(RuleResources.BA3012_Warning),
                            context.TargetUri.GetFileName()));
                    return;
                }

                // '{0}' enables the recommended format string security-relevant checks.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA3012_Pass),
                        context.TargetUri.GetFileName()));
                return;
            }

            if (binary is MachOBinary mainBinary)
            {
                foreach (SingleMachOBinary subBinary in mainBinary.MachOs)
                {
                    if (!analyze(subBinary))
                    {
                        // '{0}' does not enable the recommended format string security-relevant checks.
                        // To Enable the recommended format string security-relevant checks,
                        // compile with flag -Wformat -Wformat-security -Werror=format-security or higher level.
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                                nameof(RuleResources.BA3012_Warning),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                }

                // '{0}' enables the recommended format string security-relevant checks.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA3012_Pass),
                        context.TargetUri.GetFileName()));
            }
        }

        private CanAnalyzeDwarfResult VerifyDwarfBinary(IDwarfBinary binary)
        {
            // We check for "any usage of non-gcc" as a default/standard compilation with clang leads to [GCC, Clang]
            // either because it links with a gcc-compiled object (cstdlib) or the linker also reading as GCC.
            // This has a potential for a False Negative if teams are using GCC and other tools.
            if (binary.Compilers.Any(c => c.Compiler != ELFCompilerType.GCC))
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
            else
            {
                string dwarfCompilerCommand = binary.GetDwarfCompilerCommand();

                if (string.IsNullOrWhiteSpace(dwarfCompilerCommand))
                {
                    return new CanAnalyzeDwarfResult
                    {
                        Reason = MetadataConditions.ElfNotBuiltWithDwarfDebugging,
                        Result = AnalysisApplicability.NotApplicableToSpecifiedTarget
                    };
                }
            }

            return new CanAnalyzeDwarfResult
            {
                Reason = null,
                Result = AnalysisApplicability.ApplicableToSpecifiedTarget
            };
        }
    }
}
