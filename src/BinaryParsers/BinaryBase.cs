// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public abstract class BinaryBase : IBinary
    {
        private string sha256Hash;

        public BinaryBase(Uri uri)
        {
            this.TargetUri = uri;
        }

        public Uri TargetUri { get; private set; }

        public Exception LoadException { get; protected set; }

        public bool Valid { get; protected set; }

        /// <summary>
        /// Gets the SHA-256 hash of the binary file. Computed once and cached.
        /// </summary>
        public virtual string SHA256Hash
        {
            get
            {
                if (this.sha256Hash != null)
                {
                    return this.sha256Hash;
                }

                try
                {
                    byte[] hash = SHA256.HashData(File.ReadAllBytes(this.TargetUri.LocalPath));
                    this.sha256Hash = BitConverter.ToString(hash).Replace("-", string.Empty);
                }
                catch
                {
                    this.sha256Hash = string.Empty;
                }

                return this.sha256Hash;
            }
        }

        public virtual void Dispose() { }
    }
}
