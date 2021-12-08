// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
