// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinSkim.Sdk;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    [Export(typeof(IBinarySkimmer))]
    public class MarkImageAsNXCompatible : IBinarySkimmer, IRuleContext
    {
        public string Id { get { return RuleConstants.MarkImageAsNXCompatibleId; } }

        public string Name { get { return nameof(MarkImageAsNXCompatible); } }

        public void Initialize(BinaryAnalyzerContext context) { return; }

        public AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
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

            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public void Analyze(BinaryAnalyzerContext context)
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
                context.Logger.Log(MessageKind.Fail, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.MarkImageAsNXCompatible_Fail));
                return;
            }

            // '{0}' is marked as NX compatible.
            context.Logger.Log(MessageKind.Pass, context,
                RuleUtilities.BuildMessage(context,
                    RulesResources.MarkImageAsNXCompatible_Pass));
        }
    }
}
