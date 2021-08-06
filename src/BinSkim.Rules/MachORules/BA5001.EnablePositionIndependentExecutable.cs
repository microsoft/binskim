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
    public class EnablePositionIndependentExecutableMachO : MachOBinarySkimmer
    {
        /// <summary>
        /// BA5001
        /// </summary>
        public override string Id => RuleIds.EnablePositionIndependentExecutableMachO;

        /// <summary>
        /// A Position Independent Executable (PIE) relocates all of its sections at load time, including the code section,
        /// if ASLR is enabled in the Linux kernel (instead of just the stack/heap). This makes ROP-style attacks more difficult.
        /// This can be enabled by passing '-f pie' to clang/gcc.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA5001_EnablePositionIndependentExecutable_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA5001_Pass),
                    nameof(RuleResources.BA5001_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyze(MachOBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            if (target is MachOBinary mainMachO)
            {
                foreach (SingleMachOBinary singleMachO in mainMachO.MachOs)
                {
                    if (IsApplicableMachO(singleMachO))
                    {
                        // if any macho is applicable
                        return AnalysisApplicability.ApplicableToSpecifiedTarget;
                    }
                }

                reasonForNotAnalyzing = MetadataConditions.MachOIsNotExecutableDynamicLibraryOrObject;
            }

            return AnalysisApplicability.NotApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            IDwarfBinary binary = context.DwarfBinary();
            if (binary is MachOBinary mainBinary)
            {
                foreach (SingleMachOBinary subBinary in mainBinary.MachOs)
                {
                    if (IsApplicableMachO(subBinary) && !HasPieFlag(subBinary))
                    {
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                                nameof(RuleResources.BA5001_Error),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                }

                // PIE enabled on executable '{0}'.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA5001_Pass),
                        context.TargetUri.GetFileName()));
            }
        }

        private static bool IsApplicableMachO(SingleMachOBinary macho)
        {
            // if binary is an executable, or its libary with dwarf so can check its compiler options
            return macho.MachO.FileType == ELFSharp.MachO.FileType.Executable ||
                   ((macho.MachO.FileType == ELFSharp.MachO.FileType.DynamicLibrary ||
                     macho.MachO.FileType == ELFSharp.MachO.FileType.Object ||
                     macho.MachO.FileType == ELFSharp.MachO.FileType.Debug) &&
                     IsValidDwarfBinary(macho));

        }

        private static bool HasPieFlag(SingleMachOBinary binary)
        {
            if (binary.MachO.FileType == ELFSharp.MachO.FileType.Executable)
            {
                // for executables, check if file's header flag has PIE flag
                // https://developer.apple.com/library/archive/qa/qa1788/_index.html
                return binary.MachO.Flags.HasFlag(ELFSharp.MachO.HeaderFlags.PIE);
            }

            if (binary.MachO.FileType == ELFSharp.MachO.FileType.DynamicLibrary ||
                binary.MachO.FileType == ELFSharp.MachO.FileType.Object ||
                binary.MachO.FileType == ELFSharp.MachO.FileType.Debug)
            {
                // for libraries, check if compiler includes option "mdynamic-no-pic"
                return !binary.GetDwarfCompilerCommand().Contains("mdynamic-no-pic", System.StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsValidDwarfBinary(IDwarfBinary binary)
        {
            return binary.Compilers.Any(c => c.Compiler == ElfCompilerType.GCC) &&
                   !string.IsNullOrWhiteSpace(binary.GetDwarfCompilerCommand());
        }
    }
}
