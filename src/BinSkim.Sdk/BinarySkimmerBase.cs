// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif.Sdk;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public abstract class BinarySkimmerBase : SkimmerBase<BinaryAnalyzerContext>, IBinarySkimmer
    {
    }
}
