// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Globalization;
using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;
using Microsoft.CodeAnalysis.Sarif.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    internal class AnalyzeCommand : AnalyzeCommandBase<BinaryAnalyzerContext, AnalyzeOptions>
    {
        public static HashSet<string> ValidAnalysisFileExtensions = new HashSet<string>(
            new string[] { ".dll", ".exe", ".sys" }
            );

        public override IEnumerable<Assembly> DefaultPlugInAssemblies
        {
            get
            {
                return new Assembly[] { typeof(MarkImageAsNXCompatible).Assembly };
            }
            set {  throw new InvalidOperationException(); }
        }

        public override string Prerelease {  get { return VersionConstants.Prerelease; } }


        private RoslynAnalysisContext _globalRoslynAnalysisContext;

        protected override void InitializeFromOptions(AnalyzeOptions analyzeOptions)
        {
            if (!string.IsNullOrEmpty(analyzeOptions.SymbolsPath))
            {
                Pdb.SymbolPath = analyzeOptions.SymbolsPath;
            }
        }

        protected override BinaryAnalyzerContext AnalyzeTarget(AnalyzeOptions options, IEnumerable<ISkimmer<BinaryAnalyzerContext>> skimmers, BinaryAnalyzerContext rootContext, string target, HashSet<string> disabledSkimmers)
        {
            BinaryAnalyzerContext context = base.AnalyzeTarget(options, skimmers, rootContext, target, disabledSkimmers);
            AnalyzeManagedAssembly(target, options.PlugInFilePaths, context);
            return context;
        }

        private void AnalyzeManagedAssembly(string assemblyFilePath, IEnumerable<string> roslynAnalyzerFilePaths, BinaryAnalyzerContext context)
        {
            if (_globalRoslynAnalysisContext == null)
            {
                _globalRoslynAnalysisContext = new RoslynAnalysisContext();

                // We could use the ILDiagnosticsAnalyzer factory method that initializes
                // an object instance from an enumerable collection of analyzer paths. We
                // initialize a context object from each path one-by-one instead, in order
                // to make an attempt to load each specified analyzer. We will therefore
                // collect information on each analyzer that fails to load. We will also 
                // proceed with performing as much analysis as possible. Ultimately, a 
                // single analyzer load failure will return in BinSkim returning a non-zero
                // failure code from the run.
                foreach (string analyzerFilePath in roslynAnalyzerFilePaths)
                {
                    InvokeCatchingRelevantIOExceptions
                    (
                        action: () => { ILDiagnosticsAnalyzer.LoadAnalyzer(analyzerFilePath, _globalRoslynAnalysisContext); },
                        exceptionHandler: (ex) =>
                        {
                            LogExceptionLoadingRoslynAnalyzer(analyzerFilePath, context, ex);
                        }
                    );
                }
            }

            ILDiagnosticsAnalyzer roslynAnalyzer = ILDiagnosticsAnalyzer.Create(_globalRoslynAnalysisContext);

            roslynAnalyzer.Analyze(assemblyFilePath, diagnostic =>
            {
                ResultKind messageKind = diagnostic.Severity.ConvertToMessageKind();
                context.Logger.Log(messageKind, context, diagnostic.GetMessage(CultureInfo.CurrentCulture));
            });
        }
      

        private void LogExceptionLoadingRoslynAnalyzer(string analyzerFilePath, BinaryAnalyzerContext context, Exception ex)
        {
            context.Rule = ErrorDescriptors.InvalidConfiguration;

            // An exception was raised attempting to load Roslyn analyzer '{0}'. Exception information:
            // {1}
            context.Logger.Log(ResultKind.ConfigurationError,
                context,
                string.Format(DriverResources.ExceptionLoadingAnalysisPlugIn,
                    analyzerFilePath,
                    context.PE.LoadException.ToString()));

            RuntimeErrors |= RuntimeConditions.ExceptionLoadingAnalysisPlugIn;
        }
    }
}