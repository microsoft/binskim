// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule))]
    public class MarkImageAsNXCompatible : BinarySkimmerBase
    {
        /// <summary>
        /// BA2016
        /// </summary>
        public override string Id { get { return RuleIds.MarkImageAsNXCompatibleId; } }

        /// <summary>
        /// Binaries should be marked as NX compatible in order to help prevent
        /// execution of untrusted data as code. The NXCompat bit, also known
        /// as "Data Execution Prevention" (DEP) or "Execute Disable" (XD),
        /// triggers a processor security feature that allows a program to mark
        /// a piece of memory as non-executable. This helps mitigate memory
        /// corruption vulnerabilities by preventing an attacker from supplying
        /// direct shellcode in their exploit (because the exploit comes in the
        /// form of input data to the exploited program on a data segment,
        /// rather than on an executable code segment). Ensure that your tool
        /// chain is configured to mark your binaries as NX compatible, e.g. by 
        /// passing /NXCOMPAT to the C/C++ linker.
        /// </summary>

        public override string FullDescription
        {
            get { return RuleResources.BA2016_MarkImageAsNXCompatible_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2016_Pass),
                    nameof(RuleResources.BA2016_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIs64BitBinary;
            if (context.PE.PEHeaders.PEHeader.Magic == PEMagic.PE32Plus) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsXBoxBinary;
            if (portableExecutable.IsXBox) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsPreVersion7WindowsCEBinary;
            if (OSVersions.IsWindowsCEPriorToV7(portableExecutable)) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsBootBinary;
            if (portableExecutable.IsBoot) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;

            if ((peHeader.DllCharacteristics & DllCharacteristics.NxCompatible /*IMAGE_DLLCHARACTERISTICS_NX_COMPAT*/) == 0)
            {
                // '{0}' is not marked NX compatible. The NXCompat bit, also known as "Data Execution Prevention"
                // (DEP) or "Execute Disable" (XD), is a processor feature that allows a program to mark a piece
                // of memory as non - executable. This helps mitigate memory corruption vulnerabilities by 
                // preventing an attacker from supplying direct shellcode in their exploit, because the exploit 
                // comes in the form of input data to the exploited program on a data segment, rather than on an
                // executable code segment. To resolve this issue, ensure that your tool chain is configured to mark 
                //your binaries as NX compatible, e.g. by passing / NXCOMPAT to the C / C++ linker.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                        nameof(RuleResources.BA2016_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' is marked as NX compatible.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                    nameof(RuleResources.BA2016_Pass),
                        context.TargetUri.GetFileName()));
        }
    }
}
