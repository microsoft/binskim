// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Composition;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Sdk;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(IBinarySkimmer)), Export(typeof(IRuleDescriptor))]
    public class EnableHighEntropyVirtualAddresses : BinarySkimmerBase
    {
        public override string Id { get { return RuleIds.EnableHighEntropyVirtualAddressesId; } }

        public override string FullDescription
        {
            get { return RulesResources.EnableHighEntropyVirtualAddresses_Description; }
        }

        private static readonly Version s_minHighEntropyVersion = new Version(17, 0, 0, 0);
        
        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsNot64BitBinary;
            if (portableExecutable.PEHeaders.PEHeader.Magic != PEMagic.PE32Plus) { return result; }

            // TODO need to put a check here for verifying that the
            // compiler that built the target supports high entropy va                       

            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;
            DllCharacteristics dllCharacteristics = peHeader.DllCharacteristics;

            CoffHeader coffHeader = context.PE.PEHeaders.CoffHeader;
            Characteristics characteristics = coffHeader.Characteristics;


            bool highEntropyVA = ((int)dllCharacteristics & 0x0020 /*IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA*/) == 0x0020;

            //  /LARGEADDRESSAWARE is necessary for HIGH_ENTROPY_VA to have effect
            bool largeAddressAware = (characteristics & Characteristics.LargeAddressAware /*IMAGE_FILE_LARGE_ADDRESS_AWARE*/) == Characteristics.LargeAddressAware;

            if (!highEntropyVA && !largeAddressAware)
            {
                // '{0}' does not declare itself as high entropy ASLR compatible. High entropy allows 
                // Address Space Layout Randomization to be more effective in mitigating memory 
                // corruption vulnerabilities. To resolve this issue, configure your tool chain to 
                // mark the program high entropy compatible; e.g. by supplying /HIGHENTROPYVA as well
                // as /LARGEADDRESSAWARE to the C or C++ linker command line.
                context.Logger.Log(ResultKind.Error, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.EnableHighEntropyVirtualAddresses_NeitherHighEntropyVANorLargeAddressAware_FAIL));
                return;
            }

            if (!highEntropyVA)
            {
                // '{0}' does not declare itself as high entropy ASLR compatible. High entropy allows 
                // Address Space Layout Randomization to be more effective in mitigating memory 
                // corruption vulnerabilities. To resolve this issue, configure your tool chain to 
                // mark the program high entropy compatible; e.g. by supplying /HIGHENTROPYVA to the
                // C or C++ linker command line. (This image was determined to have been properly 
                // compiled as /LARGEADDRESSAWARE.)
                context.Logger.Log(ResultKind.Error, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.EnableHighEntropyVirtualAddresses_NoHighEntropyVA_FAIL));
                return;
            }

            if (!largeAddressAware)
            {
                // '{0}' does not declare itself as high entropy ASLR compatible. High entropy allows 
                // Address Space Layout Randomization to be more effective in mitigating memory 
                // corruption vulnerabilities. To resolve this issue, configure your tool chain to 
                // mark the program high entropy compatible by supplying /LARGEADDRESSAWARE to the C 
                // or C++ linker command line. (This image was determined to have been properly 
                // compiled as /HIGHENTROPYVA.)
                context.Logger.Log(ResultKind.Error, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.EnableHighEntropyVirtualAddresses_NoLargeAddressAware_FAIL));
                return;
            }

            //'{0}' is high entropy ASLR compatible.
            context.Logger.Log(ResultKind.Pass, context,
                 RuleUtilities.BuildMessage(context,
                    RulesResources.EnableHighEntropyVirtualAddresses_Pass));
        }
    }
}
