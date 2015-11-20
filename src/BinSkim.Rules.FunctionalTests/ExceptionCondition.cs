// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
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
