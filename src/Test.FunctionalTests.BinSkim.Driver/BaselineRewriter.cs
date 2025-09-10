// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.Diagnostics.Tracing.Parsers.AspNet;

namespace Microsoft.CodeAnalysis.IL
{
    internal class BaselineRewriter :SarifRewritingVisitor
    {
        private readonly string repoRoot;
        private readonly string normalizedRoot;

        public BaselineRewriter(string repoRoot, string normalizedRoot)
        {
            this.repoRoot = repoRoot;
            this.normalizedRoot = normalizedRoot;
        }
        public override Tool VisitTool(Tool node)
        {
            node.Driver.Version = "15.0.0.0";
            node.Driver.DottedQuadFileVersion = null;
            node.Driver.Organization = null;
            node.Driver.Product = null;
            node.Driver.FullName = null;
            node.Driver.SemanticVersion = null;
            node.Driver.SetProperty("comments", "A security and correctness analyzer for portable executable and MSIL formats.");
            return base.VisitTool(node);
        }

        public override ArtifactLocation VisitArtifactLocation(ArtifactLocation node)
        {
            node.Uri = new Uri(node.Uri.OriginalString.Replace(this.repoRoot, this.normalizedRoot));
            return base.VisitArtifactLocation(node);
        }
    }
}
