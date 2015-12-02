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
using Microsoft.CodeAnalysis.Driver;
using Microsoft.CodeAnalysis.IL;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.IL
{
    internal class AnalyzeCommand : DriverCommand<AnalyzeOptions>
    {
        internal const int SUCCESS = 0;
        internal const int FAILURE = 1;

        public static HashSet<string> ValidAnalysisFileExtensions = new HashSet<string>(
            new string[] { ".dll", ".exe", ".sys" }
            );

        private RoslynAnalysisContext _globalRoslynAnalysisContext;

        internal Exception ExecutionException { get; set; }

        internal RuntimeConditions RuntimeErrors { get; set; }

        internal static bool RaiseUnhandledExceptionInDriverCode { get; set; }

        public override int Run(AnalyzeOptions analyzeOptions)
        {
            // 0. Initialize an common logger that drives all outputs. This
            //    object drives logging for console, statistics, etc.
            using (AggregatingLogger logger = InitializeLogger(analyzeOptions))
            {
                try
                {
                    Analyze(analyzeOptions, logger);
                }
                catch (ExitApplicationException<FailureReason> ex)
                {
                    // These exceptions have already been logged
                    ExecutionException = ex;
                    return FAILURE;
                }
                catch (Exception ex)
                {
                    // These exceptions escaped our net and must be logged here
                    BinaryAnalyzerContext context = new BinaryAnalyzerContext();
                    context.Rule = ErrorRules.UnhandledEngineException;
                    context.Logger = logger;
                    LogUnhandledEngineException(ex, context);
                    ExecutionException = ex;
                    return FAILURE;
                }
            }

            return ((RuntimeErrors & RuntimeConditions.Fatal) == RuntimeConditions.NoErrors) ? SUCCESS : FAILURE;
        }

        private void Analyze(AnalyzeOptions analyzeOptions, AggregatingLogger logger)
        {
            // 1. Scrape the analyzer options for settings that alter
            //    behaviors of binary parsers (such as settings for
            //    symbols resolution).
            InitializeParsersFromOptions(analyzeOptions);

            // 2. Produce a comprehensive set of analysis targets 
            HashSet<string> targets = CreateTargetsSet(analyzeOptions);

            // 3. Proactively validate that we can locate and 
            //    access all analysis targets. Helper will return
            //    a list that potentially filters out files which
            //    did not exist, could not be accessed, etc.
            targets = ValidateTargetsExist(logger, targets);

            // 4. Create our policy, which will be shared across
            //    all context objects that are created during analysis
            PropertyBag policy = CreatePolicyFromOptions(analyzeOptions);

            // 5. Create short-lived context object to pass to 
            //    skimmers during initialization. The logger and
            //    policy objects are common to all context instances
            //    and will be passed on again for analysis.
            BinaryAnalyzerContext context = CreateContext(logger, policy);

            // 6. Initialize report file, if configured.
            InitializeOutputFile(analyzeOptions, context, targets);

            // 7. Instantiate skimmers.
            HashSet<IBinarySkimmer> skimmers = CreateSkimmers(logger);

            // 8. Initialize skimmers. Initialize occurs a single time only.
            skimmers = InitializeSkimmers(skimmers, context);

            // 9. Run all PE- and MSIL-based analysis
            Analyze(skimmers, analyzeOptions.RoslynAnalyzerFilePaths, targets, logger, policy);

            // 10. For test purposes, raise an unhandled exception if indicated
            if (RaiseUnhandledExceptionInDriverCode)
            {
                throw new InvalidOperationException(nameof(AnalyzeCommand));
            }
        }

        internal AggregatingLogger InitializeLogger(AnalyzeOptions analyzeOptions)
        {
            var logger = new AggregatingLogger();
            logger.Loggers.Add(new ConsoleLogger(analyzeOptions.Verbose));

            if (analyzeOptions.Statistics)
            {
                logger.Loggers.Add(new StatisticsLogger());
            }

            return logger;
        }

        private void InitializeParsersFromOptions(AnalyzeOptions analyzeOptions)
        {
            if (!string.IsNullOrEmpty(analyzeOptions.SymbolsPath))
            {
                Pdb.SymbolPath = analyzeOptions.SymbolsPath;
            }
        }

        private static HashSet<string> CreateTargetsSet(AnalyzeOptions analyzeOptions)
        {
            HashSet<string> targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string specifier in analyzeOptions.BinaryFileSpecifiers)
            {
                // Currently, we do not filter on any extensions.
                var fileSpecifier = new FileSpecifier(specifier, recurse: analyzeOptions.Recurse, filter: "*");
                foreach (string file in fileSpecifier.Files) { targets.Add(file); }
            }

            return targets;
        }

        private HashSet<string> ValidateTargetsExist(IMessageLogger<BinaryAnalyzerContext> logger, HashSet<string> targets)
        {
            return targets;
        }    

        internal static BinaryAnalyzerContext CreateContext(IMessageLogger<BinaryAnalyzerContext> logger, PropertyBag policy, string filePath = null)
        {
            var context = new BinaryAnalyzerContext();
            context.Logger = logger;
            context.Policy = policy;

            if (filePath != null)
            {
                context.Uri = new Uri(filePath);
            }

            return context;
        }

        private void InitializeOutputFile(AnalyzeOptions analyzeOptions, BinaryAnalyzerContext context, HashSet<string> targets)
        {
            string filePath = analyzeOptions.OutputFilePath;
            AggregatingLogger aggregatingLogger = (AggregatingLogger)context.Logger;

            if (!string.IsNullOrEmpty(filePath))
            {
                InvokeCatchingRelevantIOExceptions
                (
                    () => aggregatingLogger.Loggers.Add(
                            new SarifLogger(
                                analyzeOptions.OutputFilePath,
                                analyzeOptions.Verbose,
                                targets,
                                analyzeOptions.ComputeTargetsHash)),
                    (ex) =>
                    {
                        LogExceptionCreatingLogFile(filePath, context, ex);
                        throw new ExitApplicationException<FailureReason>(DriverResources.UnexpectedApplicationExit, ex)
                        {
                            FailureReason = FailureReason.ExceptionCreatingLogFile
                        };
                    }
                );
            }
        }

        private void InvokeCatchingRelevantIOExceptions(Action action, Action<Exception> exceptionHandler)
        {
            try
            {
                action();
            }
            catch (UnauthorizedAccessException ex)
            {
                exceptionHandler(ex);
            }
            catch (IOException ex)
            {
                exceptionHandler(ex);
            }
        }

        private HashSet<IBinarySkimmer> CreateSkimmers(IMessageLogger<BinaryAnalyzerContext> logger)
        {
            HashSet<IBinarySkimmer> skimmers = null;
            try
            {
                CompositionHost container = CreateCompositionContainer();
                skimmers = new HashSet<IBinarySkimmer>(container.GetExports<IBinarySkimmer>());
            }
            catch (Exception ex)
            {
                RuntimeErrors |= RuntimeConditions.ExceptionInstantiatingSkimmers;
                throw new ExitApplicationException<FailureReason>(DriverResources.UnexpectedApplicationExit, ex)
                {
                    FailureReason = FailureReason.UnhandledExceptionInstantiatingSkimmers
                };
            }
            return skimmers;
        }

        private void Analyze(
            IEnumerable<IBinarySkimmer> skimmers,
            IList<string> roslynAnalyzerFilePaths,
            IEnumerable<string> targets,
            AggregatingLogger logger,
            PropertyBag policy)
        {
            HashSet<string> disabledSkimmers = new HashSet<string>();

            foreach (string target in targets)
            {
                var context = AnalyzeCommand.CreateContext(logger, policy, target);

                if (context.PE.LoadException != null)
                {
                    LogExceptionLoadingTarget(context);
                    continue;
                }
                else if (!context.PE.IsPEFile)
                {
                    LogExceptionInvalidPE(context);
                    continue;
                }

                context = CreateContext(logger, policy, target);

                // Analyzing {0}...
                logger.Log(MessageKind.AnalyzingTarget, context, DriverResources.Analyzing);

                foreach (IBinarySkimmer skimmer in skimmers)
                {
                    if (disabledSkimmers.Contains(skimmer.Id)) { continue; }

                    string reasonForNotAnalyzing = null;
                    context.Rule = skimmer;

                    AnalysisApplicability applicability = AnalysisApplicability.Unknown;

                    try
                    {
                        applicability = skimmer.CanAnalyze(context, out reasonForNotAnalyzing);
                    }
                    catch (Exception ex)
                    {
                        LogUnhandledRuleExceptionAssessingTargetApplicability(disabledSkimmers, context, skimmer, ex);
                        continue;
                    }

                    switch (applicability)
                    {
                        case AnalysisApplicability.NotApplicableToSpecifiedTarget:
                        {
                            // Image '{0}' was not evaluated for check '{1}' as the analysis
                            // is not relevant based on observed binary metadata: {2}.
                            context.Logger.Log(MessageKind.NotApplicable,
                                context,
                                RuleUtilities.BuildTargetNotAnalyzedMessage(
                                    context.PE.FileName,
                                    context.Rule.Name,
                                    reasonForNotAnalyzing));

                            break;
                        }

                        case AnalysisApplicability.NotApplicableToAnyTargetWithoutPolicy:
                        {
                            // Check '{0}' was disabled for this run as the analysis was not 
                            // configured with required policy ({1}). To resolve this, 
                            // configure and provide a policy file on the BinSkim command-line 
                            // using the --policy argument (recommended), or pass 
                            // '--policy default' to invoke built-in settings. Invoke the 
                            // BinSkim.exe 'export' command to produce an initial policy file 
                            // that can be edited if required and passed back into the tool.
                            context.Logger.Log(MessageKind.ConfigurationError, context,
                                RuleUtilities.BuildRuleDisabledDueToMissingPolicyMessage(
                                    context.Rule.Name,
                                    reasonForNotAnalyzing));
                            disabledSkimmers.Add(skimmer.Id);
                            break;
                        }

                        case AnalysisApplicability.ApplicableToSpecifiedTarget:
                        {
                            try
                            {
                                skimmer.Analyze(context);
                            }
                            catch (Exception ex)
                            {
                                LogUnhandledRuleExceptionAnalyzingTarget(disabledSkimmers, context, skimmer, ex);
                            }
                            break;
                        }
                    }
                }

                // Once we've processed all portable executable skimmers for a specific
                // target, we'll proactively let go of the data associated with this 
                // analysis phase. Follow-on analyses (such as the Roslyn integration)
                // shouldn't attempt to rehydrate this data. The context implementation
                // currently raises an exception if there is an attempt to rehydrate a
                // previously nulled PE instance.
                DisposePortableExecutableContextData(context);

                // IsManagedAssembly is computed on intitializing the binary context
                // object and is still valid after disposing the PE data. The Roslyn
                // analysis is driven solely off the binary file path in the context.
                if (context.IsManagedAssembly && roslynAnalyzerFilePaths?.Count > 0)
                {
                    AnalyzeManagedAssembly(context.Uri.LocalPath, roslynAnalyzerFilePaths, context);
                }
            }
        }

        private static void DisposePortableExecutableContextData(BinaryAnalyzerContext context)
        {
            context.DisposePortableExecutableData();
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
                MessageKind messageKind = diagnostic.Severity.ConvertToMessageKind();
                context.Logger.Log(messageKind, context, diagnostic.GetMessage(CultureInfo.CurrentCulture));
            });
        }

        private void LogUnhandledEngineException(Exception ex, BinaryAnalyzerContext context)
        {
            // An unhandled exception was raised during analysis:
            // {1}
            context.Logger.Log(MessageKind.InternalError,
                context,
                string.Format(DriverResources.UnhandledEngineException,
                    ex.ToString()));

            RuntimeErrors |= RuntimeConditions.ExceptionInEngine;
        }


        private void LogExceptionLoadingRoslynAnalyzer(string analyzerFilePath, BinaryAnalyzerContext context, Exception ex)
        {
            context.Rule = ErrorRules.InvalidConfiguration;

            // An exception was raised attempting to load Roslyn analyzer '{0}'. Exception information:
            // {1}
            context.Logger.Log(MessageKind.ConfigurationError,
                context,
                string.Format(DriverResources.UnhandledExceptionLoadingRoslynAnalyzer,
                    analyzerFilePath,
                    context.PE.LoadException.ToString()));

            RuntimeErrors |= RuntimeConditions.ExceptionLoadingRoslynAnalyzer;
        }

        private void LogExceptionInvalidPE(BinaryAnalyzerContext context)
        {
            context.Rule = ErrorRules.InvalidPE;

            // Image '{0}' was not analyzed as the it does not
            // appear to be a valid portable executable.
            context.Logger.Log(MessageKind.NotApplicable,
                context,
                string.Format(
                    SdkResources.TargetNotAnalyzed_NotAPortableExecutable,
                    context.Uri.LocalPath));

            DisposePortableExecutableContextData(context);
            RuntimeErrors |= RuntimeConditions.OneOrMoreTargetsNotPortableExecutables;
        }

        private void LogExceptionLoadingTarget(BinaryAnalyzerContext context)
        {
            context.Rule = ErrorRules.InvalidConfiguration;

            // An exception was raised attempting to load analysis target '{0}'. Exception information:
            // {1}
            context.Logger.Log(MessageKind.ConfigurationError,
                context,
                string.Format(DriverResources.ExceptionLoadingAnalysisTarget,
                    context.PE.FileName,
                    context.PE.LoadException.ToString()));

            DisposePortableExecutableContextData(context);
            RuntimeErrors |= RuntimeConditions.ExceptionLoadingTargetFile;
        }

        private void LogExceptionCreatingLogFile(string fileName, BinaryAnalyzerContext context, Exception ex)
        {
            // An exception was raised attempting to create output file '{0}'. Exception information:
            // {1}
            context.Rule = ErrorRules.InvalidConfiguration;
            context.Logger.Log(MessageKind.ConfigurationError,
                context,
                string.Format(DriverResources.ExceptionCreatingLogFile,
                    fileName,
                    ex.ToString()));

            RuntimeErrors |= RuntimeConditions.ExceptionCreatingLogfile;
        }

        private void LogUnhandledExceptionInitializingRule(BinaryAnalyzerContext context, IBinarySkimmer skimmer, Exception ex)
        {
            string ruleName = context.Rule.Name;
            // An unhandled exception was encountered initializing check '{0}', which 
            // has been disabled for the remainder of the analysis. Exception information:
            // {1}
            context.Rule = ErrorRules.UnhandledRuleException;
            context.Logger.Log(MessageKind.InternalError,
                context,
                string.Format(DriverResources.UnhandledExceptionInitializingRule,
                    ruleName,
                    ex.ToString()));

            RuntimeErrors |= RuntimeConditions.ExceptionInSkimmerInitialize;
        }

        private void LogUnhandledRuleExceptionAssessingTargetApplicability(HashSet<string> disabledSkimmers, BinaryAnalyzerContext context, IBinarySkimmer skimmer, Exception ex)
        {
            string ruleName = context.Rule.Name;
            // An unhandled exception was raised attempting to determine whether '{0}' 
            // is a valid analysis target for check '{1}' (which has been disabled 
            // for the remainder of the analysis). The exception may have resulted 
            // from a problem related to parsing image metadata and not specific to 
            // the rule, however. Exception information:
            // {2}
            context.Rule = ErrorRules.UnhandledRuleException;
            context.Logger.Log(MessageKind.InternalError,
                context,
                string.Format(DriverResources.UnhandledExceptionCheckingApplicability,
                    context.Uri.LocalPath,
                    ruleName,
                    ex.ToString()));

            if (disabledSkimmers != null) { disabledSkimmers.Add(skimmer.Id); }

            RuntimeErrors |= RuntimeConditions.ExceptionRaisedInSkimmerCanAnalyze;
        }

        private void LogUnhandledRuleExceptionAnalyzingTarget(HashSet<string> disabledSkimmers, BinaryAnalyzerContext context, IBinarySkimmer skimmer, Exception ex)
        {
            string ruleName = context.Rule.Name;
            // An unhandled exception was encountered analyzing '{0}' for check '{1}', 
            // which has been disabled for the remainder of the analysis.The 
            // exception may have resulted from a problem related to parsing 
            // image metadata and not specific to the rule, however.
            // Exception information:
            // {2}
            context.Rule = ErrorRules.UnhandledRuleException;
            context.Logger.Log(MessageKind.InternalError,
                context,
                string.Format(DriverResources.UnhandledRuleExceptionAnalyzingTarget,
                    context.Uri.LocalPath,
                    ruleName,
                    ex.ToString()));

            if (disabledSkimmers != null) { disabledSkimmers.Add(skimmer.Id); }

            RuntimeErrors |= RuntimeConditions.ExceptionInSkimmerAnalyze;
        }

        internal HashSet<IBinarySkimmer> InitializeSkimmers(HashSet<IBinarySkimmer> skimmers, BinaryAnalyzerContext context)
        {
            HashSet<IBinarySkimmer> disabledSkimmers = new HashSet<IBinarySkimmer>();

            // ONE-TIME initialization of skimmers. Do not call 
            // Initialize more than once per skimmer instantiation
            foreach (IBinarySkimmer skimmer in skimmers)
            {
                try
                {
                    context.Rule = skimmer;
                    skimmer.Initialize(context);
                }
                catch (Exception ex)
                {
                    RuntimeErrors |= RuntimeConditions.ExceptionInSkimmerInitialize;
                    LogUnhandledExceptionInitializingRule(context, skimmer, ex);
                    disabledSkimmers.Add(skimmer);
                }
            }

            foreach (IBinarySkimmer disabledSkimmer in disabledSkimmers)
            {
                skimmers.Remove(disabledSkimmer);
            }

            return skimmers;
        }


        private static PropertyBag CreatePolicyFromOptions(AnalyzeOptions analyzeOptions)
        {
            PropertyBag policy = null;
            string policyFilePath = analyzeOptions.PolicyFilePath;

            if (!string.IsNullOrEmpty(policyFilePath))
            {
                policy = new PropertyBag();
                if (!policyFilePath.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    policy.LoadFrom(policyFilePath);
                }
            }

            return policy;
        }

        private static IEnumerable<Assembly> s_fallbackAssemblies = new Assembly[]
            { typeof(MarkImageAsNXCompatible).Assembly };

        // This data is replaced during unit testing in order drive negative tests, etc. 
        internal static IEnumerable<Assembly> DefaultRuleAssemblies = null;

        private static CompositionHost CreateCompositionContainer(IEnumerable<Assembly> assemblies = null)
        {
            ConventionBuilder conventions = GetConventions();

            // In the event we receive no explicit assemblies, we load the default rules only
            assemblies = assemblies ?? DefaultRuleAssemblies ?? s_fallbackAssemblies;

            return new ContainerConfiguration()
                .WithAssemblies(assemblies, conventions)
                .CreateContainer();
        }

        private static ConventionBuilder GetConventions()
        {
            var conventions = new ConventionBuilder();

            conventions.ForTypesDerivedFrom<IBinarySkimmer>()
                .Export<IBinarySkimmer>();

            return conventions;
        }
    }
}