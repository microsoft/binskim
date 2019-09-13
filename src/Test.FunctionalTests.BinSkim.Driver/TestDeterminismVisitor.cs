// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.Sarif;

namespace Test.FunctionalTests.BinSkim.Driver
{
    public class TestDeterminismVisitor : SarifRewritingVisitor
    {
        public override Invocation VisitInvocation(Invocation node)
        {
            node.EndTimeUtc = new DateTime();
            node.StartTimeUtc = new DateTime();
            return base.VisitInvocation(node);
        }

        public override ToolComponent VisitToolComponent(ToolComponent node)
        {
            node.DottedQuadFileVersion = null;
            return base.VisitToolComponent(node);
        }
    }
}
