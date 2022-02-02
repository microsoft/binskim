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

        public override IEnumerable<Assembly> DefaultPluginAssemblies
        {
            get => new Assembly[] { typeof(MarkImageAsNXCompatible).Assembly };
            set => throw new InvalidOperationException();
        }

        protected override BinaryAnalyzerContext CreateContext(AnalyzeOptions options, IAnalysisLogger logger, RuntimeConditions runtimeErrors, PropertiesDictionary policy = null, string filePath = null)
        {
            BinaryAnalyzerContext binaryAnalyzerContext = base.CreateContext(options, logger, runtimeErrors, policy, filePath);

            // Update context object based on command-line parameters.
            binaryAnalyzerContext.ForceOverwrite = options.Force;
            binaryAnalyzerContext.SymbolPath = options.SymbolsPath;
            binaryAnalyzerContext.IgnorePdbLoadError = options.IgnorePdbLoadError;
            binaryAnalyzerContext.LocalSymbolDirectories = options.LocalSymbolDirectories;
            binaryAnalyzerContext.TracePdbLoads = options.Traces.Contains(nameof(Traces.PdbLoad));

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

            context.CompilerDataLogger = new CompilerDataLogger(options.OutputFilePath, context, this.FileSystem);

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

            int smallestPathLegth = smallestPath.Length;
            if (smallestPathLegth == 0)
            {
                return string.Empty;
            }

            if (smallestPath[smallestPathLegth - 1] != '\\')
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
            if (!Environment.GetCommandLineArgs().Any(arg => arg.Equals("--sarif-output-version")))
            {
                analyzeOptions.SarifOutputVersion = Sarif.SarifVersion.Current;
            }

            if (s_UnitTestOutputVersion != Sarif.SarifVersion.Unknown)
            {
                analyzeOptions.SarifOutputVersion = s_UnitTestOutputVersion;
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

        internal static Sarif.SarifVersion s_UnitTestOutputVersion;
    }
}
