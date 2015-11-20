// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Composition;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinSkim.Sdk;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    [Export(typeof(IBinarySkimmer))]
    public class EnableControlFlowGuard : IBinarySkimmer, IRuleContext
    {
        public string Id { get { return RuleConstants.EnableControlFlowGuardId; } }

        public string Name { get { return nameof(EnableControlFlowGuard); } }

        public const UInt32 IMAGE_DLLCHARACTERISTICS_CONTROLFLOWGUARD = 0x4000;
        public const UInt32 IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG = 10;
        public const UInt32 IMAGE_GUARD_CF_INSTRUMENTED = 0x0100;
        public const UInt32 IMAGE_GUARD_CF_FUNCTION_TABLE_PRESENT = 0x0400;
        public const UInt32 IMAGE_GUARD_CF_CHECKS = IMAGE_GUARD_CF_INSTRUMENTED | IMAGE_GUARD_CF_FUNCTION_TABLE_PRESENT;
        public const UInt32 IMAGE_LOAD_CONFIG_MINIMUM_SIZE_32 = 0x005C;
        public const UInt32 IMAGE_LOAD_CONFIG_MINIMUM_SIZE_64 = 0x0090;

        public Version MinimumSupportedLinkerVersion = new Version("14.0");

        public void Initialize(BinaryAnalyzerContext context) { return; }

        public AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyManagedAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            if (portableExecutable.LinkerVersion < MinimumSupportedLinkerVersion)
            {
                reasonForNotAnalyzing = RuleUtilities.BuildObsoleteToolsMessage(
                    portableExecutable.LinkerVersion, MinimumSupportedLinkerVersion);
                return result;
            }

            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public void Analyze(BinaryAnalyzerContext context)
        {
            // '{0} does not contain a load configuration table, indicating that it does not enable the control flow guard mitigation.PEHeader peHeader = context.PE.PEHeaders.PEHeader;

            if (!EnablesControlFlowGuard(context))
            {
                context.Logger.Log(MessageKind.Fail, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.EnableControlFlowGuard_Fail));
                return;
            }

            // '{0}' enables the control flow guard mitigation.
            context.Logger.Log(MessageKind.Pass, context,
                RuleUtilities.BuildMessage(context,
                    RulesResources.EnableControlFlowGuard_Pass));
        }

        private bool EnablesControlFlowGuard(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;
            if (((uint)peHeader.DllCharacteristics & IMAGE_DLLCHARACTERISTICS_CONTROLFLOWGUARD) == 0)
            {
                return false;
            }

            SafePointer loadConfigRVA = new SafePointer(context.PE.ImageBytes, peHeader.LoadConfigTableDirectory.RelativeVirtualAddress);
            if (loadConfigRVA.Address == 0)
            {
                return false;
            }

            SafePointer loadConfigVA = context.PE.RVA2VA(loadConfigRVA);

            if (context.PE.Is64Bit)
            {
                ImageLoadConfigDirectory64 loadConfig = new ImageLoadConfigDirectory64(peHeader, loadConfigVA);

                Int32 imageDirectorySize = (Int32)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.Size);
                UInt64 guardCFCheckFunctionPointer = (UInt64)loadConfig.GetField(ImageLoadConfigDirectory64.Fields.GuardCFCheckFunctionPointer);
                UInt64 guardCFFunctionTable = (UInt64)loadConfig.GetField(ImageLoadConfigDirectory64.Fields.GuardCFFunctionTable);
                UInt32 guardFlags = (UInt32)loadConfig.GetField(ImageLoadConfigDirectory64.Fields.GuardFlags);

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
                ImageLoadConfigDirectory32 loadConfig = new ImageLoadConfigDirectory32(peHeader, loadConfigVA);

                Int32 imageDirectorySize = (Int32)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.Size);
                UInt32 guardCFCheckFunctionPointer = (UInt32)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.GuardCFCheckFunctionPointer);
                UInt32 guardCFFunctionTable = (UInt32)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.GuardCFFunctionTable);
                UInt32 guardFlags = (UInt32)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.GuardFlags);

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
