// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableHighEntropyVirtualAddresses : PEBinarySkimmerBase
    {
        /// <summary>
        /// BA2015
        /// </summary>
        public override string Id => RuleIds.EnableHighEntropyVirtualAddresses;

        /// <summary>
        /// Binaries should be marked as high entropy Address Space Layout Randomization
        /// (ASLR) compatible. High entropy allows ASLR to be more effective in
        /// mitigating memory corruption vulnerabilities. To resolve this issue,
        /// configure your tool chain to mark the program high entropy compatible;
        /// e.g. by supplying /HIGHENTROPYVA to the C or C++ linker command line.
        /// Binaries must also be compiled as /LARGEADDRESSAWARE in order to enable
        /// high entropy ASLR.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2015_EnableHighEntropyVirtualAddresses_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2015_Pass),
                    nameof(RuleResources.BA2015_Error_NoHighEntropyVA),
                    nameof(RuleResources.BA2015_Error_NoLargeAddressAware),
                    nameof(RuleResources.BA2015_Error_NeitherHighEntropyVANorLargeAddressAware),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageLikelyLoadsAs32BitProcess;
            if (portableExecutable.PEHeaders.PEHeader.Magic != PEMagic.PE32Plus)
            {
                // If the image's magic bytes are 'PE32', it is either a 32 bit binary (rule does not apply), or it is a managed binary compiled as AnyCpu.
                // If it's an AnyCPU managed binary, we need to do a bit more checking--if it has 'Prefers32Bit'/'Requires32Bit' flagged, it will probably
                // load as a 32 bit process.  If it doesn't, we're likely to load in a 64 bit process space on a 64 bit arch & want to ensure HighEntropyVA is enabled.
                if (!portableExecutable.IsManaged ||
                        portableExecutable.IsManaged && portableExecutable.PEHeaders.CorHeader.Flags.HasFlag(CorFlags.Requires32Bit))
                {
                    return result;
                }
            }

            reasonForNotAnalyzing = MetadataConditions.ImageIsNotExe;
            if (!portableExecutable.PEHeaders.IsExe) { return result; }

            // A dotnet core entry point dll is itself loaded within a process
            // that will always be configured for high entropy va, if available.
            reasonForNotAnalyzing = MetadataConditions.ImageIsDotNetCoreEntryPointDll;
            if (portableExecutable.IsDotNetCore || portableExecutable.IsDotNetStandard) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PEHeader peHeader = target.PE.PEHeaders.PEHeader;
            DllCharacteristics dllCharacteristics = peHeader.DllCharacteristics;

            CoffHeader coffHeader = target.PE.PEHeaders.CoffHeader;
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
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2015_Error_NeitherHighEntropyVANorLargeAddressAware),
                        context.TargetUri.GetFileName()));
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
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2015_Error_NoHighEntropyVA),
                        context.TargetUri.GetFileName()));
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
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2015_Error_NoLargeAddressAware),
                        context.TargetUri.GetFileName()));
                return;
            }

            //'{0}' is high entropy ASLR compatible.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2015_Pass),
                        context.TargetUri.GetFileName()));
        }
    }
}
