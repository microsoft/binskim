// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace Microsoft.CodeAnalysis.IL
{
    public class RoslynCompilationStartAnalysisContextTests
    {
        [Fact]
        public void CompilationStartAnalysisContext_Simple()
        {
            var context = new RoslynCompilationStartAnalysisContext(null, null, CancellationToken.None);
            int invocationCount = 0;

            // The relevant work
            context.RegisterSymbolAction((c) => invocationCount++, SymbolKind.NamedType);

            // No-ops
            context.RegisterCodeBlockAction(null);
            context.RegisterCodeBlockStartAction<int>(null);
            context.RegisterCompilationEndAction(null);
            context.RegisterSemanticModelAction(null);
            context.RegisterSyntaxNodeAction<int>(null);
            context.RegisterSyntaxTreeAction(null);

            Assert.NotNull(context.SymbolActions);

            context.SymbolActions.Invoke(SymbolKind.NamedType, new SymbolAnalysisContext());

            Assert.Equal(1, invocationCount);
        }
    }
}
