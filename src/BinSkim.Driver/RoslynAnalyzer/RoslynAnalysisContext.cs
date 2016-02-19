// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis.Diagnostics;


namespace Microsoft.CodeAnalysis.IL
{
    /// <summary>
    /// The basic analysis contact provided to all Roslyn diagnostics Initialize methods. This object
    /// collects all compilation start and symbol actions registration. All compilation start callbacks
    /// will subsequently be called before MSIL traversal (in order to collect additional symbol actions
    /// registered at this phase). The aggregated collection of symbol actions will be invoked by
    /// the ILDiagnosticsAnalyzer as it visits the IL for each analysis target.
    /// </summary>
    internal sealed class RoslynAnalysisContext : AnalysisContext
    {
        public ActionMap<SymbolAnalysisContext, SymbolKind> SymbolActions { get; }
        public ActionMap<OperationAnalysisContext, OperationKind> OperationActions { get; }

        public Action<CompilationStartAnalysisContext> CompilationStartActions { get; private set; }
        public Action<CompilationAnalysisContext> CompilationActions { get; private set; }
        public Action<OperationBlockStartAnalysisContext> OperationBlockStartActions { get; private set; }
        public Action<OperationBlockAnalysisContext> OperationBlockActions { get; private set; }

        public RoslynAnalysisContext()
        {
            SymbolActions = new ActionMap<SymbolAnalysisContext, SymbolKind>();
            OperationActions = new ActionMap<OperationAnalysisContext, OperationKind>();
        }

        public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, ImmutableArray<SymbolKind> symbolKinds)
        {
            SymbolActions.Add(action, symbolKinds);
        }

        public override void RegisterOperationAction(Action<OperationAnalysisContext> action, ImmutableArray<OperationKind> operationKinds)
        {
            OperationActions.Add(action, operationKinds);
        }

        public override void RegisterCompilationStartAction(Action<CompilationStartAnalysisContext> action)
        {
            CompilationStartActions += action;
        }

        public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action)
        {
            CompilationActions += action;
        }

        public override void RegisterOperationBlockStartAction(Action<OperationBlockStartAnalysisContext> action)
        {
            OperationBlockStartActions += action;
        }

        public override void RegisterOperationBlockAction(Action<OperationBlockAnalysisContext> action)
        {
            OperationBlockActions += action;
        }

        // These registration actions are currently unsupported or not relevant to an MSIL-driven analysis
        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) { }
        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action) { }
        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) { }
        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(Action<SyntaxNodeAnalysisContext> action, ImmutableArray<TLanguageKindEnum> syntaxKinds) { }
        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) { }
        public override void EnableConcurrentExecution() { }
        public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags analysisMode) { }
    }
}
