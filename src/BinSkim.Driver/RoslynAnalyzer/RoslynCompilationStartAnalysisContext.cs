// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.IL
{
    /// <summary>
    /// Basic analysis context provided to Roslyn analyzers. We use a singleton instance of this class to 
    /// capture all symbol action registration for analyzers. These actions will subsequently be invoked
    /// as we visit the IL of all analysis targets.
    /// </summary>
    internal sealed class RoslynCompilationStartAnalysisContext : CompilationStartAnalysisContext
    {
        public ActionsMap<SymbolAnalysisContext, SymbolKind> SymbolActions { get; set; }

        public RoslynCompilationStartAnalysisContext(ActionsMap<SymbolAnalysisContext, SymbolKind> symbolActions, Compilation compilation, AnalyzerOptions options, CancellationToken cancellationToken)
            : base(compilation, options, cancellationToken)
        {
            SymbolActions = symbolActions ?? new ActionsMap<SymbolAnalysisContext, SymbolKind>(); ;
        }
        public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, ImmutableArray<SymbolKind> symbolKinds)
        {
            SymbolActions.Add(action, symbolKinds);
        }

        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) { }
        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action) { }
        public override void RegisterCompilationEndAction(Action<CompilationAnalysisContext> action) { }
        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) { }
        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(Action<SyntaxNodeAnalysisContext> action, ImmutableArray<TLanguageKindEnum> syntaxKinds) { }
        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) { }
    }
}
