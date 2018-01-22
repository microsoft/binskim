// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Dia2Lib;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>A source file record in a PDB.</summary>
    /// <seealso cref="T:System.IDisposable"/>
    public sealed class SourceFile : IDisposable
    {
        private readonly IDiaSourceFile _sourceFile;
        private readonly Lazy<byte[]> _hashBytes;
        private bool _disposed;

        /// <summary>
        /// Constructs a wrapper object around an <see cref="IDiaSourceFile"/> instance.
        /// </summary>
        /// <param name="source">The COM RCW for the IDiaSourceFile. This instance takes ownership of the
        /// RCW.</param>
        public SourceFile(IDiaSourceFile source)
        {
            _hashBytes = new Lazy<byte[]>(GetHash);
            _sourceFile = source;
        }

        /// <summary>
        /// The hash of the source file
        /// </summary>
        public byte[] Hash
        {
            get
            {
                this.AssertNotDisposed();
                return _hashBytes.Value;       // NB: evil callers can change the contents of the hash array
            }
        }

        /// <summary>
        /// Hash type (SHA1, MD5 or None)
        /// </summary>
        public HashType HashType
        {
            get
            {
                this.AssertNotDisposed();
                return (HashType)_sourceFile.checksumType;
            }
        }

        /// <summary>Gets the filename for this source file stored in the PDB.</summary>
        /// <value>The file name for this source file stored in the PDB.</value>
        public string FileName
        {
            get
            {
                this.AssertNotDisposed();
                return _sourceFile.fileName;
            }
        }

        /// <summary>
        /// Destroys this <see cref="SourceFile"/>
        /// </summary>
        /// <seealso cref="M:System.IDisposable.Dispose()"/>
        public void Dispose()
        {
            if (!_disposed)
            {
                Marshal.ReleaseComObject(_sourceFile);
            }

            _disposed = true;
        }

        private byte[] GetHash()
        {
            IntPtr nativeBuffer = IntPtr.Zero;

            try
            {
                uint hashLength = 0;
                _sourceFile.get_checksum(0, out hashLength, IntPtr.Zero);

                int allocSize = checked((int)hashLength);
                nativeBuffer = Marshal.AllocHGlobal(allocSize);

                uint actualHashLength;
                _sourceFile.get_checksum(hashLength, out actualHashLength, nativeBuffer);
                if (actualHashLength != hashLength)
                {
                    throw new InvalidOperationException("Inconsistent hash lengths returned from IDiaSourceFile::get_checksum.");
                }

                byte[] hashResult = new byte[allocSize];
                Marshal.Copy(nativeBuffer, hashResult, 0, allocSize);
                return hashResult;
            }
            finally
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }

        private void AssertNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("SourceFile");
            }
        }
    }

    /// <summary>
    /// Hash type enum
    /// </summary>
    public enum HashType : uint
    {
        None = 0,
        MD5,
        SHA1
    }
}
