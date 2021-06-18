// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable
{
    public enum ImageDllCharacteristicsEx
    {
        IMAGE_DLLCHARACTERISTICS_EX_CET_COMPAT = 0x01,
        IMAGE_DLLCHARACTERISTICS_EX_CET_COMPAT_STRICT_MODE = 0x02,
        IMAGE_DLLCHARACTERISTICS_EX_CET_SET_CONTEXT_IP_VALIDATION_RELAXED_MODE = 0x04,
        IMAGE_DLLCHARACTERISTICS_EX_CET_DYNAMIC_APIS_ALLOW_IN_PROC = 0x08,
        IMAGE_DLLCHARACTERISTICS_EX_CET_RESERVED_1 = 0x10, // Reserved for CET policy *downgrade* only
        IMAGE_DLLCHARACTERISTICS_EX_CET_RESERVED_2 = 0x20, // Reserved for CET policy *downgrade* only
    }
}
