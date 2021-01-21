// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableAddressSpaceLayoutRandomization : PEBinarySkimmerBase
    {
        /// <summary>
        /// BA2009
        /// </summary>
        public override string Id => RuleIds.EnableAddressSpaceLayoutRandomization;

        /// <summary>
        /// Binaries should linked as DYNAMICBASE in order to be eligible for relocation
        /// by Address Space Layout Randomization (ASLR). ASLR is an important
        /// mitigation that makes it more difficult for an attacker to exploit
        /// memory corruption vulnerabilities. Configure your tool chain to build with
        /// this feature enabled. For C and C++ binaries, add /DYNAMICBASE to your
        /// linker command line. For .NET applications, use a compiler shipping with
        /// Visual Studio 2008 or later.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2009_EnableAddressSpaceLayoutRandomization_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2009_Pass),
                    nameof(RuleResources.BA2009_Error_NotDynamicBase),
                    nameof(RuleResources.BA2009_Error_RelocsStripped),
                    nameof(RuleResources.BA2009_Error_WinCENoRelocationSection),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsXBoxBinary;
            if (portableExecutable.IsXBox) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsPreVersion7WindowsCEBinary;
            if (OSVersions.IsWindowsCEPriorToV7(portableExecutable)) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsBootBinary;
            if (portableExecutable.IsBoot) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PEHeader optionalHeader = target.PE.PEHeaders.PEHeader;

            if ((optionalHeader.DllCharacteristics & DllCharacteristics.DynamicBase) != DllCharacteristics.DynamicBase)
            {
                // '{0}' is not marked as DYNAMICBASE. This means that the binary is not eligible for relocation 
                // by Address Space Layout Randomization (ASLR). ASLR is an important mitigation that makes it 
                // more difficult for an attacker to exploit memory corruption vulnerabilities. To resolve this 
                // issue, configure your tool chain to build with this feature enabled. For C and C++ binaries, 
                // add /DYNAMICBASE to your linker command line. For .NET applications, use a compiler shipping 
                // with Visual Studio 2008 or later.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2009_Error_NotDynamicBase),
                        context.TargetUri.GetFileName()));
                return;
            }

            CoffHeader coffHeader = target.PE.PEHeaders.CoffHeader;

            if ((coffHeader.Characteristics & Characteristics.RelocsStripped) == Characteristics.RelocsStripped)
            {
                // '{0}' is marked as DYNAMICBASE but relocation data has been stripped
                // from the image, preventing address space layout randomization. 
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2009_Error_RelocsStripped),
                        context.TargetUri.GetFileName()));
                return;
            }

            if (target.PE.Subsystem == Subsystem.WindowsCEGui)
            {
                Debug.Assert(target.PE.OSVersion >= OSVersions.WindowsCE7);

                bool relocSectionFound = false;

                // For WinCE 7+ ASLR is a machine-wide setting and binaries must
                // have relocation info present in order to be dynamically rebased.
                foreach (SectionHeader sectionHeader in target.PE.PEHeaders.SectionHeaders)
                {
                    if (sectionHeader.Name.Equals(".reloc", StringComparison.OrdinalIgnoreCase) &&
                        sectionHeader.SizeOfRawData > 0)
                    {
                        relocSectionFound = true;
                        break;
                    }
                }

                if (!relocSectionFound)
                {
                    // EnableAddressSpaceLayoutRandomization_WinCENoRelocationSection_Error	'{0}'
                    // is a Windows CE image but does not contain any relocation data, preventing 
                    // address space layout randomization.	
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                            nameof(RuleResources.BA2009_Error_WinCENoRelocationSection),
                        context.TargetUri.GetFileName()));
                    return;
                }
            }

            //'{0}' is properly compiled to enable address space layout randomization.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2009_Pass),
                        context.TargetUri.GetFileName()));
        }
    }
}
