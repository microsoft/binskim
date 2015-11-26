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
        private readonly RoslynAnalysisContext _roslynAnalysisContext = new RoslynAnalysisContext();
        private static readonly AnalyzerOptions _options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
        private static readonly Func<Diagnostic, bool> _isSupportedDiagnostic = diagnostic => true;

        public void LoadAnalyzer(string path)
        {
            Assembly assembly = Assembly.LoadFrom(path);

            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type);
                    analyzer.Initialize(_roslynAnalysisContext);
                }
            }
        }

        public void Analyze(string targetPath, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken = default(CancellationToken))
        {
            var reference = MetadataReference.CreateFromFile(targetPath);
            var compilation = CSharpCompilation.Create("_", references: new[] { reference });
            var target = compilation.GetAssemblyOrModuleSymbol(reference);
            var compilationContext = new RoslynCompilationStartAnalysisContext(_roslynAnalysisContext.SymbolActions, compilation, _options, cancellationToken);

            _roslynAnalysisContext.CompilationStartActions?.Invoke(compilationContext);

            var visitor = new RoslynSymbolVisitor(symbol => Analyze(symbol, compilation, reportDiagnostic, cancellationToken));
            visitor.Visit(target);
        }

        private void Analyze(ISymbol symbol, Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {            
            var symbolContext = new SymbolAnalysisContext(symbol, compilation, _options, reportDiagnostic, _isSupportedDiagnostic, cancellationToken);
            _roslynAnalysisContext.SymbolActions.Invoke(symbol.Kind, symbolContext);
        }
    }
}
