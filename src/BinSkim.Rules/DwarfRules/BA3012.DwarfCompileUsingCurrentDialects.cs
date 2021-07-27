// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public class DwarfCompileUsingCurrentDialects : DwarfSkimmerBase
    {
        /// <summary>
        /// BA3012
        /// </summary>
        public override string Id => RuleIds.DwarfCompileUsingCurrentDialects;

        private static readonly List<string> OldDialectsForC = new List<string>() { "C89", "C90", "C99", "C11" };

        private static readonly List<string> OldDialectsForCPlugPlus = new List<string>() { "C++98", "C++11", "C++14" };

        /// <summary>
        /// The '/std' setting enables supported C and C++ language features from the 
        /// specified version of the C or C++ language standard. Compile using current 
        /// dialects enables current standard-specific features and behavior.
        /// </summary>
        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA3012_DwarfCompileUsingCurrentDialects_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA3012_Pass),
            nameof(RuleResources.BA3012_Warning),
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
                string dwarfCompilerCommand = binary.GetDwarfCompilerCommand();

                List<string> args = ArgumentSplitter.CommandLineToArgvW(dwarfCompilerCommand);

                return args.Count >= 2 && (!OldDialectsForC.Contains(args[1]) && !OldDialectsForCPlugPlus.Contains(args[1]));
            }

            if (binary is ElfBinary elf)
            {
                if (!analyze(elf))
                {
                    // '{0}' was not compiled with current dialects. Compile using
                    // current dialects enables current standard-specific features
                    // and behavior. To resolve this problem, compiling with the
                    // compiler flags /std with version 17 or later, e.g. '/std:c++17'
                    // for C++ and '/std:c17' for C.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                            nameof(RuleResources.BA3012_Warning),
                            context.TargetUri.GetFileName()));
                    return;
                }

                // '{0}' was compiled with current dialects. Compile using current
                // dialects enables current standard-specific features and behavior.
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
                        // '{0}' was not compiled with current dialects. Compile using
                        // current dialects enables current standard-specific features
                        // and behavior. To resolve this problem, compiling with the
                        // compiler flags /std with version 17 or later, e.g. '/std:c++17'
                        // for C++ and '/std:c17' for C.
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                                nameof(RuleResources.BA3012_Warning),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                }

                // '{0}' was compiled with current dialects. Compile using current
                // dialects enables current standard-specific features and behavior.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA3012_Pass),
                        context.TargetUri.GetFileName()));
            }
        }

        private CanAnalyzeDwarfResult VerifyDwarfBinary(IDwarfBinary binary)
        {
            // We check for "any usage of non-gcc" as a default/standard compilation with
            // clang leads to [GCC, Clang] either because it links with a gcc-compiled
            // object (cstdlib) or the linker also reading as GCC. This has a potential
            // for a False Negative if teams are using GCC and other tools.
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
