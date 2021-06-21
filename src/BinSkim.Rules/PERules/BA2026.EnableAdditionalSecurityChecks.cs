// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableAdditionalSecurityChecks : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA2026
        /// </summary>
        public override string Id => RuleIds.EnableAdditionalSecurityChecks;

        /// <summary>
        /// /sdl enables a superset of the baseline security checks provided by /GS and overrides /GS-. 
        /// By default, /sdl is off. /sdl- disables the additional security checks.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString
        {
            Text = RuleResources.BA2026_EnableAdditionalSecurityChecks_Description
        };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2026_Pass),
            nameof(RuleResources.BA2026_Warning),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
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
                if ((ImageDebugType)debugDirectory.Type == ImageDebugType.IMAGE_DEBUG_TYPE_VC_FEATURE)
                {
                    PEMemoryBlock memory = target.PE.GetSectionData(debugDirectory.DataRelativeVirtualAddress);
                    BlobReader reader = memory.GetReader();
                    reader.ReadUInt32(); // Pre-VC++ 11.00 flag
                    reader.ReadUInt32(); // C/C++ Version
                    reader.ReadUInt32(); // /GS setting
                    uint sdlSetting = reader.ReadUInt32(); // /sdl setting
                    reader.ReadUInt32(); // guardN setting

                    if (sdlSetting == 1)
                    {
                        // '{0}' enables the recommended Security Development Lifecycle (SDL) checks.
                        // These checks change security-relevant warnings into errors, and set additional secure code-generation features.
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                            nameof(RuleResources.BA2026_Pass),
                            context.TargetUri.GetFileName()));
                        return;
                    }
                    else
                    {
                        // '{0}' does not enable the recommended Security Development Lifecycle (SDL) checks.
                        // To Enable the recommended Security Development Lifecycle (SDL) checks pass /sdl on the cl.exe command-line.
                        context.Logger.Log(this,
                            RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                            nameof(RuleResources.BA2026_Warning),
                            context.TargetUri.GetFileName()));
                        return;
                    }
                }
            }
        }
    }
}
