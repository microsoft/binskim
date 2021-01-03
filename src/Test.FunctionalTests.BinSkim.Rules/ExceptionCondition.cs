// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public enum ExceptionCondition
    {
        None,
        AccessingId,
        AccessingName,
        InvokingConstructor,
        InvokingAnalyze,
        InvokingCanAnalyze,
        InvokingInitialize
    }
}
