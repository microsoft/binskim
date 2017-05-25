// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

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


        protected override void InitializeConfiguration(AnalyzeOptions analyzeOptions, BinaryAnalyzerContext context)
        {
            base.InitializeConfiguration(analyzeOptions, context);

            if (!string.IsNullOrEmpty(analyzeOptions.SymbolsPath))
            {
                Pdb.SymbolPath = analyzeOptions.SymbolsPath;
            }
            _plugInFilePaths = analyzeOptions.PluginFilePaths;
        }

        protected override void AnalyzeTarget(IEnumerable<ISkimmer<BinaryAnalyzerContext>> skimmers, BinaryAnalyzerContext context, HashSet<string> disabledSkimmers)
        {
            base.AnalyzeTarget(skimmers, context, disabledSkimmers);

            if (context.PE.IsManaged && !context.PE.IsManagedResourceOnly)
            {
                AnalyzeManagedAssembly(context.TargetUri.LocalPath, _plugInFilePaths, context);
            }
        }

        public override int Run(AnalyzeOptions analyzeOptions)
        {
            int result = base.Run(analyzeOptions);

            // In BinSkim, no rule is ever applicable to every target type. For example,
            // we have checks that are only relevant to either 32-bit or 64-bit binaries.
            // Because of this, the return code bit for RuleNotApplicableToTarget is not
            // interesting (it will always be set). 
            return analyzeOptions.RichReturnCode 
                ? (int)((uint)result & (uint)~RuntimeConditions.RuleNotApplicableToTarget) 
                : result;
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
                            Errors.LogExceptionLoadingPlugin(analyzerFilePath, context, ex);
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
                result.Level = diagnostic.Severity.ConvertToResultLevel();
                result.Message = diagnostic.GetMessage();

                if (diagnostic.IsSuppressed)
                {
                    result.SuppressionStates = SuppressionStates.SuppressedInSource;
                }

                result.SetProperty("Severity", diagnostic.Severity.ToString());
                result.SetProperty("IsWarningAsError", diagnostic.IsWarningAsError.ToString());
                result.SetProperty("WarningLevel", diagnostic.WarningLevel.ToString());

                foreach (string key in diagnostic.Properties.Keys)
                {
                    string value;
                    if (result.TryGetProperty(key, out value))
                    {
                        // If the properties bag recapitulates one of the values set
                        // previously, we'll retain the already set value
                        continue;
                    }
                    result.SetProperty(key, diagnostic.Properties[key]);
                }

                result.Locations = new List<Sarif.Location>();

                // 1. Record the assembly under analysis
                PhysicalLocation analysisTarget = new PhysicalLocation()
                {
                    Uri = new Uri(assemblyFilePath)
                };

                // 2. Record the actual location associated with the result
                var region = diagnostic.Location.ConvertToRegion();
                string filePath;
                PhysicalLocation resultFile = null;

                if (diagnostic.Location != Location.None)
                {
                    filePath = diagnostic.Location.GetLineSpan().Path;

                    resultFile = new PhysicalLocation
                    {
                        Uri = new Uri(filePath),
                        Region = region
                    };
                }

                result.Locations.Add(new Sarif.Location()
                {
                    AnalysisTarget = analysisTarget,
                    ResultFile = resultFile,
                });
                

                // 3. If present, emit additional locations associated with diagnostic.
                //    According to docs, these locations typically reference related
                //    locations (i.e., they are not locations that specify other 
                //    occurrences of a problem).

                if (diagnostic.AdditionalLocations != null && diagnostic.AdditionalLocations.Count > 0)
                {
                    result.RelatedLocations = new List<AnnotatedCodeLocation>();

                    foreach(Location location in diagnostic.AdditionalLocations)
                    {
                        filePath = location.GetLineSpan().Path;
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

                IRule rule = diagnostic.ConvertToRuleDescriptor();
                context.Logger.Log(null, result);
            });
        }    
    }
}