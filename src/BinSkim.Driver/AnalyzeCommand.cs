// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif.Readers;
using Microsoft.CodeAnalysis.Sarif.VersionOne;
using Microsoft.CodeAnalysis.Sarif.Visitors;

namespace Microsoft.CodeAnalysis.IL
{
    public class AnalyzeCommand : MultithreadedAnalyzeCommandBase<BinaryAnalyzerContext, AnalyzeOptions>
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

            binaryAnalyzerContext.SymbolPath = options.SymbolsPath;
            binaryAnalyzerContext.IgnorePdbLoadError = options.IgnorePdbLoadError;
            binaryAnalyzerContext.TracePdbLoads = options.Traces.Contains(nameof(Traces.PdbLoad));
            binaryAnalyzerContext.LocalSymbolDirectories = options.LocalSymbolDirectories;

#pragma warning disable CS0618 // Type or member is obsolete
            if (options.Verbose && ShouldWarnVerbose)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                Warnings.LogObsoleteOption(binaryAnalyzerContext, "--verbose", Sdk.SdkResources.Verbose_ReplaceWithLevelAndKind);
                ShouldWarnVerbose = false;
            }

            binaryAnalyzerContext.CompilerDataLogger = new CompilerDataLogger(binaryAnalyzerContext,
                                                                              options.TargetFileSpecifiers);

            return binaryAnalyzerContext;
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

#pragma warning disable CS0618 // Type or member is obsolete
            if (analyzeOptions.Verbose)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                analyzeOptions.Level = new List<FailureLevel> { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note };
                analyzeOptions.Kind = new List<ResultKind> { ResultKind.Fail, ResultKind.NotApplicable, ResultKind.Pass };
            }

#pragma warning disable CS0618 // Type or member is obsolete
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

            try
            {
                if (CompilerDataLogger.TelemetryEnabled &&
                    !string.IsNullOrEmpty(analyzeOptions.OutputFilePath) &&
                    this.FileSystem.FileExists(analyzeOptions.OutputFilePath))
                {
                    SarifLog sarifLog = ReadSarifLog(this.FileSystem, analyzeOptions);

                    AnalysisSummary summary = AnalysisSummaryExtractor.ExtractAnalysisSummary(
                        sarifLog, analyzeOptions);
                    CompilerDataLogger.Summarize(summary);

                    IEnumerable<ExecutionException> exceptions = AnalysisSummaryExtractor.ExtractExceptionData(sarifLog);
                    foreach (ExecutionException ex in exceptions)
                    {
                        CompilerDataLogger.WriteException(ex, summary);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                CompilerDataLogger.Flush();
            }

            // In BinSkim, no rule is ever applicable to every target type. For example,
            // we have checks that are only relevant to either 32-bit or 64-bit binaries.
            // Because of this, the return code bit for RuleNotApplicableToTarget is not
            // interesting (it will always be set).

            return analyzeOptions.RichReturnCode
                ? (int)((uint)result & ~(uint)RuntimeConditions.RuleNotApplicableToTarget)
                : result;
        }

        internal static SarifLog ReadSarifLog(IFileSystem fileSystem, AnalyzeOptions analyzeOptions)
        {
            SarifLog sarifLog;
            if (analyzeOptions.SarifOutputVersion == Sarif.SarifVersion.Current)
            {
                sarifLog = SarifLog.Load(analyzeOptions.OutputFilePath);
            }
            else
            {
                SarifLogVersionOne actualLog = ReadSarifFile<SarifLogVersionOne>(fileSystem, analyzeOptions.OutputFilePath, SarifContractResolverVersionOne.Instance);
                var visitor = new SarifVersionOneToCurrentVisitor();
                visitor.VisitSarifLogVersionOne(actualLog);
                sarifLog = visitor.SarifLog;
            }

            return sarifLog;
        }

        internal static Sarif.SarifVersion s_UnitTestOutputVersion;
    }
}
