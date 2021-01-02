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
        private readonly IDiaSourceFile sourceFile;
        private readonly Lazy<byte[]> hashBytes;
        private bool disposed;

        /// <summary>
        /// Constructs a wrapper object around an <see cref="IDiaSourceFile"/> instance.
        /// </summary>
        /// <param name="source">The COM RCW for the IDiaSourceFile. This instance takes ownership of the
        /// RCW.</param>
        public SourceFile(IDiaSourceFile source)
        {
            this.hashBytes = new Lazy<byte[]>(this.GetHash);
            this.sourceFile = source;
        }

        /// <summary>
        /// The hash of the source file
        /// </summary>
        public byte[] Hash
        {
            get
            {
                this.AssertNotDisposed();
                return this.hashBytes.Value;       // NB: evil callers can change the contents of the hash array
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
                return (HashType)this.sourceFile.checksumType;
            }
        }

        /// <summary>Gets the filename for this source file stored in the PDB.</summary>
        /// <value>The file name for this source file stored in the PDB.</value>
        public string FileName
        {
            get
            {
                this.AssertNotDisposed();
                return this.sourceFile.fileName;
            }
        }

        /// <summary>
        /// Destroys this <see cref="SourceFile"/>
        /// </summary>
        /// <seealso cref="M:System.IDisposable.Dispose()"/>
        public void Dispose()
        {
            if (!this.disposed)
            {
                Marshal.ReleaseComObject(this.sourceFile);
            }

            this.disposed = true;
        }

        private byte[] GetHash()
        {
            IntPtr nativeBuffer = IntPtr.Zero;

            try
            {
                this.sourceFile.get_checksum(0, out uint hashLength, IntPtr.Zero);

                int allocSize = checked((int)hashLength);
                nativeBuffer = Marshal.AllocHGlobal(allocSize);

                this.sourceFile.get_checksum(hashLength, out uint actualHashLength, nativeBuffer);
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
            if (this.disposed)
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
        SHA1,
        SHA256
    }
}
