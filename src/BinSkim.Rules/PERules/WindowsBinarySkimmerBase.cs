// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    // Windows specific binary skimmers.
    public abstract class WindowsBinarySkimmerBase : PEBinarySkimmerBase
    {
        public override SupportedPlatform SupportedPlatforms => SupportedPlatform.Windows;
    }
}
