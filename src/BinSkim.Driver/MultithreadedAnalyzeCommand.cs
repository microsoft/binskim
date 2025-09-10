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

        public override void InitializeOutputs(BinaryAnalyzerContext globalContext)
        {
            base.InitializeOutputs(globalContext);

            if (globalContext.NormalizeOutputForComparison)
            {
                var aggregatingLogger = (AggregatingLogger)globalContext.Logger;
                for (int i = 0; i< aggregatingLogger.Loggers.Count; i++)
                {
                    if (aggregatingLogger.Loggers[i] is SarifLogger sarifLogger)
                    {
                        aggregatingLogger.Loggers[i] = new NormalizingSarifLogger(sarifLogger, globalContext.EnlistmentRoot);
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

            context.DisableTelemetry = options.DisableTelemetry != null ? options.DisableTelemetry.Value : context.DisableTelemetry;
            context.LocalSymbolDirectories = options.LocalSymbolDirectories ?? context.LocalSymbolDirectories;
            context.TracePdbLoads = options.Trace.Contains(nameof(Traces.PdbLoad));

            // Hidden options for test normalization purposes.
            context.EnlistmentRoot = options.EnlistmentRoot ?? context.EnlistmentRoot;
            context.NormalizeOutputForComparison = options.NormalizeOutputForComparison != null ? options.NormalizeOutputForComparison.Value : context.NormalizeOutputForComparison;

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

        protected override BinaryAnalyzerContext CreateScanTargetContext(BinaryAnalyzerContext context)
        {
            BinaryAnalyzerContext scanTargetContext = base.CreateScanTargetContext(context);

            scanTargetContext.CompilerDataLogger = context.CompilerDataLogger;
            scanTargetContext.SymbolPath = context.SymbolPath;
            scanTargetContext.IncludeWixBinaries = context.IncludeWixBinaries;
            scanTargetContext.IgnorePdbLoadError = context.IgnorePdbLoadError;
            scanTargetContext.IgnorePELoadError = context.IgnorePELoadError;
            scanTargetContext.LocalSymbolDirectories = context.LocalSymbolDirectories;
            scanTargetContext.TracePdbLoads = context.TracePdbLoads;

            // Command-line provided policy is now initialized. Update context 
            // based on any possible configuration provided in this way.

            return scanTargetContext;
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
    }
}
