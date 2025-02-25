// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    public static class ResourceReleaser
    {
        public static void Release(object resource)
        {
            if (resource != null && Marshal.IsComObject(resource) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Marshal.ReleaseComObject(resource);
            }
            else if (resource is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public static object GetObjectForIUnknown(nint pointer)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Marshal.GetObjectForIUnknown(pointer)
                : new object();
        }
    }
}
