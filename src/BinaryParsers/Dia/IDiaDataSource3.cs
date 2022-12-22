// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dia2Lib
{
    [ComImport]
    [Guid("65A23C15-BAB3-45DA-8639-F06DE86B9EA8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDiaDataSource3 : IDiaDataSource2
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
        new void getRawPDBPtr(out IntPtr pppdb);

        [MethodImpl(MethodImplOptions.InternalCall)]
        new void loadDataFromRawPDBPtr([In] IntPtr ppdb);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void getStreamSize([In][MarshalAs(UnmanagedType.LPWStr)] string stream, out uint pcb);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void getStreamRawData([In][MarshalAs(UnmanagedType.LPWStr)] string stream, [In] uint cbRead, out byte pbData);
    }
}
