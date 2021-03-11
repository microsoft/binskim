// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public enum ValidationState
    {
        Ignore = 0,
        Pass,
        Warning,
        Error
    }
}
