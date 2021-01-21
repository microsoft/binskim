// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class EnableControlFlowGuard : PEBinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2008
        /// </summary>
        public override string Id => RuleIds.EnableControlFlowGuard;

        /// <summary>
        /// Binaries should enable the compiler control guard feature (CFG) at build
        /// time in order to prevent attackers from redirecting execution to
        /// unexpected, unsafe locations. CFG analyzes and discovers all
        /// indirect-call instructions at compilation and link time. It also injects
        /// a check that precedes every indirect call in code that ensures the
        /// target is an expected, safe location.  If that check fails at runtime,
        /// the operating system will close the program.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2008_EnableControlFlowGuard_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2008_Pass),
                    nameof(RuleResources.BA2008_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata),
                    nameof(RuleResources.BA2008_NotApplicable_UnsupportedKernelModeVersion)
                };

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                MinimumRequiredLinkerVersion
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.EnableControlFlowGuard + "." + nameof(EnableControlFlowGuard);

        public static PerLanguageOption<Version> MinimumRequiredLinkerVersion { get; } =
            new PerLanguageOption<Version>(
                AnalyzerName, nameof(MinimumRequiredLinkerVersion), defaultValue: () => new Version("14.0"));

        public const uint IMAGE_GUARD_CF_INSTRUMENTED = 0x0100;
        public const uint IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG = 10;
        public const uint IMAGE_LOAD_CONFIG_MINIMUM_SIZE_32 = 0x005C;
        public const uint IMAGE_LOAD_CONFIG_MINIMUM_SIZE_64 = 0x0090;
        public const uint IMAGE_GUARD_CF_FUNCTION_TABLE_PRESENT = 0x0400;
        public const uint IMAGE_DLLCHARACTERISTICS_CONTROLFLOWGUARD = 0x4000;

        public const uint IMAGE_GUARD_CF_CHECKS =
            IMAGE_GUARD_CF_INSTRUMENTED | IMAGE_GUARD_CF_FUNCTION_TABLE_PRESENT;

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsMixedModeBinary;
            if (portableExecutable.IsMixedMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeAndNot64Bit;
            if (portableExecutable.IsKernelMode && !portableExecutable.Is64Bit) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsBootBinary;
            if (portableExecutable.IsBoot) { return result; }

            Version minimumRequiredLinkerVersion = policy.GetProperty(MinimumRequiredLinkerVersion);

            if (portableExecutable.LinkerVersion < minimumRequiredLinkerVersion)
            {
                reasonForNotAnalyzing = string.Format(
                    MetadataConditions.ImageCompiledWithOutdatedTools,
                    portableExecutable.LinkerVersion,
                    minimumRequiredLinkerVersion);

                return result;
            }

            reasonForNotAnalyzing = MetadataConditions.ImageIsWixBinary;
            if (portableExecutable.IsWixBinary) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;

            if (pe.IsKernelMode &&
                (pe.FileVersion.FileMajorPart < 10 || pe.FileVersion.FileBuildPart < 15000) &&
                pe.FileVersion.FileBuildPart < 15000)
            {
                // '{0}' is a kernel mode portable executable compiled for a 
                // version of Windows that does not support the control flow
                // guard feature for kernel mode binaries.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.NotApplicable, context, null,
                        nameof(RuleResources.BA2008_NotApplicable_UnsupportedKernelModeVersion),
                            context.TargetUri.GetFileName()));
            }

            if (!this.EnablesControlFlowGuard(target))
            {
                // '{0}' does not enable the control flow guard (CFG) mitigation. 
                // To resolve this issue, pass /guard:cf on both the compiler
                // and linker command lines. Binaries also require the 
                // /DYNAMICBASE linker option in order to enable CFG.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2008_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' enables the control flow guard mitigation.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2008_Pass),
                        context.TargetUri.GetFileName()));
        }

        private bool EnablesControlFlowGuard(PEBinary target)
        {
            PEHeader peHeader = target.PE.PEHeaders.PEHeader;
            if (((uint)peHeader.DllCharacteristics & IMAGE_DLLCHARACTERISTICS_CONTROLFLOWGUARD) == 0)
            {
                return false;
            }

            var loadConfigRVA = new SafePointer(target.PE.ImageBytes, peHeader.LoadConfigTableDirectory.RelativeVirtualAddress);
            if (loadConfigRVA.Address == 0)
            {
                return false;
            }

            SafePointer loadConfigVA = target.PE.RVA2VA(loadConfigRVA);

            if (target.PE.Is64Bit)
            {
                var loadConfig = new ImageLoadConfigDirectory64(peHeader, loadConfigVA);

                int imageDirectorySize = (int)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.Size);
                ulong guardCFCheckFunctionPointer = (ulong)loadConfig.GetField(ImageLoadConfigDirectory64.Fields.GuardCFCheckFunctionPointer);
                ulong guardCFFunctionTable = (ulong)loadConfig.GetField(ImageLoadConfigDirectory64.Fields.GuardCFFunctionTable);
                uint guardFlags = (uint)loadConfig.GetField(ImageLoadConfigDirectory64.Fields.GuardFlags);

                if (imageDirectorySize >= IMAGE_LOAD_CONFIG_MINIMUM_SIZE_64 &&
                    guardCFCheckFunctionPointer != 0 &&
                    guardCFFunctionTable != 0 &&
                    (guardFlags & IMAGE_GUARD_CF_CHECKS) == IMAGE_GUARD_CF_CHECKS)
                {
                    return true;
                }
            }
            else
            {
                var loadConfig = new ImageLoadConfigDirectory32(peHeader, loadConfigVA);

                int imageDirectorySize = (int)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.Size);
                uint guardCFCheckFunctionPointer = (uint)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.GuardCFCheckFunctionPointer);
                uint guardCFFunctionTable = (uint)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.GuardCFFunctionTable);
                uint guardFlags = (uint)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.GuardFlags);

                if (imageDirectorySize >= IMAGE_LOAD_CONFIG_MINIMUM_SIZE_32 &&
                    guardCFCheckFunctionPointer != 0 &&
                    guardCFFunctionTable != 0 &&
                    (guardFlags & IMAGE_GUARD_CF_CHECKS) == IMAGE_GUARD_CF_CHECKS)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
