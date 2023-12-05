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
    public class EnableShadowStack : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA2025
        /// </summary>
        public override string Id => RuleIds.EnableShadowStack;

        /// <summary>
        /// Control-flow Enforcement Technology (CET) Shadow Stack is a computer processor feature
        /// that provides capabilities to defend against return-oriented programming (ROP) based
        /// malware attacks.
        /// Note: older versions of .NET are not compatible with CET/shadow stack technology. 
        /// If your native process loads older managed assemblies (.NET 6 or earlier), 
        /// unhandled exceptions in those components may not be handled properly 
        /// and may cause your process to crash.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString
        {
            Text = RuleResources.BA2025_EnableShadowStack_Description
        };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2025_Pass),
            nameof(RuleResources.BA2025_Warning),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

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

            if (portableExecutable.IsNativeUniversalWindowsPlatform)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsNativeUniversalWindowsPlatformBinary;
                return notApplicable;
            }

            if (portableExecutable.Machine == Machine.Arm64)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsArm64BitBinary;
                return notApplicable;
            }

            if (portableExecutable.Machine == Machine.Arm || portableExecutable.Machine == Machine.ArmThumb2)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsArmBinary;
                return notApplicable;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            IEnumerable<DebugDirectoryEntry> debugDirectories = target.PE.DebugDirectories;

            if (debugDirectories == null)
            {
                return;
            }

            foreach (DebugDirectoryEntry debugDirectory in debugDirectories)
            {
                if ((ImageDebugType)debugDirectory.Type == ImageDebugType.IMAGE_DEBUG_TYPE_EX_DLLCHARACTERISTICS)
                {
                    PEMemoryBlock memory = target.PE.GetSectionData(debugDirectory.DataRelativeVirtualAddress);
                    if ((memory.GetReader().ReadUInt16() & (ushort)ImageDllCharacteristicsEx.IMAGE_DLLCHARACTERISTICS_EX_CET_COMPAT)
                        == (ushort)ImageDllCharacteristicsEx.IMAGE_DLLCHARACTERISTICS_EX_CET_COMPAT)
                    {
                        // '{0}' enables the Control-flow Enforcement Technology (CET) Shadow Stack mitigation.
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                            nameof(RuleResources.BA2025_Pass),
                            context.CurrentTarget.Uri.GetFileName()));
                        return;
                    }
                }
            }

            // '{0}' does not enable the Control-flow Enforcement Technology (CET) Shadow Stack mitigation.
            // To resolve this issue, pass /CETCOMPAT on the linker command lines.
            // Note: older versions of .NET are not compatible with CET/shadow stack technology.
            // If your native process loads older managed assemblies (.NET 6 or earlier),
            // unhandled exceptions in those components may not be handled properly
            // and may cause your process to crash.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                nameof(RuleResources.BA2025_Warning),
                context.CurrentTarget.Uri.GetFileName()));
        }
    }
}
