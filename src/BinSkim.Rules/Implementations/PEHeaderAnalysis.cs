// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.PortableExecutable;

namespace Microsoft.CodeAnalysis.IL.Rules.Implementations
{
    public static class PEHeaderAnalysis
    {
        public static void AnalyzeLoadImageAboveFourGigabyteAddress(PEHeader peHeader)
        {
            if (peHeader.ImageBase <= 0xFFFFFFFF)
            {
                // We have a problem.
            }
        }
    }
}
