// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;

namespace Microsoft.CodeAnalysis.IL.Rules.Helpers
{
    public static class PEHeaderHelper
    {
        /// <summary>
        /// This is similar to the rule BA2001.
        /// </summary>
        /// <param name="peHeader"></param>
        /// <returns><see cref="ValidationState"/></returns>
        public static ValidationState AnalyzeLoadImagesAboveFourGigabyteAddress(PEHeader peHeader)
        {
            return peHeader.ImageBase <= 0xFFFFFFFF
                ? ValidationState.Error
                : ValidationState.Pass;
        }
    }
}
