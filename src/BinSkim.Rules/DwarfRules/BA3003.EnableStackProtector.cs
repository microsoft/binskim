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

using static Microsoft.CodeAnalysis.BinaryParsers.CommandLineHelper;

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
            List<DwarfCompileCommandLineInfo> failedList;

            static bool analyze(IDwarfBinary binary, out List<DwarfCompileCommandLineInfo> failedList)
            {
                failedList = new List<DwarfCompileCommandLineInfo>();

                foreach (DwarfCompileCommandLineInfo info in binary.CommandLineInfos)
                {
                    if (!info.ParametersIncluded)
                    {
                        continue;
                    }

                    bool failed = false;
                    if ((!info.CommandLine.Contains("-fstack-protector-all", StringComparison.OrdinalIgnoreCase)
                        && !info.CommandLine.Contains("-fstack-protector-strong", StringComparison.OrdinalIgnoreCase))
                        || info.CommandLine.Contains("-fno-stack-protector", StringComparison.OrdinalIgnoreCase))
                    {
                        failed = true;
                    }
                    else
                    {
                        string[] paramToCheck = { "--param=ssp-buffer-size=" };
                        string paramValue = string.Empty;
                        bool found = GetOptionValue(info.CommandLine, paramToCheck, OrderOfPrecedence.FirstWins, ref paramValue);

                        if (found && !string.IsNullOrWhiteSpace(paramValue))
                        {
                            if (int.TryParse(paramValue, out int bufferSize))
                            {
                                if (bufferSize > 4)
                                {
                                    failed = true;
                                }
                            }
                        }
                    }

                    if (failed)
                    {
                        failedList.Add(info);
                    }
                }

                return !failedList.Any();
            }

            if (binary is ElfBinary elf)
            {
                if (!analyze(elf, out failedList))
                {
                    // The stack protector was not found in '{0}'.
                    // This may be because '--stack-protector-strong' was not used,
                    // or because it was explicitly disabled by '-fno-stack-protectors'.
                    // Modules did not meet the criteria: {1}
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                            nameof(RuleResources.BA3003_Error),
                            context.TargetUri.GetFileName(),
                            DwarfUtility.GetDistinctNames(failedList, context.TargetUri.GetFileName())));
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
                    if (!analyze(subBinary, out failedList))
                    {
                        // The stack protector was not found in '{0}'.
                        // This may be because '--stack-protector-strong' was not used,
                        // or because it was explicitly disabled by '-fno-stack-protectors'.
                        // Modules did not meet the criteria: {1}
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                                nameof(RuleResources.BA3003_Error),
                                context.TargetUri.GetFileName(),
                                DwarfUtility.GetDistinctNames(failedList, context.TargetUri.GetFileName())));
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
            if (binary.Compilers.Any(c => c.Compiler == ElfCompilerType.GCC && c.Version.Major < 8))
            {
                return new CanAnalyzeDwarfResult
                {
                    Reason = MetadataConditions.ElfNotBuiltWithGccV8OrLater,
                    Result = AnalysisApplicability.NotApplicableToSpecifiedTarget
                };
            }
            else if (!binary.CommandLineInfos.Any())
            {
                return new CanAnalyzeDwarfResult
                {
                    Reason = MetadataConditions.ElfNotBuiltWithDwarfDebugging,
                    Result = AnalysisApplicability.NotApplicableToSpecifiedTarget
                };
            }
            else if (!binary.CommandLineInfos.Any(info => info.ParametersIncluded))
            {
                return new CanAnalyzeDwarfResult
                {
                    Reason = MetadataConditions.ImageBuiltWithoutRecordCommandLine,
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
