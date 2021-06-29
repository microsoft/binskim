// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

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
        private readonly string[] stack_check_symbols = new string[]{
            "__stack_chk_fail",
            "__stack_chk_fail_local", // Optimization for some architectures, according to compiler comments.
            // macho symbol names
            "___stack_chk_fail",
            "___stack_chk_guard",
        };

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
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3003_EnableStackProtector_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA3003_Pass),
                    nameof(RuleResources.BA3003_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzeDwarf(IDwarfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            if (target is ElfBinary elf)
            {
                if (elf.ELF.Type == FileType.Executable || elf.ELF.Type == FileType.SharedObject)
                {
                    return AnalysisApplicability.ApplicableToSpecifiedTarget;
                }

                reasonForNotAnalyzing = MetadataConditions.ElfIsCoreNoneOrObject;
            }
            else if (target is MachOBinary mainMachO)
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
            HashSet<string> symbolNames = null;
            bool result = false;
            if (context.IsELF())
            {
                IELF elf = context.ELFBinary().ELF;
                symbolNames = new HashSet<string>
                (
                    ElfUtility.GetAllSymbols(elf).Select(sym => sym.Name)
                );

                result = symbolNames.Any(symbol => this.stack_check_symbols.Contains(symbol));
            }
            else if (context.IsMachO())
            {
                MachOBinary mainMachO = context.MachOBinary();
                foreach (SingleMachOBinary singleMachO in mainMachO.MachOs)
                {
                    if (!IsApplicableMachO(singleMachO))
                    {
                        continue;
                    }

                    symbolNames = new HashSet<string>
                    (
                        singleMachO.SymbolTables.SelectMany(st => st.Symbols).Select(s => s.Name)
                    );

                    result = symbolNames.Any(symbol => this.stack_check_symbols.Contains(symbol));

                    // if any macho fails the check
                    if (!result)
                    {
                        break;
                    }
                }
            }

            if (result)
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA3003_Pass),
                        context.TargetUri.GetFileName()));
            }
            else
            {
                // If we haven't found the stack protector, we assume it wasn't used.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA3003_Error),
                        context.TargetUri.GetFileName()));
            }
        }

        private static bool IsApplicableMachO(SingleMachOBinary macho)
        {
            return (macho.MachO.FileType == ELFSharp.MachO.FileType.Object ||
                    macho.MachO.FileType == ELFSharp.MachO.FileType.Executable ||
                    macho.MachO.FileType == ELFSharp.MachO.FileType.DynamicLibrary);
        }
    }
}
