// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    /// <summary>helper code used for command line analyze.</summary>
    internal static class CommandLineUtilities
    {
        internal static AnalysisApplicability CanAnalyzeDwarf(IDwarfBinary target, out string reasonForNotAnalyzing)
        {
            CanAnalyzeDwarfResult result = default;

            if (target is ElfBinary elf)
            {
                result = VerifyDwarfBinary(elf);
            }
            else if (target is MachOBinary mainMacho)
            {
                foreach (SingleMachOBinary subMachO in mainMacho.MachOs)
                {
                    result = VerifyDwarfBinary(subMachO);
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

        private static CanAnalyzeDwarfResult VerifyDwarfBinary(IDwarfBinary binary)
        {
            // We check for "any usage of non-gcc" as a default/standard compilation with clang leads to [GCC, Clang]
            // either because it links with a gcc-compiled object (cstdlib) or the linker also reading as GCC.
            // This has a potential for a False Negative if teams are using GCC and other tools.
            if (binary.Compilers.Any(c => c.Compiler != ElfCompilerType.GCC && c.Compiler != ElfCompilerType.Clang))
            {
                return new CanAnalyzeDwarfResult
                {
                    Reason = MetadataConditions.ImageNotBuiltWithGccOrClang,
                    Result = AnalysisApplicability.NotApplicableToSpecifiedTarget
                };
            }
            else if (binary.Compilers.Any(c => c.Compiler == ElfCompilerType.GCC && c.Version.Major < 8))
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
