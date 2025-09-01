// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL
{
    internal class BaselineRewriter :SarifRewritingVisitor
    {
        public override Tool VisitTool(Tool node)
        {
            node.Driver.Version = "15.0.0.0";
            node.Driver.DottedQuadFileVersion = null;
            node.Driver.SetProperty<string>("comments", "A security and correctness analyzer for portable executable and MSIL formats.");
            return base.VisitTool(node);
        }
    }
}
