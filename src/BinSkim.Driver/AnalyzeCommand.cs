// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;
using Microsoft.CodeAnalysis.Sarif.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;

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
        private IEnumerable<string> _plugInFilePaths;


        public override void ConfigureFromOptions(BinaryAnalyzerContext context, AnalyzeOptions analyzeOptions)
        {
            base.ConfigureFromOptions(context, analyzeOptions);

            if (!string.IsNullOrEmpty(analyzeOptions.SymbolsPath))
            {
                Pdb.SymbolPath = analyzeOptions.SymbolsPath;
            }
            _plugInFilePaths = analyzeOptions.PlugInFilePaths;
        }

        protected override void AnalyzeTarget(IEnumerable<ISkimmer<BinaryAnalyzerContext>> skimmers, BinaryAnalyzerContext context, HashSet<string> disabledSkimmers)
        {
            base.AnalyzeTarget(skimmers, context, disabledSkimmers);

            if (context.PE.IsManaged && !context.PE.IsManagedResourceOnly)
            {
                AnalyzeManagedAssembly(context.TargetUri.LocalPath, _plugInFilePaths, context);
            }
        }

        private void AnalyzeManagedAssembly(string assemblyFilePath, IEnumerable<string> roslynAnalyzerFilePaths, BinaryAnalyzerContext context)
        {
            if (roslynAnalyzerFilePaths == null)
            {
                return;
            }

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
                            Errors.LogExceptionLoadingPlugIn(analyzerFilePath, context, ex);
                        }
                    );
                }
            }

            Debug.Assert(context.MimeType == Sarif.Writers.MimeType.Binary);

            ILDiagnosticsAnalyzer roslynAnalyzer = ILDiagnosticsAnalyzer.Create(_globalRoslynAnalysisContext);
            roslynAnalyzer.Analyze(assemblyFilePath, diagnostic =>
            {
                // 0. Populate various members
                var result = new Result();
                result.Kind = diagnostic.Severity.ConvertToMessageKind();
                result.FullMessage = diagnostic.GetMessage();

                // For Roslyn diagnostics, suppression information is always available (i.e., it 
                // is not contingent on compilation with specific #define such as CODE_ANLAYSIS).
                // As a result, we always populate IsSuppressedInSource with this information.
                result.IsSuppressedInSource = diagnostic.IsSuppressed;

                result.Properties = new Dictionary<string, string>();
                result.Properties["Severity"] = diagnostic.Severity.ToString();
                result.Properties["IsWarningAsError"] = diagnostic.IsWarningAsError.ToString();
                result.Properties["WarningLevel"] = diagnostic.WarningLevel.ToString();

                foreach (string key in diagnostic.Properties.Keys)
                {
                    result.Properties[key] = diagnostic.Properties[key];
                }

                // 1. Record the assembly under analysis
                result.Locations = new[] {
                new Sarif.Location {
                    AnalysisTarget = new PhysicalLocation
                        {
                            Uri = new Uri(assemblyFilePath),
                        }
                } };

                // 2. Record the actual location associated with the result
                var region = diagnostic.Location.ConvertToRegion();
                string filePath;

                if (diagnostic.Location != Location.None)
                {
                    filePath = diagnostic.Location.GetLineSpan().Path;
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        result.Locations[0].ResultFile =
                            new PhysicalLocation
                            {
                                Uri = new Uri(filePath),
                                Region = region
                            };
                    }
                }

                // 3. If present, emit additional locations associated with diagnostic.
                //    According to docs, these locations typically reference related
                //    locations (i.e., they are not locations that specify other 
                //    occurrences of a problem).

                if (diagnostic.AdditionalLocations != null && diagnostic.AdditionalLocations.Count > 0)
                {
                    result.RelatedLocations = new List<AnnotatedCodeLocation>(diagnostic.AdditionalLocations.Count);

                    foreach(Location location in diagnostic.AdditionalLocations)
                    {
                        filePath = location.GetLineSpan().Path;
                        if (string.IsNullOrEmpty(filePath))
                        {
                            continue;
                        }

                        region = location.ConvertToRegion();

                        result.RelatedLocations.Add(new AnnotatedCodeLocation
                        {
                            Message = "Additional location",
                            PhysicalLocation = new PhysicalLocation
                                {
                                    Uri = new Uri(filePath),
                                    Region = region
                                }
                        });
                    }
                }
                context.Logger.Log(diagnostic.ConvertToRuleDescriptor(), result);
            });
        }    
    }
}