// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000001-0000-0000-C000-000000000046")]
    public interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
        int LockServer(int fLock);
    }

    public class FakeComActivator
    {
        internal delegate int DllGetClassObject([In, MarshalAs(UnmanagedType.LPStruct)] Guid ClassId, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppvObject);

        [DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true, BestFitMapping = false, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi, ExactSpelling = true, BestFitMapping = false)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        public static void CoCreateFromFile(string filenameOfServer, Guid clsidOfServer, Guid riid, out IntPtr pvObject)
        {
            // We are leaking this HMODULE. We don't have the machinery here to do the whole
            // DllCanUnloadNow mess that COM requires to do an unload safely; we'll just let
            // process teardown take care of things.
            Guid iidIUnknown = new Guid("00000001-0000-0000-C000-000000000046");
            IntPtr hmod = FakeComActivator.LoadLibraryW(filenameOfServer);
            if (hmod == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            IntPtr fptr = GetProcAddress(hmod, "DllGetClassObject");
            if (fptr == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            DllGetClassObject delegateForDllGetClassObject = (DllGetClassObject)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(fptr, typeof(DllGetClassObject));
            IntPtr pClassFactory = IntPtr.Zero;
            int hr = delegateForDllGetClassObject(clsidOfServer, new Guid("00000001-0000-0000-C000-000000000046"), out pClassFactory);
            //IClassFactory classFactory = (IClassFactory)Marshal.GetTypedObjectForIUnknown(pClassFactory, typeof(IClassFactory));
            IClassFactory classFactory = Marshal.GetObjectForIUnknown(pClassFactory) as IClassFactory;
            classFactory.CreateInstance(IntPtr.Zero, ref riid, out pvObject);
            Marshal.Release(pClassFactory);
            Marshal.ReleaseComObject(classFactory);
        }
    }
}