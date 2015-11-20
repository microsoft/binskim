// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinSkim.Sdk
{
    public interface IMessageLogger<T>
    {
        void Log(MessageKind messageKind, T context, string message);
    }
}
