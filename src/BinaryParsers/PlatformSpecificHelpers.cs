// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Runtime.InteropServices;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public static class PlatformSpecificHelpers
    {
        public static bool RunningOnWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public static void ThrowIfNotOnWindows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException(
                    string.Format(BinaryParsersResources.PlatformUnsupportedFormat, RuntimeInformation.OSDescription, OSPlatform.Windows));
            }
        }

        public static string GetCurrentOSDescription()
        {
            return RuntimeInformation.OSDescription;
        }
    }
}
