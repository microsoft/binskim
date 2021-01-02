// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using Dia2Lib;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class EnableSecureSourceCodeHashing : WindowsBinaryAndPdbSkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2004
        /// </summary>
        public override string Id => RuleIds.EnableSecureSourceCodeHashing;

        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = "TODO" };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2004_Pass_Managed),
            nameof(RuleResources.BA2004_Pass_Native),
            nameof(RuleResources.BA2004_Error_Managed),
            nameof(RuleResources.BA2004_Error_Native),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                RequiredCompilerWarnings,
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.EnableSecureSourceCodeHashing + "." + nameof(EnableSecureSourceCodeHashing);

        /// <summary>
        /// Enable namespace import optimization.
        /// </summary>
        public static PerLanguageOption<IntegerSet> RequiredCompilerWarnings { get; } =
            new PerLanguageOption<IntegerSet>(
                AnalyzerName, nameof(RequiredCompilerWarnings), defaultValue: () => BuildRequiredCompilerWarningsSet());

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb di = target.Pdb;

            foreach (DisposableEnumerableView<Symbol> omView in di.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                if (omDetails.Language != Language.C && omDetails.Language != Language.Cxx)
                {
                    continue;
                }

                if (!omDetails.HasDebugInfo)
                {
                    continue;
                }

                foreach (DisposableEnumerableView<SourceFile> sfView in di.CreateSourceFileIterator(om))
                {
                    SourceFile sf = sfView.Value;

                    if (sf.HashType != HashType.SHA256)
                    {
                        if (om.IsManaged)
                        {

                        }
                        else
                        {

                        }
                    }
                }
            }
        }

        private static string CreateTextWarningList(IEnumerable<int> warningList)
        {
            return string.Join(";", warningList
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
            var result = new IntegerSet
            {
                4018,
                4146,
                4244,
                4267,
                4302,
                4308,
                4509,
                4532,
                4533,
                4700,
                4789,
                4995,
                4996
            };
            return result;
        }
    }
}
