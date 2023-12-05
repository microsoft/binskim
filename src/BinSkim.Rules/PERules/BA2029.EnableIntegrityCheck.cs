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
    public class EnableIntegrityCheck : PEBinarySkimmerBase
    {
        /// <summary>
        /// BA2029
        /// </summary>
        public override string Id => RuleIds.EnableIntegrityCheck;

        /// <summary>
        /// Binaries that are loaded by certain Windows features must (and device drivers should) 
        /// opt into Windows validation of their digital signatures by setting the /INTEGRITYCHECK 
        /// linker flag. This option sets the IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY attribute 
        /// in the PE header of binaries which tells the memory manager to validate a binary's 
        /// digital signature when loaded. Any user mode code that is interfacing with Early Launch 
        /// Antimalware (ELAM) drivers, integrates with device firmware execution or is trying to 
        /// load into protected process lite space must enable /INTEGRITYCHECK. This feature applies 
        /// to both 32-but and 64-bit files. Binaries that opt into /INTEGRITYCHECK must be signed
        /// using the Microsoft Azure Code Signing program.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString
        {
            Text = RuleResources.BA2029_EnableIntegrityCheck_Description
        };

        public override bool EnabledByDefault => false;

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2029_Pass),
            nameof(RuleResources.BA2029_Error),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public const uint IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY = 0x0080;

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability notApplicable = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            if (portableExecutable.IsILOnly)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
                return notApplicable;
            }

            if (portableExecutable.IsResourceOnly)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
                return notApplicable;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();

            PEHeader peHeader = target.PE.PEHeaders.PEHeader;
            if (((uint)peHeader.DllCharacteristics & IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY) == IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY)
            {
                // '{0}' was compiled with /INTEGRITYCHECK and will therefore have its digital signature
                // validated at load time when executing in sensitive Windows runtime environments.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2029_Pass),
                    context.CurrentTarget.Uri.GetFileName()));
                return;
            }

            // '{0}' was not compiled with /INTEGRITYCHECK and therefore will not have its digital signature
            // validated at load time. Failing to validate binary signatures increases the risk of loading
            // malicious code in low-level, high-privilege execution environments, including subsystems that
            // provide critical security malware protections. To resolve this problem, pass '/INTEGRITYCHECK'
            // on the linker command line and sign your files using the Microsoft Azure Code Signing program.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                nameof(RuleResources.BA2029_Error),
                context.CurrentTarget.Uri.GetFileName()));
            return;
        }
    }
}
