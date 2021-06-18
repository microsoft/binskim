// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable
{
    public enum ImageDebugType
    {
        IMAGE_DEBUG_TYPE_UNKNOWN = 0,
        IMAGE_DEBUG_TYPE_COFF = 1,
        IMAGE_DEBUG_TYPE_CODEVIEW = 2,
        IMAGE_DEBUG_TYPE_FPO = 3,
        IMAGE_DEBUG_TYPE_MISC = 4,
        IMAGE_DEBUG_TYPE_EXCEPTION = 5,
        IMAGE_DEBUG_TYPE_FIXUP = 6,
        IMAGE_DEBUG_TYPE_OMAP_TO_SRC = 7,
        IMAGE_DEBUG_TYPE_OMAP_FROM_SRC = 8,
        IMAGE_DEBUG_TYPE_BORLAND = 9,
        IMAGE_DEBUG_TYPE_RESERVED10 = 10,
        IMAGE_DEBUG_TYPE_CLSID = 11,
        IMAGE_DEBUG_TYPE_VC_FEATURE = 12,
        IMAGE_DEBUG_TYPE_POGO = 13,
        IMAGE_DEBUG_TYPE_ILTCG = 14,
        IMAGE_DEBUG_TYPE_MPX = 15,
        IMAGE_DEBUG_TYPE_REPRO = 16,
        IMAGE_DEBUG_TYPE_EX_DLLCHARACTERISTICS = 20,
    }
}
