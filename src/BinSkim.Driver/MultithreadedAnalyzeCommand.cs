// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL
{
    public class MultithreadedAnalyzeCommand : MultithreadedAnalyzeCommandBase<BinaryAnalyzerContext, AnalyzeOptions>
    {
        private static bool ShouldWarnVerbose = true;

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

        protected override BinaryAnalyzerContext CreateContext(AnalyzeOptions options, IAnalysisLogger logger, RuntimeConditions runtimeErrors, PropertiesDictionary policy = null, string filePath = null)
        {
            if (logger is AggregatingLogger aggregatingLogger && this.Telemetry?.TelemetryClient != null)
            {
                aggregatingLogger.Loggers.Add(new RuleTelemetryLogger(this.Telemetry.TelemetryClient));
            }

            BinaryAnalyzerContext binaryAnalyzerContext = base.CreateContext(options, logger, runtimeErrors, policy, filePath);

            // Update context object based on command-line parameters.
            binaryAnalyzerContext.ForceOverwrite = options.Force;
            binaryAnalyzerContext.SymbolPath = options.SymbolsPath;
            binaryAnalyzerContext.IgnorePdbLoadError = options.IgnorePdbLoadError;
            binaryAnalyzerContext.LocalSymbolDirectories = options.LocalSymbolDirectories;
            binaryAnalyzerContext.TracePdbLoads = options.Traces.Contains(nameof(Traces.PdbLoad));
            binaryAnalyzerContext.MaxFileSizeInKilobytes = options.MaxFileSizeInKilobytes > 1024 ? options.MaxFileSizeInKilobytes : int.MaxValue;

#pragma warning disable CS0618 // Type or member is obsolete
            if (options.Verbose && ShouldWarnVerbose)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                Warnings.LogObsoleteOption(binaryAnalyzerContext, "--verbose", Sdk.SdkResources.Verbose_ReplaceWithLevelAndKind);
                ShouldWarnVerbose = false;
            }

            return binaryAnalyzerContext;
        }

        protected override void InitializeConfiguration(AnalyzeOptions options, BinaryAnalyzerContext context)
        {
            base.InitializeConfiguration(options, context);

            // Command-line provided policy is now initialized. Update context 
            // based on any possible configuration provided in this way.

            context.CompilerDataLogger = new CompilerDataLogger(options.OutputFilePath, options.SarifOutputVersion, context, this.FileSystem, this.Telemetry);

            // If the user has hard-coded a non-deterministic file path root to elide from telemetry,
            // we will honor that. If it has not been specified, and if all file target specifiers
            // point to a common directory, then we will use that directory as the path to elide.
            if (string.IsNullOrEmpty(context.CompilerDataLogger.RootPathToElide))
            {
                context.CompilerDataLogger.RootPathToElide =
                    ReturnCommonPathRootFromTargetSpecifiersIfOneExists(options.TargetFileSpecifiers);
            }
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
            if (analyzeOptions.Verbose)
#pragma warning restore CS0618
            {
                analyzeOptions.Kind = new List<ResultKind> { ResultKind.Fail, ResultKind.NotApplicable, ResultKind.Pass };
                analyzeOptions.Level = new List<FailureLevel> { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note };
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

            // In BinSkim, no rule is ever applicable to every target type. For example,
            // we have checks that are only relevant to either 32-bit or 64-bit binaries.
            // Because of this, the return code bit for RuleNotApplicableToTarget is not
            // interesting (it will always be set).

            return analyzeOptions.RichReturnCode
                ? (int)((uint)result & ~(uint)RuntimeConditions.RuleNotApplicableToTarget)
                : result;
        }

        internal Sarif.SarifVersion UnitTestOutputVersion { get; set; }

        private Sdk.Telemetry Telemetry { get; }
    }
}
