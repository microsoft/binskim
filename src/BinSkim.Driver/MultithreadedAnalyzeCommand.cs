// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using CommandLine;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif.Writers;

namespace Microsoft.CodeAnalysis.IL
{
    public class MultithreadedAnalyzeCommand : MultithreadedAnalyzeCommandBase<BinaryAnalyzerContext, AnalyzeOptions>
    {
        public static HashSet<string> ValidAnalysisFileExtensions = new HashSet<string>(
            new string[] { ".dll", ".exe", ".sys" }
            );

        /// <summary>
        /// Construct a new <see cref="MultithreadedAnalyzeCommand"/> instance.
        /// </summary>
        /// <param name="telemetry">The Application Insights telemetry instance. May be null.</param>
        public MultithreadedAnalyzeCommand(Sdk.Telemetry telemetry = null)
        {
            this.Telemetry = telemetry;
        }

        public override IEnumerable<Assembly> DefaultPluginAssemblies
        {
            get => new Assembly[] { typeof(MarkImageAsNXCompatible).Assembly };
            set => throw new InvalidOperationException();
        }

        public override void InitializeOutputs(BinaryAnalyzerContext context)
        {
            base.InitializeOutputs(context);

            if (!string.IsNullOrEmpty(context.EnlistmentRootToNormalize))
            {
                var aggregatingLogger = (AggregatingLogger)context.Logger;
                for (int i = 0; i < aggregatingLogger.Loggers.Count; i++)
                {
                    if (aggregatingLogger.Loggers[i] is SarifLogger sarifLogger)
                    {
                        aggregatingLogger.Loggers[i] = new NormalizingSarifLogger(sarifLogger, context.EnlistmentRootToNormalize);
                    }
                }
            }
        }

        public override BinaryAnalyzerContext InitializeGlobalContextFromOptions(AnalyzeOptions options, ref BinaryAnalyzerContext context)
        {
            base.InitializeGlobalContextFromOptions(options, ref context);

            if (this.Telemetry?.TelemetryClient != null)
            {
                // Create an aggregating logger that will combine all loggers into a single logger.
                var aggregatingLogger = new AggregatingLogger();
                if (context.Logger is AggregatingLogger)
                {
                    aggregatingLogger = context.Logger as AggregatingLogger;
                }
                else
                {
                    aggregatingLogger.Loggers.Add(context.Logger);
                }

                var ruleTelemetryLogger = new RuleTelemetryLogger(this.Telemetry.TelemetryClient);
                ruleTelemetryLogger.AnalysisStarted();

                // Combine rule telemetry with any other loggers that may be present.
                aggregatingLogger.Loggers.Add(ruleTelemetryLogger);
                context.Logger = aggregatingLogger;
            }

            // We override the driver framework size default to be as large as
            // possible Binaries and (in particular) their PDBs can be large.
            context.MaxFileSizeInKilobytes = options.MaxFileSizeInKilobytes != null
                ? options.MaxFileSizeInKilobytes.Value
                : long.MaxValue;

            // Update context object based on command-line parameters.
            context.SymbolPath = options.SymbolsPath ?? context.SymbolPath;
            context.IgnorePdbLoadError = options.IgnorePdbLoadError != null ? options.IgnorePdbLoadError.Value : context.IgnorePdbLoadError;
            context.IgnorePELoadError = options.IgnorePELoadError != null ? options.IgnorePELoadError.Value : context.IgnorePELoadError;
            context.IgnoreBinaryAnalysisErrors = options.IgnoreBinaryAnalysisErrors != null ? options.IgnoreBinaryAnalysisErrors.Value : context.IgnoreBinaryAnalysisErrors;

            context.DisableTelemetry = options.DisableTelemetry != null ? options.DisableTelemetry.Value : context.DisableTelemetry;
            context.LocalSymbolDirectories = options.LocalSymbolDirectories ?? context.LocalSymbolDirectories;
            context.TracePdbLoads = options.Trace.Contains(nameof(Traces.PdbLoad));
            context.VerboseErrors = options.Trace.Any();

            // Hidden options for test normalization purposes.
            context.EnlistmentRootToNormalize = options.EnlistmentRoot ?? context.EnlistmentRootToNormalize;

            context.CompilerDataLogger =
                new CompilerDataLogger(context.OutputFilePath,
                                       Sarif.SarifVersion.Current,
                                       context,
                                       this.FileSystem,
                                       this.Telemetry);

            // If the user has hard-coded a non-deterministic file path root to elide from telemetry,
            // we will honor that. If it has not been specified, and if all file target specifiers
            // point to a common directory, then we will use that directory as the path to elide.
            if (string.IsNullOrEmpty(context.CompilerDataLogger.RootPathToElide))
            {
                context.CompilerDataLogger.RootPathToElide =
                    ReturnCommonPathRootFromTargetSpecifiersIfOneExists(context.TargetFileSpecifiers);
            }

            this.globalContext = context;
            return context;
        }

        private BinaryAnalyzerContext globalContext;

        protected override ISet<Skimmer<BinaryAnalyzerContext>> InitializeSkimmers(
            ISet<Skimmer<BinaryAnalyzerContext>> skimmers,
            BinaryAnalyzerContext context)
        {
            skimmers = base.InitializeSkimmers(skimmers, context);

            AnalyzeOptions options = this.currentOptions;
            if (options == null)
            {
                return skimmers;
            }

            Dictionary<string, FailureLevel?> enableRules = ParseRuleSpecifiers(options.EnableRules);
            Dictionary<string, FailureLevel?> runOnlyRules = ParseRuleSpecifiers(options.RunOnlyRules);

            if (enableRules.Count == 0 && runOnlyRules.Count == 0)
            {
                return skimmers;
            }

            if (enableRules.Count > 0 && runOnlyRules.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot specify both --enable-disabled-rules and --run-only-rules. Use one or the other.");
            }

            foreach (Skimmer<BinaryAnalyzerContext> skimmer in skimmers)
            {
                if (runOnlyRules.Count > 0)
                {
                    // --run-only-rules: disable everything, then enable only specified rules.
                    if (runOnlyRules.TryGetValue(skimmer.Id, out FailureLevel? level))
                    {
                        skimmer.DefaultConfiguration.Enabled = true;
                        if (level.HasValue)
                        {
                            skimmer.DefaultConfiguration.Level = level.Value;
                        }
                    }
                    else if (skimmer.DefaultConfiguration.Enabled)
                    {
                        skimmer.DefaultConfiguration.Enabled = false;
                        LogRuleExplicitlyDisabled(context, skimmer);
                    }
                }
                else if (enableRules.TryGetValue(skimmer.Id, out FailureLevel? level))
                {
                    // --enable-disabled-rules: enable the specified rules (useful for rules disabled by default).
                    skimmer.DefaultConfiguration.Enabled = true;
                    if (level.HasValue)
                    {
                        skimmer.DefaultConfiguration.Level = level.Value;
                    }
                }
            }

            // Warn about rule IDs in the specifiers that don't match any loaded skimmer.
            var loadedRuleIds = new HashSet<string>(skimmers.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, FailureLevel?> specifiers = runOnlyRules.Count > 0 ? runOnlyRules : enableRules;

            foreach (string ruleId in specifiers.Keys.Where(id => !loadedRuleIds.Contains(id)))
            {
                context.Logger.LogConfigurationNotification(
                    new Notification
                    {
                        Descriptor = new ReportingDescriptorReference
                        {
                            Id = Warnings.Wrn999_RuleExplicitlyDisabled,
                        },
                        Message = new Message
                        {
                            Text = $"Rule '{ruleId}' was specified on the command line but does not " +
                                   $"match any loaded rule. Verify the rule ID is correct.",
                        },
                        Level = FailureLevel.Warning,
                    });
            }

            return skimmers;
        }

        private static void LogRuleExplicitlyDisabled(BinaryAnalyzerContext context, Skimmer<BinaryAnalyzerContext> skimmer)
        {
            context.Logger.LogConfigurationNotification(
                new Notification
                {
                    Descriptor = new ReportingDescriptorReference
                    {
                        Id = Warnings.Wrn999_RuleExplicitlyDisabled,
                    },
                    Message = new Message
                    {
                        Text = $"Rule '{skimmer.Id}' was explicitly disabled by the user. As result, " +
                               $"this tool run cannot be used for compliance or other auditing processes " +
                               $"that require a comprehensive analysis.",
                    },
                    Level = FailureLevel.Warning,
                });
        }

        /// <summary>
        /// Parses rule specifiers in the format "RuleId" or "RuleId:Level" into a dictionary.
        /// </summary>
        internal static Dictionary<string, FailureLevel?> ParseRuleSpecifiers(IEnumerable<string> specifiers)
        {
            var result = new Dictionary<string, FailureLevel?>(StringComparer.OrdinalIgnoreCase);

            if (specifiers == null)
            {
                return result;
            }

            foreach (string specifier in specifiers)
            {
                if (string.IsNullOrWhiteSpace(specifier))
                {
                    continue;
                }

                string[] parts = specifier.Split(':');
                string ruleId = parts[0].Trim();

                FailureLevel? level = null;
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    if (!Enum.TryParse(parts[1].Trim(), ignoreCase: true, out FailureLevel parsed) ||
                        parsed == FailureLevel.None)
                    {
                        throw new InvalidOperationException(
                            $"Invalid level '{parts[1].Trim()}' for rule '{ruleId}'. " +
                            $"Valid values are: Error, Warning, Note.");
                    }

                    level = parsed;
                }

                result[ruleId] = level;
            }

            return result;
        }

        protected override BinaryAnalyzerContext CreateScanTargetContext(BinaryAnalyzerContext context)
        {
            BinaryAnalyzerContext scanTargetContext = base.CreateScanTargetContext(context);

            scanTargetContext.CompilerDataLogger = context.CompilerDataLogger;
            scanTargetContext.SymbolPath = context.SymbolPath;
            scanTargetContext.IncludeWixBinaries = context.IncludeWixBinaries;
            scanTargetContext.IgnorePdbLoadError = context.IgnorePdbLoadError;
            scanTargetContext.IgnorePELoadError = context.IgnorePELoadError;
            scanTargetContext.IgnoreBinaryAnalysisErrors = context.IgnoreBinaryAnalysisErrors;
            scanTargetContext.LocalSymbolDirectories = context.LocalSymbolDirectories;
            scanTargetContext.TracePdbLoads = context.TracePdbLoads;
            scanTargetContext.VerboseErrors = context.VerboseErrors;

            // Command-line provided policy is now initialized. Update context 
            // based on any possible configuration provided in this way.

            return scanTargetContext;
        }

        protected override void AnalyzeTarget(
            BinaryAnalyzerContext context,
            IEnumerable<Skimmer<BinaryAnalyzerContext>> skimmers,
            ISet<string> disabledSkimmers)
        {
            if (!context.IgnoreBinaryAnalysisErrors)
            {
                base.AnalyzeTarget(context, skimmers, disabledSkimmers);
                return;
            }

            // When --ignoreBinaryAnalysisErrors is set, an exception thrown by a rule
            // does NOT disable that rule for subsequent targets and does NOT
            // set a fatal RuntimeConditions flag (which would produce a non-zero
            // exit code).

            foreach (Skimmer<BinaryAnalyzerContext> skimmer in skimmers)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (disabledSkimmers.Count > 0)
                {
                    lock (disabledSkimmers)
                    {
                        if (disabledSkimmers.Contains(skimmer.Id)) { continue; }
                    }
                }

                context.Rule = skimmer;

                try
                {
                    skimmer.Analyze(context);
                }
                catch (Exception ex)
                {
                    LogRecoverableRuleException(context, ex, "analyzing");

                    context.RuntimeExceptions ??= new List<Exception>();
                    context.RuntimeExceptions.Add(ex);
                }
            }
        }

        protected override IEnumerable<Skimmer<BinaryAnalyzerContext>> DetermineApplicabilityForTarget(
            BinaryAnalyzerContext context,
            IEnumerable<Skimmer<BinaryAnalyzerContext>> skimmers,
            ISet<string> disabledSkimmers)
        {
            if (!context.IgnoreBinaryAnalysisErrors)
            {
                return base.DetermineApplicabilityForTarget(context, skimmers, disabledSkimmers);
            }

            // When --ignoreBinaryAnalysisErrors is set, an exception thrown during
            // applicability evaluation does NOT disable the rule for subsequent
            // targets and does NOT set a fatal RuntimeConditions flag.

            var candidateSkimmers = new List<Skimmer<BinaryAnalyzerContext>>();

            foreach (Skimmer<BinaryAnalyzerContext> skimmer in skimmers)
            {
                if (disabledSkimmers.Count > 0)
                {
                    lock (disabledSkimmers)
                    {
                        if (disabledSkimmers.Contains(skimmer.Id)) { continue; }
                    }
                }

                context.Rule = skimmer;

                try
                {
                    AnalysisApplicability applicability = skimmer.CanAnalyze(context, out string reasonForNotAnalyzing);

                    switch (applicability)
                    {
                        case AnalysisApplicability.NotApplicableToSpecifiedTarget:
                            Notes.LogNotApplicableToSpecifiedTarget(context, reasonForNotAnalyzing);
                            break;

                        case AnalysisApplicability.ApplicableToSpecifiedTarget:
                            candidateSkimmers.Add(skimmer);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogRecoverableRuleException(context, ex, "assessing applicability of");
                }
            }

            return candidateSkimmers;
        }

        private static void LogRecoverableRuleException(
            BinaryAnalyzerContext context,
            Exception exception,
            string phase)
        {
            // Log a warning-level notification for visibility. Unlike the default
            // SDK behavior (Errors.LogUnhandledRuleExceptionAnalyzingTarget), this
            // does NOT set fatal RuntimeConditions and does NOT add the rule to
            // the disabledSkimmers set.
            //
            // By default only the exception type and message are shown. The full
            // stack trace is included when any --trace option is specified.
            bool verbose = context.VerboseErrors;

            string message =
                $"An exception of type '{exception.GetType().Name}' was raised " +
                $"{phase} '{context.CurrentTarget.Uri.GetFileName()}' for check " +
                $"'{context.Rule.Name}' ('{context.Rule.Id}'). The rule remains " +
                $"enabled for subsequent targets. {exception.GetType().Name}: " +
                $"{exception.Message}" +
                (verbose ? $"\n{exception}" : string.Empty);

            context.Logger.LogToolNotification(
                new Notification
                {
                    AssociatedRule = new ReportingDescriptorReference { Id = context.Rule.Id },
                    Level = FailureLevel.Warning,
                    Message = new Message { Text = message },
                },
                context.Rule);
        }

        internal static string ReturnCommonPathRootFromTargetSpecifiersIfOneExists(IEnumerable<string> targetFileSpecifiers)
        {
            Debug.Assert(targetFileSpecifiers != null && targetFileSpecifiers.Any());

            var fileSpecifierDirectories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // Normalizing all 'targetFileSpecifiers', ensuring that they will always end with only one slash.
            foreach (string targetFileSpecifier in targetFileSpecifiers)
            {
                string targetFileDirectory = Path.GetDirectoryName(Path.GetFullPath(targetFileSpecifier));
                targetFileDirectory = targetFileDirectory.EndsWith(@"\")
                    ? targetFileDirectory
                    : targetFileDirectory + @"\";

                fileSpecifierDirectories.Add(targetFileDirectory);
            }

            string smallestPath = fileSpecifierDirectories.First();
            fileSpecifierDirectories.Remove(smallestPath);

            // We don't need to iterate all characters of 'smallestPath'
            // if we don't have anything to compare against.
            if (fileSpecifierDirectories.Count == 0)
            {
                return smallestPath;
            }

            for (int i = 0; i < smallestPath.Length; i++)
            {
                foreach (string fileSpecifierDirectory in fileSpecifierDirectories)
                {
                    if (char.ToLowerInvariant(smallestPath[i]) != char.ToLowerInvariant(fileSpecifierDirectory[i]))
                    {
                        smallestPath = smallestPath.Substring(0, i);
                        break;
                    }
                }
            }

            int smallestPathLength = smallestPath.Length;
            if (smallestPathLength == 0)
            {
                return string.Empty;
            }

            if (smallestPath[smallestPathLength - 1] != '\\')
            {
                // In this case, 'smallestPath' is equal to 'c:\path1\partial-path' (this is an incomplete path).
                // Once we execute, our 'smallestPath' will be transformed into 'c:\path1\'.
                int lastIndex = smallestPath.LastIndexOf('\\');
                smallestPath = smallestPath.Substring(0, lastIndex + 1);
            }

            return smallestPath;
        }

        public override int Run(AnalyzeOptions analyzeOptions)
        {
            Stopwatch stopwatch = null;

            this.currentOptions = analyzeOptions;

            if (analyzeOptions.DisableTelemetry == true)
            {
                this.Telemetry = null;
            }

            if (analyzeOptions.Trace.Where(s => s == nameof(DefaultTraces.ScanTime)).Any())
            {
                stopwatch = Stopwatch.StartNew();
            }

            if (analyzeOptions.TargetFileSpecifiers?.Any() != true)
            {
                throw new ArgumentNullException(nameof(analyzeOptions.TargetFileSpecifiers), "Please specify one or more files, directories, or filter patterns for BinSkim analyze.");
            }

            if (!Environment.GetCommandLineArgs().
                Any(arg => arg.Equals("--sarif-output-version", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-v", StringComparison.OrdinalIgnoreCase)))
            {
                analyzeOptions.SarifOutputVersion = Sarif.SarifVersion.Current;
            }

            if (this.UnitTestOutputVersion != Sarif.SarifVersion.Unknown)
            {
                analyzeOptions.SarifOutputVersion = this.UnitTestOutputVersion;
            }

            if (analyzeOptions.SarifOutputVersion == Sarif.SarifVersion.OneZeroZero)
            {
                throw new InvalidOperationException(
                    "BinSkim no longer supports emitting SARIF 1.0 (an obsolete format). " +
                    "Pass 'Current' on the command-line or omit the '-v|--sarif-output-version' argument entirely.");
            }

            // Type or member is obsolete
#pragma warning disable CS0618
            if (analyzeOptions.ComputeFileHashes)
#pragma warning restore CS0618
            {
                OptionallyEmittedData dataToInsert = analyzeOptions.DataToInsert.ToFlags();
                dataToInsert |= OptionallyEmittedData.Hashes;

                analyzeOptions.DataToInsert = Enum.GetValues(typeof(OptionallyEmittedData)).Cast<OptionallyEmittedData>()
                    .Where(oed => dataToInsert.HasFlag(oed)).ToList();
            }

            int result = 0;

            try
            {
                result = base.Run(analyzeOptions);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                if (stopwatch != null)
                {
                    Console.WriteLine($"Scan time: {stopwatch.Elapsed}");
                }
            }

            // In BinSkim, no rule is ever applicable to every target type. For example,
            // we have checks that are only relevant to either 32-bit or 64-bit binaries.
            // Because of this, the return code bit for RuleNotApplicableToTarget is not
            // interesting (it will always be set).

            return analyzeOptions.RichReturnCode == true
                ? (int)((uint)result & ~(long)RuntimeConditions.RuleNotApplicableToTarget)
                : result;
        }

        internal Sarif.SarifVersion UnitTestOutputVersion { get; set; }

        private Sdk.Telemetry Telemetry { get; set; }

        internal AnalyzeOptions currentOptions;
    }
}
