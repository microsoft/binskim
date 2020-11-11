// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.IL
{
    [Flags]
    public enum Traces
    {
        None = 0,
        PdbLoad = 0x1
    }
}
