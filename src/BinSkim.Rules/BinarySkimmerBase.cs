// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;
using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public abstract class BinarySkimmerBase : SkimmerBase<BinaryAnalyzerContext>, IBinarySkimmer
    {
    }
}
