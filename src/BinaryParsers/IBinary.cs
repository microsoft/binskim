using System;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public interface IBinary : IDisposable
    {
        Uri TargetUri { get; }
        Exception LoadException { get; }
        bool Valid { get; }
    }
}
