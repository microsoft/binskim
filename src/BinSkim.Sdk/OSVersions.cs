// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public static class OSVersions
    {
        public static readonly Version WindowsCE7 = new Version(7, 0);

        public static bool IsWindowsCEPriorToV7(PE portableExecutable)
        {
            return portableExecutable.Subsystem == Subsystem.WindowsCEGui &&
                   portableExecutable.OSVersion < WindowsCE7;
        }
    }
}
