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
        public ActionMap<SymbolAnalysisContext, SymbolKind> SymbolActions { get; private set; }

        public Action<CompilationAnalysisContext> CompilationEndActions { get; private set; }

        public RoslynCompilationStartAnalysisContext(Compilation compilation, AnalyzerOptions options, CancellationToken cancellationToken)
            : base(compilation, options, cancellationToken)
        {
            this.SymbolActions = new ActionMap<SymbolAnalysisContext, SymbolKind>();
        }

        /// <summary>
        /// Register a per-compilation symbol action. This pattern is often used by analyzers that inspect some compilation-level
        /// data (such as whether certain analysis-relevant types are, in fact, available due to targeting or other settings)
        /// to drive registration of symbol actions.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="symbolKinds"></param>
        public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, ImmutableArray<SymbolKind> symbolKinds)
        {
            this.SymbolActions.Add(action, symbolKinds);
        }

        /// <summary>
        /// Register a compilation end action. We record and invoke these callbacks after analyzing each assembly, in order
        /// to allow analyzers to perform any relevant clean-up actions.
        /// </summary>
        /// <param name="action"></param>
        public override void RegisterCompilationEndAction(Action<CompilationAnalysisContext> action)
        {
            this.CompilationEndActions += action;
        }

        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) { }
        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action) { }
        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) { }
        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(Action<SyntaxNodeAnalysisContext> action, ImmutableArray<TLanguageKindEnum> syntaxKinds) { }
        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) { }
    }
}
