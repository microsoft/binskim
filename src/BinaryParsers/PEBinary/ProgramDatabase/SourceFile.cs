// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;

using Dia2Lib;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>A source file record in a PDB.</summary>
    /// <seealso cref="T:System.IDisposable"/>
    public sealed class SourceFile : IDisposable
    {
        private readonly Pdb parentPdb;
        private readonly IDiaSourceFile sourceFile;
        private readonly Lazy<byte[]> hashBytes;
        private bool disposed;

        /// <summary>
        /// Constructs a wrapper object around an <see cref="IDiaSourceFile"/> instance.
        /// </summary>
        /// <param name="parentPdb">The PDB file that this instance belongs to.</param>
        /// <param name="source">The COM RCW for the IDiaSourceFile. This instance takes ownership of the
        /// RCW.</param>
        public SourceFile(Pdb parentPdb, IDiaSourceFile source)
        {
            this.hashBytes = new Lazy<byte[]>(this.GetHash);
            this.parentPdb = parentPdb;
            this.sourceFile = source;
            this.FindInjectedInformation();
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
        public HashType HashType { get; set; } = HashType.None;

        /// <summary>
        /// In the case of managed languages, the file information such as language and hash are located in an InjectedSourceRecord.
        /// </summary>
        private void FindInjectedInformation()
        {
            this.AssertNotDisposed();
            this.HashType = (HashType)this.sourceFile.checksumType;
            if (this.HashType == HashType.None)
            {
                // Could be an injected file and thus need to go farther.
                IDiaInjectedSource injected = this.parentPdb.InjectedSources(this.FileName).FirstOrDefault();
                if (injected != null)
                {
                    this.GetSourceDataHash(injected);
                }
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
            uint maxHashLength = 256; // With buffer for future. MD5: 16 bytes, SHA-1: 20 bytes, SHA-256: 32 bytes, SHA-512: 64 bytes.
            byte[] checksum = new byte[maxHashLength];
            this.sourceFile.get_checksum(maxHashLength, out uint hashLength, out checksum[0]);
            this.sourceFile.get_checksum(hashLength, out uint actualHashLength, out checksum[0]);

            if (actualHashLength != hashLength)
            {
                throw new InvalidOperationException("Inconsistent hash lengths returned from IDiaSourceFile::get_checksum.");
            }

            return checksum.Take((int)actualHashLength).ToArray();
        }

        private void AssertNotDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("SourceFile");
            }
        }

        private void GetSourceDataHash(IDiaInjectedSource injectedSource)
        {
            SourceFormat header = default;

            int headerSize = Marshal.SizeOf<SourceFormat>();
            uint size = (uint)headerSize;
            uint count;

            // Initialize unmanaged memory to hold the struct.
            IntPtr p = Marshal.AllocHGlobal(headerSize);

            try
            {
                unsafe
                {
                    byte* bp = (byte*)p;
                    injectedSource.get_source(size, out count, out *bp);
                }

                header = Marshal.PtrToStructure<SourceFormat>(p);
            }
            finally
            {
                // Free the unmanaged memory.
                Marshal.FreeHGlobal(p);
            }

            if (header.AlgorithmId == Constant.MD5Guid)
            {
                this.HashType = HashType.MD5;
            }
            else if (header.AlgorithmId == Constant.Sha1Guid)
            {
                this.HashType = HashType.SHA1;
            }
            else if (header.AlgorithmId == Constant.Sha256Guid)
            {
                this.HashType = HashType.SHA256;
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
