// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dia2Lib
{
    [ComImport]
    [DefaultMember("Item")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("E8368CA9-01D1-419D-AC0C-E31235DBDA9F")]
    public interface IDiaEnumSegments
    {
        [DispId(1)]
        int Count
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "System.Runtime.InteropServices.CustomMarshalers.EnumeratorToEnumVariantMarshaler, CustomMarshalers, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        IEnumerator GetEnumerator();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.Interface)]
        IDiaSegment Item([In] uint index);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void Next([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaSegment rgelt, out uint pceltFetched);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void Skip([In] uint celt);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void Reset();

        [MethodImpl(MethodImplOptions.InternalCall)]
        void Clone([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSegments ppenum);
    }
}
