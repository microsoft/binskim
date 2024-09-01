// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;


public class ResourceReleaser : IResourceReleaser
{
    public void Release(object resource)
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

    public object GetObjectForIUnknown(nint pointer)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Marshal.GetObjectForIUnknown(pointer)
            : throw new PlatformNotSupportedException();
    }
}

