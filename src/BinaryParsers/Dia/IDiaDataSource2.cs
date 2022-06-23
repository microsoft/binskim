// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dia2Lib
{
    [ComImport]
    [Guid("6D31CB3B-EDD4-4C3E-AB44-12B9F7A3828E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDiaDataSource2 : IDiaDataSource
    {
        [DispId(1)]
        new string lastError
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        new void loadDataFromPdb([In][MarshalAs(UnmanagedType.LPWStr)] string pdbPath);

        [MethodImpl(MethodImplOptions.InternalCall)]
        new void loadAndValidateDataFromPdb([In][MarshalAs(UnmanagedType.LPWStr)] string pdbPath, [In] ref Guid pcsig70, [In] uint sig, [In] uint age);

        [MethodImpl(MethodImplOptions.InternalCall)]
        new void loadDataForExe([In][MarshalAs(UnmanagedType.LPWStr)] string executable, [In][MarshalAs(UnmanagedType.LPWStr)] string searchPath, [In][MarshalAs(UnmanagedType.IUnknown)] object pCallback);

        [MethodImpl(MethodImplOptions.InternalCall)]
        new void loadDataFromIStream([In][MarshalAs(UnmanagedType.Interface)] IStream pIStream);

        [MethodImpl(MethodImplOptions.InternalCall)]
        new void openSession([MarshalAs(UnmanagedType.Interface)] out IDiaSession ppSession);

        [MethodImpl(MethodImplOptions.InternalCall)]
        new void loadDataFromCodeViewInfo([In][MarshalAs(UnmanagedType.LPWStr)] string executable, [In][MarshalAs(UnmanagedType.LPWStr)] string searchPath, [In] uint cbCvInfo, [In] ref byte pbCvInfo, [In][MarshalAs(UnmanagedType.IUnknown)] object pCallback);

        [MethodImpl(MethodImplOptions.InternalCall)]
        new void loadDataFromMiscInfo([In][MarshalAs(UnmanagedType.LPWStr)] string executable, [In][MarshalAs(UnmanagedType.LPWStr)] string searchPath, [In] uint timeStampExe, [In] uint timeStampDbg, [In] uint sizeOfExe, [In] uint cbMiscInfo, [In] ref byte pbMiscInfo, [In][MarshalAs(UnmanagedType.IUnknown)] object pCallback);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void getRawPDBPtr(out IntPtr pppdb);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void loadDataFromRawPDBPtr([In] IntPtr ppdb);
    }
}
