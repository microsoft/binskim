﻿// Copyright (c) Microsoft. All rights reserved.
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
    public class LoadImageAboveFourGigabyteAddress : PEBinarySkimmerBase
    {
        /// <summary>
        /// BA2001
        /// </summary>
        public override string Id => RuleIds.LoadImageAboveFourGigabyteAddress;

        /// <summary>
        /// 64-bit images should have a preferred base address above the 4GB boundary in
        /// order to prevent triggering an Address Space Layout Randomization (ASLR)
        /// compatibility mode that decreases security. ASLR compatibility mode reduces
        /// the number of locations to which ASLR may relocate the binary, reducing its
        /// effectiveness at mitigating memory corruption vulnerabilities. To resolve
        /// this issue, either use the default preferred base address by removing any
        /// uses of /baseaddress from compiler command lines, or /BASE from linker
        /// command lines (recommended), or configure your program to start at a base
        /// address above 4GB when compiled for 64 bit platforms (by changing the
        /// constant passed to /baseaddress / /BASE). Note that if you choose to
        /// continue using a custom preferred base address, you will need to make this
        /// modification only for 64-bit builds, as base addresses above 4GB are not
        /// valid for 32-bit binaries.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2001_LoadImageAboveFourGigabyteAddress_Description };

        protected override ICollection<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2001_Pass),
                    nameof(RuleResources.BA2001_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsNot64BitBinary;
            if (portableExecutable.PEHeaders.PEHeader.Magic != PEMagic.PE32Plus) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PEHeader peHeader = target.PE.PEHeaders.PEHeader;

            ulong imageBase = peHeader.ImageBase;

            if (imageBase <= 0xFFFFFFFF)
            {
                // '{0}' is a 64-bit image with a preferred base address below the 4GB boundary. 
                // Having a preferred base address below this boundary triggers a compatibility 
                // mode in Address Space Layout Randomization (ASLR) on recent versions of 
                // Windows that reduces the number of locations to which ASLR may relocate the 
                // binary. This reduces the effectiveness of ASLR at mitigating memory corruption 
                // vulnerabilities. To resolve this issue, either use the default preferred base 
                // address by removing any uses of /baseaddress from compiler command lines, or 
                // /BASE from linker command lines (recommended), or configure your program to 
                // start at a base address above 4GB when compiled for 64 bit platforms (by 
                // changing the constant passed to /baseaddress / /BASE). Note that if you choose 
                // to continue using a custom preferred base address, you will need to make this 
                // modification only for 64-bit builds, as base addresses above 4GB are not valid 
                // for 32-bit binaries.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2001_Error),
                        context.CurrentTarget.Uri.GetFileName()));
                return;
            }

            // '{0}' is marked as NX compatible.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2001_Pass),
                        context.CurrentTarget.Uri.GetFileName()));
        }
    }
}
