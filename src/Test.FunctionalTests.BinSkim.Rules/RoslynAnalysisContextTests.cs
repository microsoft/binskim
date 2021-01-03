// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace Microsoft.CodeAnalysis.IL
{
    public class RoslynAnalysisContextTests
    {
        [Fact]
        public void AnalysisContext_Simple()
        {
            var context = new RoslynAnalysisContext();
            int symbolActionInvocationCount = 0;
            int compilationStartInvocationCount = 0;

            // The relevant work
            context.RegisterSymbolAction((c) => symbolActionInvocationCount += 13, SymbolKind.NamedType);
            context.RegisterCompilationStartAction((c) => compilationStartInvocationCount += 7);

            // No-ops
            context.RegisterCodeBlockAction(null);
            context.RegisterCodeBlockStartAction<int>(null);
            context.RegisterSemanticModelAction(null);
            context.RegisterSyntaxNodeAction<int>(null);
            context.RegisterSyntaxTreeAction(null);

            Assert.NotNull(context.SymbolActions);
            Assert.NotNull(context.CompilationStartActions);

            context.CompilationStartActions.Invoke(null);
            Assert.Equal(7, compilationStartInvocationCount);

            context.SymbolActions.Invoke(SymbolKind.NamedType, new SymbolAnalysisContext());
            Assert.Equal(13, symbolActionInvocationCount);
        }
    }
}
