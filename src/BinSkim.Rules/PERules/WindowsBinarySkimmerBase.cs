// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    // Windows specific binary skimmers.
    public abstract class WindowsBinarySkimmerBase : PEBinarySkimmerBase
    {
        // Placeholder until SARIF 1.7.2 releases with SupportedPlatform.
        public bool SupportedPlatforms => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
