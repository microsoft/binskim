// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.Options
{
    public interface IOption
    {
        string Feature { get; }
        string Name { get; }
        Type Type { get; }
        object DefaultValue { get; }
        bool IsPerLanguage { get; }
    }
}

