// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    /// <summary>
    /// Receives callbacks from the DIA symbol locating procedure, thus enabling
    /// a user interface to report on the progress of the location attempt.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("C32ADB82-73F4-421b-95D5-A4706EDF5DBE")]
    public interface IDiaLoadCallback
    {
        /// <summary>
        /// Called when a debug directory was found in the .exe file.
        /// </summary>
        /// <param name="executable">
        /// TRUE if the debug directory is read from an executable (rather than a .dbg file).
        /// </param>
        /// <param name="dataSize">Count of bytes of data in the debug directory.</param>
        /// <param name="data">
        /// An array that is filled in with the debug directory (format is _IMAGE_DEBUG_DIRECTORY).
        /// </param>
        void NotifyDebugDir(
            [MarshalAs(UnmanagedType.Bool)] bool executable,
            int dataSize,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data);

        /// <summary>
        /// Called when a candidate .dbg file has been opened.
        /// </summary>
        /// <param name="dbgPath">The full path of the .dbg file.</param>
        /// <param name="resultCode">
        /// Code that indicates the success (S_OK) or failure of the load as applied
        /// to this file.
        /// </param>
        void NotifyOpenDbg(
            [MarshalAs(UnmanagedType.LPWStr)] string dbgPath,
            DiaHresult resultCode);

        /// <summary>
        /// Called when a candidate .pdb file is opened.
        /// </summary>
        /// <param name="pdbPath">The full path of the .pdb file.</param>
        /// <param name="resultCode">
        /// Code that indicates the success (S_OK) or failure of the load as applied to
        /// this file.
        /// </param>
        void NotifyOpenPdb(
            [MarshalAs(UnmanagedType.LPWStr)] string pdbPath,
            DiaHresult resultCode);

        /// <summary>
        /// return hr != S_OK to prevent querying the registry for symbol search paths.
        /// </summary>
        /// <returns>
        /// Return true to restrict access (S_FALSE).
        /// Return false to allow access (S_OK).
        /// </returns>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool RestrictRegistryAccess();

        /// <summary>
        /// return hr != S_OK to prevent accessing a symbol server.
        /// </summary>
        /// <returns>
        /// Return true to restrict access (S_FALSE).
        /// Return false to allow access (S_OK).
        /// </returns>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool RestrictSymbolServerAccess();
    }

    /// <summary>
    /// Receives callbacks from the DIA symbol locating procedure, allowing restrictions
    /// to be imposed on the locating process.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("4688a074-5a4d-4486-aea8-7b90711d9f7c")]
    public interface IDiaLoadCallback2
    {
        /// <summary>
        /// Called when a debug directory was found in the .exe file.
        /// </summary>
        /// <param name="executable">
        /// TRUE if the debug directory is read from an executable (rather than a .dbg file).
        /// </param>
        /// <param name="dataSize">Count of bytes of data in the debug directory.</param>
        /// <param name="data">
        /// An array that is filled in with the debug directory (format is _IMAGE_DEBUG_DIRECTORY).
        /// </param>
        void NotifyDebugDir(
            [MarshalAs(UnmanagedType.Bool)] bool executable,
            int dataSize,
            [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data);

        /// <summary>
        /// Called when a candidate .dbg file has been opened.
        /// </summary>
        /// <param name="dbgPath">The full path of the .dbg file.</param>
        /// <param name="resultCode">
        /// Code that indicates the success (S_OK) or failure of the load as applied
        /// to this file.
        /// </param>
        void NotifyOpenDbg(
            [MarshalAs(UnmanagedType.LPWStr)] string dbgPath,
            DiaHresult resultCode);

        /// <summary>
        /// Called when a candidate .pdb file is opened.
        /// </summary>
        /// <param name="pdbPath">The full path of the .pdb file.</param>
        /// <param name="resultCode">
        /// Code that indicates the success (S_OK) or failure of the load as applied to
        /// this file.
        /// </param>
        void NotifyOpenPdb(
            [MarshalAs(UnmanagedType.LPWStr)] string pdbPath,
            DiaHresult resultCode);

        /// <summary>
        /// return hr != S_OK to prevent querying the registry for symbol search paths.
        /// </summary>
        /// <returns>
        /// Return true to restrict access (S_FALSE).
        /// Return false to allow access (S_OK).
        /// </returns>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool RestrictRegistryAccess();

        /// <summary>
        /// return hr != S_OK to prevent accessing a symbol server.
        /// </summary>
        /// <returns>
        /// Return true to restrict access (S_FALSE).
        /// Return false to allow access (S_OK).
        /// </returns>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool RestrictSymbolServerAccess();
        /// <summary>
        /// Determines if it is okay to look for a .pdb file in the original debug directory.
        /// return hr != S_OK to prevent looking up PDB specified in the debug directory.
        /// </summary>
        /// <returns>
        /// Return true to restrict access (S_FALSE).
        /// Return false to allow access (S_OK).
        /// </returns>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool RestrictOriginalPathAccess();

        /// <summary>
        /// Determines if looking for a .pdb file is allowed in the path where the .exe file is located.
        /// return hr != S_OK to prevent looking up for PDB where the EXE is located.
        /// </summary>
        /// <returns>
        /// Return true to restrict access (S_FALSE).
        /// Return false to allow access (S_OK).
        /// </returns>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool RestrictReferencePathAccess();

        /// <summary>
        /// Determines if looking for debug information is allowed from .dbg files.
        /// return hr != S_OK to prevent looking up debug information from DBG files.
        /// </summary>
        /// <returns>
        /// Return true to restrict access (S_FALSE).
        /// Return false to allow access (S_OK).
        /// </returns>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool RestrictDbgAccess();

        /// <summary>
        /// Determines if searching for .pdb files is allowed in the system root directory.
        /// return hr != S_OK to prevent looking up PDBs in system root.
        /// </summary>
        /// <returns>
        /// Return true to restrict access (S_FALSE).
        /// Return false to allow access (S_OK).
        /// </returns>
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool RestrictSystemRootAccess();
    }

    /// <summary>
    /// Enables a client application to supply bytes of an executable file as specified
    /// by file position. This method is implemented by the client application and passed
    /// to the IDiaDataSource.LoadDataForExe method as an alternative method for reading
    /// the file.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("587A461C-B80B-4f54-9194-5032589A6319")]
    public interface IDiaReadExeAtOffsetCallback
    {
        /// <summary>
        /// Reads the specified number of bytes starting at the specified offset from
        /// an executable file.
        /// </summary>
        /// <param name="fileOffset">The offset in the executable file to begin reading.</param>
        /// <param name="dataSize">Number of bytes to read.</param>
        /// <param name="dataRead">Receives the number of bytes read.</param>
        /// <param name="data">An array that is filled in with bytes read from file.</param>
        void ReadExecutableAt(
            long fileOffset,
            int dataSize,
            out int dataRead,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data);
    }

    /// <summary>
    /// Enables a client application to supply bytes of an executable file as specified by
    /// a relative virtual address.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("8E3F80CA-7517-432a-BA07-285134AAEA8E")]
    public interface IDiaReadExeAtRvaCallback
    {
        /// <summary>
        /// Reads the specified number of bytes starting at the specified relative virtual
        /// address (RVA) from the executable file.
        /// </summary>
        /// <param name="relativeVirtualAddress">The RVA in the executable file to begin reading.</param>
        /// <param name="dataSize">Number of bytes to read.</param>
        /// <param name="dataRead">Returns the number of bytes read.</param>
        /// <param name="data">An array that is filled in with bytes read from the file.</param>
        void ReadExecutableAtRva(
            int relativeVirtualAddress,
            int dataSize,
            out int dataRead,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data);
    }
}
