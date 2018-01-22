using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public abstract class BinaryBase : IBinary
    {
        public BinaryBase(Uri uri)
        {
            TargetUri = uri;
        }

        public Uri TargetUri { get; private set; }

        public Exception LoadException { get; protected set; }
        
        public bool Valid { get; protected set; }

        public virtual void Dispose() { }
    }
}
