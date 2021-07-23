// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class DoNotAllowExecutableStack : MachOBinarySkimmer
    {
        /// <summary>
        /// BA5002
        /// </summary>
        public override string Id => RuleIds.DoNotAllowExecutableStack;

        /// <summary>
        /// This checks if a binary has an executable stack; an
        /// executable stack allows attackers to redirect code flow
        /// into stack memory, which is an easy place for an attacker
        /// to store shellcode. Ensure do not enable flag "--allow_stack_execute".
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA5002_DoNotAllowExecutableStack_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA5002_Pass),
                    nameof(RuleResources.BA5002_Error),
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
                    if (IsApplicableMachO(subBinary) && HasExecutableStackFlag(subBinary))
                    {
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                                nameof(RuleResources.BA5002_Error),
                                context.TargetUri.GetFileName()));
                        return;
                    }
                }

                // Executable stack is not allowed on executable '{0}'.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA5002_Pass),
                        context.TargetUri.GetFileName()));
            }
        }

        private static bool IsApplicableMachO(SingleMachOBinary macho)
        {
            return (macho.MachO.FileType == ELFSharp.MachO.FileType.Executable ||
                    macho.MachO.FileType == ELFSharp.MachO.FileType.DynamicLibrary);
        }

        private static bool HasExecutableStackFlag(SingleMachOBinary binary)
        {
            // reference
            // https://developer.apple.com/library/archive/documentation/Security/Conceptual/SecureCodingGuide/Articles/BufferOverflows.html
            return binary.MachO.Flags.HasFlag(ELFSharp.MachO.HeaderFlags.AllowStackExecution);
        }
    }
}
