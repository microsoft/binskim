// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.PortableExecutable;

using Dia2Lib;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule)), Export(typeof(IOptionsProvider))]
    public class EnableCriticalCompilerWarnings : BinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2007
        /// </summary>
        public override string Id { get { return RuleIds.EnableCriticalCompilerWarningsId; } }

        /// <summary>
        /// Binaries should be compiled with a warning level that enables all critical
        /// security-relevant checks. Enabling at least warning level 3 enables
        /// important static analysis in the compiler that can identify bugs with a
        /// potential to provoke memory corruption, information disclosure, or
        /// double-free vulnerabilities. To resolve this issue, compile at warning
        /// level 3 or higher by supplying /W3, /W4, or /Wall to the compiler, and
        /// resolve the warnings emitted.
        /// </summary>

        public override string FullDescription
        {
            get { return RuleResources.BA2007_EnableCriticalCompilerWarnings_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2007_Pass),
                    nameof(RuleResources.BA2007_Error_WarningsDisabled),
                    nameof(RuleResources.BA2007_Error_InsufficientWarningLevel),
                    nameof(RuleResources.BA2007_Error_UnknownModuleLanguage),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                RequiredCompilerWarnings,
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.EnableCriticalCompilerWarningsId + "." + nameof(EnableCriticalCompilerWarnings);

        /// <summary>
        /// Enable namespace import optimization.
        /// </summary>
        public static PerLanguageOption<IntegerSet> RequiredCompilerWarnings { get; } =
            new PerLanguageOption<IntegerSet>(
                AnalyzerName, nameof(RequiredCompilerWarnings), defaultValue: () => { return BuildRequiredCompilerWarningsSet(); });

        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyManagedAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;

            if (context.Pdb == null)
            {
                Errors.LogExceptionLoadingPdb(context, context.PdbParseException);
                return;
            }

            Pdb di = context.Pdb;

            TruncatedCompilandRecordList warningTooLowModules = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList disabledWarningModules = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList unknownLanguageModules = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList allWarningLevelLowModules = new TruncatedCompilandRecordList();

            string exampleTooLowWarningCommandLine = null;
            int overallMinimumWarningLevel = Int32.MaxValue;
            string exampleDisabledWarningCommandLine = null;
            List<int> overallDisabledWarnings = new List<int>();

            foreach (DisposableEnumerableView<Symbol> omView in di.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                // Detection applies to C/C++ produced by MS compiler only
                if (omDetails.WellKnownCompiler != WellKnownCompilers.MicrosoftNativeCompiler)
                {
                    continue;
                }

                if (omDetails.Language == Language.Unknown)
                {
                    // See if this module contributed to an executable section. If not, we can ignore the module.
                    if (di.CompilandWithIdIsInExecutableSectionContrib(om.SymIndexId))
                    {
                        unknownLanguageModules.Add(om.CreateCompilandRecord());
                    }

                    continue;
                }

                if (!om.CreateChildIterator(SymTagEnum.SymTagFunction).Any())
                {
                    // uninteresting...
                    continue;
                }

                int warningLevel = omDetails.WarningLevel;
                List<int> requiredDisabledWarnings = omDetails.ExplicitlyDisabledWarnings
                    .Where(context.Policy.GetProperty(RequiredCompilerWarnings).Contains).ToList();

                if (warningLevel >= 3 && requiredDisabledWarnings.Count == 0)
                {
                    // We duplicate this condition to bail out early and avoid writing the
                    // module description or newline into sbBadWarningModules if everything
                    // in the module is OK.
                    continue;
                }

                List<string> suffix = new List<string>(2);

                overallMinimumWarningLevel = Math.Min(overallMinimumWarningLevel, warningLevel);
                if (warningLevel < 3)
                {
                    exampleTooLowWarningCommandLine = exampleTooLowWarningCommandLine ?? omDetails.CommandLine;

                    string msg = "[warning level: " + warningLevel.ToString(CultureInfo.InvariantCulture) + "]";
                    warningTooLowModules.Add(om.CreateCompilandRecordWithSuffix(msg));
                    suffix.Add(msg);
                }

                if (requiredDisabledWarnings.Count != 0)
                {
                    MergeInto(overallDisabledWarnings, requiredDisabledWarnings);
                    exampleDisabledWarningCommandLine = exampleDisabledWarningCommandLine ?? omDetails.CommandLine;

                    string msg = "[Explicitly disabled warnings: " + CreateTextWarningList(requiredDisabledWarnings) + "]";
                    disabledWarningModules.Add(om.CreateCompilandRecordWithSuffix(msg));
                    suffix.Add(msg);
                }

                allWarningLevelLowModules.Add(om.CreateCompilandRecordWithSuffix(String.Join(" ", suffix)));
            }

            if (unknownLanguageModules.Empty &&
                exampleTooLowWarningCommandLine == null &&
                exampleDisabledWarningCommandLine == null)
            {
                // '{0}' was compiled at a secure warning level ({1}) and does not 
                // include any modules that disable specific warnings which are 
                // required by policy. As a result, there is a greater likelihood 
                // that memory corruption, information disclosure, double-free and 
                // other security-related vulnerabilities do not exist in code.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                        nameof(RuleResources.BA2007_Pass),
                        context.TargetUri.GetFileName(),
                        overallMinimumWarningLevel.ToString()));
                return;
            }

            if (!unknownLanguageModules.Empty)
            {
                // '{0}' contains code from an unknown language, preventing a 
                // comprehensive analysis of the compiler warning settings. 
                // The language could not be identified for the following modules: {1}
                context.Logger.Log(this, 
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                        nameof(RuleResources.BA2007_Error_UnknownModuleLanguage),
                        context.TargetUri.GetFileName(),
                        unknownLanguageModules.CreateSortedObjectList()));
            }

            if (!String.IsNullOrEmpty(exampleTooLowWarningCommandLine))
            {
                // '{0}' was compiled at too low a warning level. Warning level 3 enables 
                // important static analysis in the compiler to flag bugs that can lead 
                // to memory corruption, information disclosure, or double-free 
                // vulnerabilities.To resolve this issue, compile at warning level 3 or 
                // higher by supplying / W3, / W4, or / Wall to the compiler, and resolve 
                // the warnings emitted.
                // An example compiler command line triggering this check: {1}
                // Modules triggering this check: {2}
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                        nameof(RuleResources.BA2007_Error_InsufficientWarningLevel),
                        context.TargetUri.GetFileName(),
                        overallMinimumWarningLevel.ToString(),
                        exampleTooLowWarningCommandLine,
                        warningTooLowModules.CreateTruncatedObjectList()));
            }

            if (exampleDisabledWarningCommandLine != null)
            {
                // '{0}' disables compiler warning(s) which are required by policy. A 
                // compiler warning is typically required if it has a high likelihood of 
                // flagging memory corruption, information disclosure, or double-free 
                // vulnerabilities. To resolve this issue, enable the indicated warning(s) 
                // by removing /Wxxxx switches (where xxxx is a warning id indicated here) 
                // from your command line, and resolve any warnings subsequently raised 
                // during compilation.
                // An example compiler command line triggering this check was: {1}
                // Modules triggering this check were: {2}
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                        nameof(RuleResources.BA2007_Error_WarningsDisabled),
                        context.TargetUri.GetFileName(),
                        exampleDisabledWarningCommandLine,
                        disabledWarningModules.CreateTruncatedObjectList()));
            }
        }

        private static string CreateTextWarningList(IEnumerable<int> warningList)
        {
            return String.Join(";", warningList
                .Select(warningNumber => warningNumber.ToString(CultureInfo.InvariantCulture)));
        }

        private static void MergeInto(List<int> target, List<int> source)
        {
            // Yes, this is N^2, and SortedSet would be N lg N, but for the
            // values of N we're talking about here, constant factors rule.

            // In practice the number of inserts never rises above 10-20 or so,
            // so optimizing for memory-locality here.

            int idx = 0;
            foreach (int next in source)
            {
                while (idx != target.Count && target[idx] < next)
                {
                    ++idx;
                }

                if (idx == target.Count)
                {
                    target.Add(next);
                }
                else if (target[idx] != next)
                {
                    Debug.Assert(target[idx] > next);
                    target.Insert(idx, next);
                }
            }
        }
        private static IntegerSet BuildRequiredCompilerWarningsSet()
        {
            var result = new IntegerSet();
            result.Add(4018);
            result.Add(4146);
            result.Add(4244);
            result.Add(4267);
            result.Add(4302);
            result.Add(4308);
            result.Add(4509);
            result.Add(4532);
            result.Add(4533);
            result.Add(4700);
            result.Add(4789);
            result.Add(4995);
            result.Add(4996);
            return result;
        }
    }
}
