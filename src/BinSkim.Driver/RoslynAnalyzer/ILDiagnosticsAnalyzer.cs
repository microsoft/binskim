// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.IL
{
    /// <summary>
    /// Proof-of-concept driver to run Roslyn diagnostics analyzer symbol and operation rules against nodes raised from IL/metadata.
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
            return Create((IEnumerable<string>)analyzerFilePaths);
        }

        public static ILDiagnosticsAnalyzer Create(IEnumerable<string> analyzerFilePaths)
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

            var assembly = Assembly.LoadFrom(path);

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
            using (var stream = File.OpenRead(targetPath))
            using (var peReader = new PEReader(stream))
            {
                var metadataReader = peReader.GetMetadataReader();
                var references = GetReferences(targetPath, metadataReader);

                // Create a Roslyn representation of the IL by constructing a MetadataReference against
                // the target path (as if we intended to reference this binary during compilation, instead
                // of analyzing it). Using this mechanism, we can scan types/members contained in the 
                // binary. We cannot currently retrieve IL from method bodies.
                var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
                options.SetMetadataImportOptions(MetadataImportOptions.All);
                var compilation = CSharpCompilation.Create("_", options: options, references: references);
                var target = compilation.GetAssemblyOrModuleSymbol(references[0]);

                // For each analysis target, we create a compilation start context, which may result
                // in symbol action registration. We need to capture and throw these registrations 
                // away for each binary we inspect. 
                var compilationStartContext = new RoslynCompilationStartAnalysisContext(compilation, _options, cancellationToken);
                GlobalRoslynAnalysisContext.CompilationStartActions?.Invoke(compilationStartContext);

                RoslynSymbolVisitor.Visit(
                    target,
                    symbol => AnalyzeSymbol(
                        symbol,
                        compilation,
                        compilationStartContext,
                        reportDiagnostic,
                        peReader,
                        metadataReader,
                        cancellationToken));

                // Having finished analysis, we'll invoke any compilation end actions registered previously.
                // We also discard the per-compilation symbol actions we collected.
                var compilationAnalysisContext = new CompilationAnalysisContext(
                    compilation,
                    _options, 
                    reportDiagnostic,
                    _isSupportedDiagnostic,
                    cancellationToken);

                GlobalRoslynAnalysisContext.CompilationActions?.Invoke(compilationAnalysisContext);
                compilationStartContext.CompilationEndActions?.Invoke(compilationAnalysisContext);
            }
        }

        private void AnalyzeSymbol(
            ISymbol symbol, 
            Compilation compilation,
            RoslynCompilationStartAnalysisContext compilationStartContext,
            Action<Diagnostic> reportDiagnostic, 
            PEReader peReader,
            MetadataReader metadataReader,
            CancellationToken cancellationToken)
        {
            var symbolContext = new SymbolAnalysisContext(
                symbol, 
                compilation, 
                _options,
                reportDiagnostic, 
                _isSupportedDiagnostic, 
                cancellationToken);

            GlobalRoslynAnalysisContext.SymbolActions.Invoke(symbol.Kind, symbolContext);
            compilationStartContext.SymbolActions.Invoke(symbol.Kind, symbolContext);

            var method = symbol as IMethodSymbol;
            if (method != null)
            {
                AnalyzeMethodBody(
                    method,
                    compilation,
                    compilationStartContext,
                    reportDiagnostic,
                    peReader,
                    metadataReader,
                    cancellationToken);
            }
        }

        private void AnalyzeMethodBody(
            IMethodSymbol method, 
            Compilation compilation, 
            RoslynCompilationStartAnalysisContext compilationStartContext,
            Action<Diagnostic> reportDiagnostic,
            PEReader peReader, 
            MetadataReader metadataReader,
            CancellationToken cancellationToken)
        {
            var handle = method.MetadataHandle;

            if (handle.IsNil)
            {
                return; // synthesized symbol (no metadata backing)
            }

            var methodDef = metadataReader.GetMethodDefinition((MethodDefinitionHandle)handle);
            if (methodDef.RelativeVirtualAddress == 0)
            {
                return; // abstract or extern
            }

            var methodBody = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);
            var importer = new ILImporter(compilation, metadataReader, method, methodBody);
            var raisedBody = importer.Import();
            var blocks = ImmutableArray.Create(raisedBody);

            if (raisedBody.IsInvalid)
            {
                // TODO: Proper handling here. Should create built-in diagnostic and report it.
                //Console.WriteLine("warning: Failed to raise {0}: {1}", method, ((InvalidStatement)raisedBody).Exception);
            }

            // For each method, we create a block start context, which may result
            // in operation action registration. We need to capture and throw these
            // registrations for each method we inspect. 
            var blockStartContext = new RoslynOperationBlockStartAnalysisContext(
                blocks,
                method,
                compilation,
                _options,
                cancellationToken);

            GlobalRoslynAnalysisContext.OperationBlockStartActions?.Invoke(blockStartContext);
            compilationStartContext.OperationBlockStartActions?.Invoke(blockStartContext);

            RoslynOperationVisitor.Visit(
                raisedBody,
                operation => AnalyzeOperation(
                    operation,
                    method,
                    compilation,
                    compilationStartContext,
                    blockStartContext,
                    reportDiagnostic,
                    cancellationToken));

            // Having finished operation analysis, we'll invoke any block end actions registered previously.
            // We also discard the per-block operation actions we collected.
            var blockContext = new OperationBlockAnalysisContext(
                blocks,
                method,
                compilation,
                _options,
                reportDiagnostic,
                _isSupportedDiagnostic,
                cancellationToken);

            GlobalRoslynAnalysisContext.OperationBlockActions?.Invoke(blockContext);
            compilationStartContext.OperationBlockActions?.Invoke(blockContext);
            blockStartContext.OperationBlockEndActions?.Invoke(blockContext);
        }

        private void AnalyzeOperation(
            IOperation operation,
            IMethodSymbol containingMethod,
            Compilation compilation,
            RoslynCompilationStartAnalysisContext compilationStartContext,
            RoslynOperationBlockStartAnalysisContext blockStartContext,
            Action<Diagnostic> reportDiagnostic,
            CancellationToken cancellationToken)
        {
            var operationContext = new OperationAnalysisContext(
                operation,
                containingMethod,
                compilation,
                _options,
                reportDiagnostic,
                _isSupportedDiagnostic,
                cancellationToken);

            GlobalRoslynAnalysisContext.OperationActions.Invoke(operation.Kind, operationContext);
            compilationStartContext.OperationActions.Invoke(operation.Kind, operationContext);
            blockStartContext.OperationActions.Invoke(operation.Kind, operationContext);
        }

        // TODO: This policy is incomplete and incorrect -- just barely enough to bootstrap/test 
        //       without having designed and implemented the reference specification, resolution, 
		//       and error handling yet.
        private static List<MetadataReference> GetReferences(string targetPath, MetadataReader metadataReader)
        {
            var frameworkDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var targetDir = Path.GetDirectoryName(targetPath);
            var references = new List<MetadataReference>(metadataReader.AssemblyReferences.Count + 1);
            references.Add(MetadataReference.CreateFromFile(targetPath));

            foreach (var assemblyRefHandle in metadataReader.AssemblyReferences)
            {
                var assemblyRef = metadataReader.GetAssemblyReference(assemblyRefHandle);
                var name = metadataReader.GetString(assemblyRef.Name);

                var frameworkCandidate = Path.Combine(frameworkDir, name + ".dll");
                if (File.Exists(frameworkCandidate))
                {
                    references.Add(MetadataReference.CreateFromFile(frameworkCandidate));
                    continue;
                }

                var targetCandidate = Path.Combine(targetDir, name + ".dll");
                if (File.Exists(targetCandidate))
                {
                    references.Add(MetadataReference.CreateFromFile(targetCandidate));
                }
            }

            return references;
        }
    }
}
