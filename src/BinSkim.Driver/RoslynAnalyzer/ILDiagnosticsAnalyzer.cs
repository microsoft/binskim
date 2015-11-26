// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.IL
{
    /// <summary>
    /// Proof-of-concept driver to run Roslyn diagnostics analyzer symbol rules against symbols raised from IL/metadata.
    /// </summary>
    internal sealed class ILDiagnosticsAnalyzer
    {
        private static readonly AnalyzerOptions _options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
        private static readonly Func<Diagnostic, bool> _isSupportedDiagnostic = diagnostic => true;

        private ActionMap<SymbolAnalysisContext, SymbolKind> _perCompilationSymbolsActions;

        public RoslynAnalysisContext GlobalRoslynAnalysisContext { get; set; }

        public void LoadAnalyzer(string path)
        {
            GlobalRoslynAnalysisContext = GlobalRoslynAnalysisContext ?? new RoslynAnalysisContext();

            Assembly assembly = Assembly.LoadFrom(path);

            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type);
                    analyzer.Initialize(GlobalRoslynAnalysisContext);
                }
            }
        }

        public void Analyze(string targetPath, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken = default(CancellationToken))
        {
            var reference = MetadataReference.CreateFromFile(targetPath);
            var compilation = CSharpCompilation.Create("_", references: new[] { reference });
            var target = compilation.GetAssemblyOrModuleSymbol(reference);
            var compilationStartAnalysisContext = new RoslynCompilationStartAnalysisContext(compilation, _options, cancellationToken);

            _perCompilationSymbolsActions = new ActionMap<SymbolAnalysisContext, SymbolKind>();
            compilationStartAnalysisContext.SymbolActions = _perCompilationSymbolsActions;
            GlobalRoslynAnalysisContext.CompilationStartActions?.Invoke(compilationStartAnalysisContext);

            var visitor = new RoslynSymbolVisitor(symbol => Analyze(symbol, compilation, reportDiagnostic, cancellationToken));
            visitor.Visit(target);

            _perCompilationSymbolsActions = null;
        }

        private void Analyze(ISymbol symbol, Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {            
            var symbolContext = new SymbolAnalysisContext(symbol, compilation, _options, reportDiagnostic, _isSupportedDiagnostic, cancellationToken);

            GlobalRoslynAnalysisContext.SymbolActions.Invoke(symbol.Kind, symbolContext);
            _perCompilationSymbolsActions.Invoke(symbol.Kind, symbolContext);
        }
    }
}
