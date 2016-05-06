// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public interface IBinarySkimmer : ISkimmer<BinaryAnalyzerContext>, IRule
    {
    }
}
