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
    public class EnableMicrosoftCompilerSdlSwitch : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA2026
        /// Previously EnableAdditionalSdlSecurityChecks.
        /// </summary>
        public override string Id => RuleIds.EnableMicrosoftCompilerSdlSwitch;

        /// <summary>
        /// /sdl enables a superset of the baseline security checks provided by /GS and overrides /GS-. 
        /// By default, /sdl is off. /sdl- disables the additional security checks.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString
        {
            Text = RuleResources.BA2026_EnableAdditionalSecurityChecks_Description
        };

        protected override ICollection<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2026_Pass),
            nameof(RuleResources.BA2026_Warning),
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

            uint sdlEnabled = 0xffffffff;

            foreach (DebugDirectoryEntry debugDirectory in debugDirectories)
            {
                if ((ImageDebugType)debugDirectory.Type != ImageDebugType.IMAGE_DEBUG_TYPE_VC_FEATURE)
                {
                    continue;
                }

                PEMemoryBlock memory = target.PE.GetSectionData(debugDirectory.DataRelativeVirtualAddress);
                BlobReader reader = memory.GetReader();

                reader.ReadUInt32(); // Pre-VC++ 11.00 flag
                reader.ReadUInt32(); // C/C++ Version
                reader.ReadUInt32(); // -GS setting

                sdlEnabled = reader.ReadUInt32(); // -sdl setting
                break;
            }

            switch (sdlEnabled)
            {
                case 0:
                {
                    // '{0}' is a Windows PE that wasn't compiled with recommended Security
                    // Development Lifecycle (SDL) checks. As a result some critical compile-time
                    // and runtime checks may be disabled, increasing the possibility of an
                    // exploitable runtime issue. To resolve this problem, pass '/sdl' on the
                    // cl.exe command-line, set the 'SDL checks' property in the 
                    // 'C/C++ -> General' Configuration property page, or explicitly set the
                    // 'SDLCheck' property in the project file (nested within a 'CLCompile'
                    // element) to 'true'.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                        nameof(RuleResources.BA2026_Warning),
                        context.CurrentTarget.Uri.GetFileName()));
                    return;
                }

                case 1:
                {
                    // '{0}' is a Windows PE that was compiled with recommended Security
                    // Development Lifecycle (SDL) checks. These checks change security-relevant
                    // warnings into errors, and set additional secure code-generation features.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA2026_Pass),
                        context.CurrentTarget.Uri.GetFileName()));
                    return;
                }

                default: { break; }
            }

            // '{0}' is a Windows PE that wasn't compiled with a compiler that provides the Microsoft
            // /sdl command-line setting to enable additional compile-time and runtime security checks.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.NotApplicable, context, null,
                nameof(RuleResources.BA2026_NotApplicable),
                context.CurrentTarget.Uri.GetFileName()));
        }
    }
}
