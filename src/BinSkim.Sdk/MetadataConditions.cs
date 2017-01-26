// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public static class MetadataConditions
    {
        public static readonly string ImageIsNotExe = SdkResources.MetadataCondition_ImageIsNotExe;
        public static readonly string CouldNotLoadPdb = SdkResources.MetadataCondition_CouldNotLoadPdb;
        public static readonly string ImageIsNotSigned = SdkResources.MetadataCondition_ImageIsNotSigned;
        public static readonly string ImageIsXBoxBinary = SdkResources.MetadataCondition_ImageIsXboxBinary;
        public static readonly string ImageIsBootBinary = SdkResources.MetadataCondition_ImageIsBootBinary;
        public static readonly string ImageIs64BitBinary = SdkResources.MetadataCondition_ImageIs64BitBinary;
        public static readonly string ImageIsNot32BitBinary = SdkResources.MetadataCondition_ImageIsNot32BitBinary;
        public static readonly string ImageIsNot64BitBinary = SdkResources.MetadataCondition_ImageIsNot64BitBinary;
        public static readonly string ImageIsMixedModeBinary = SdkResources.MetadataCondition_ImageIsMixedModeBinary;
        public static readonly string ImageIsKernelModeBinary = SdkResources.MetadataCondition_ImageIsKernelModeBinary;
        public static readonly string ImageIsResourceOnlyBinary = SdkResources.MetadataCondition_ImageIsResourceOnlyBinary;
        public static readonly string ImageIsDotNetNativeBinary = SdkResources.MetadataCondition_ImageIsDotNetNativeBinary;
        public static readonly string ImageIsILOnlyManagedAssembly = SdkResources.MetadataCondition_ImageIsILOnlyManagedAssembly;
        public static readonly string ImageIsManagedInteropAssembly = SdkResources.MetadataCondition_ImageIsManagedInteropAssembly;
        public static readonly string ImageCompiledWithOutdatedTools = SdkResources.MetadataCondition_ImageCompiledWithOutdatedTools;
        public static readonly string ImageIsPreVersion7WindowsCEBinary = SdkResources.MetadataCondition_ImageIsPreVersion7WindowsCEBinary;
        public static readonly string ImageIsManagedResourceOnlyAssembly = SdkResources.MetadataCondition_ImageIsManagedResourceOnlyAssembly;
        public static readonly string ImageIsKernelModeAndNot64Bit_CfgUnsupported = SdkResources.MetadataCondition_ImageIsKernelModeAndNot64Bit_CfgUnsupported;
    }
}
