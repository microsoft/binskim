// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public abstract class BinaryBase : IBinary
    {
        public BinaryBase(Uri uri)
        {
            this.TargetUri = uri;
        }

        public Uri TargetUri { get; private set; }

        public Exception LoadException { get; protected set; }

        public bool Valid { get; protected set; }

        public virtual void Dispose() { }
    }
}
