// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.BinSkim
{
    public enum IssueKind
    {
        Unknown,
        NoError,
        Error,
        Warning,
        Note,
        NotApplicable,
        Pending,
        InternalError,
        ConfigurationError
    }
}
