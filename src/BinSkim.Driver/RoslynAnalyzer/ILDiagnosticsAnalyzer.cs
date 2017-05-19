// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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

        private ILDiagnosticsAnalyzer(RoslynAnalysisContext analysisContext)
        {
            GlobalRoslynAnalysisContext = analysisContext;
        }

        public RoslynAnalysisContext GlobalRoslynAnalysisContext { get; private set; }

        public static ILDiagnosticsAnalyzer Create(params string[] analyzerFilePaths)
        {
            var analysisContext = new RoslynAnalysisContext();

            foreach(string analyzerFilePath in analyzerFilePaths)
            {
                LoadAnalyzer(analyzerFilePath, analysisContext);
            }

            return Create(analysisContext);
        }

        public static ILDiagnosticsAnalyzer Create(RoslynAnalysisContext analysisContext)
        {
            return new ILDiagnosticsAnalyzer(analysisContext);
        }

        public static RoslynAnalysisContext LoadAnalyzer(string path, RoslynAnalysisContext analysisContext = null)
        {
            analysisContext = analysisContext ?? new RoslynAnalysisContext();

            Assembly assembly = Assembly.LoadFrom(path);

            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type);
                    analyzer.Initialize(analysisContext);
                }
            }
            return analysisContext;
        }

        public void Analyze(
            string targetPath, 
            Action<Diagnostic> reportDiagnostic, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Create a Roslyn representation of the IL by constructing a MetadataReference against
            // the target path (as if we intended to reference this binary during compilation, instead
            // of analyzing it). Using this mechanism, we can scan types/members contained in the 
            // binary. We cannot currently retrieve IL from method bodies.
            var reference = MetadataReference.CreateFromFile(targetPath);
            var compilation = CSharpCompilation.Create("_", references: new[] { reference });
            var target = compilation.GetAssemblyOrModuleSymbol(reference);

            // For each analysis target, we create a compilation start context, which may result
            // in symbol action registration. We need to capture and throw these registrations 
            // away for each binary we inspect. 
            var compilationStartAnalysisContext = new RoslynCompilationStartAnalysisContext(compilation, _options, cancellationToken);

            GlobalRoslynAnalysisContext.CompilationStartActions?.Invoke(compilationStartAnalysisContext);

            var visitor = new RoslynSymbolVisitor(symbol => Analyze(
                symbol,
                compilation,
                compilationStartAnalysisContext.SymbolActions,
                reportDiagnostic, 
                cancellationToken));

            visitor.Visit(target);

            // Having finished analysis, we'll invoke any compilation end actions registered previously.
            // We also discard the per-compilation symbol actions we collected.
            var compilationAnalysisContext = new CompilationAnalysisContext(compilation, _options, reportDiagnostic, _isSupportedDiagnostic, cancellationToken);

            GlobalRoslynAnalysisContext.CompilationActions?.Invoke(compilationAnalysisContext);
            compilationStartAnalysisContext.CompilationEndActions?.Invoke(compilationAnalysisContext);
        }

        private void Analyze(
            ISymbol symbol, 
            Compilation compilation,
            ActionMap<SymbolAnalysisContext, SymbolKind> perCompilationSymbolActions,
            Action<Diagnostic> reportDiagnostic, 
            CancellationToken cancellationToken)
        {            
            var symbolContext = new SymbolAnalysisContext(symbol, compilation, _options, reportDiagnostic, _isSupportedDiagnostic, cancellationToken);

            GlobalRoslynAnalysisContext.SymbolActions.Invoke(symbol.Kind, symbolContext);
            perCompilationSymbolActions.Invoke(symbol.Kind, symbolContext);
        }
    }
}
