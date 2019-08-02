// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif.Visitors;
using Microsoft.CodeAnalysis.Sarif.Writers;

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

        public override int Run(AnalyzeOptions analyzeOptions)
        {
            if (!Environment.GetCommandLineArgs().Where(arg => arg.Equals("--sarif-output-version")).Any())
            {
                analyzeOptions.SarifOutputVersion = SarifVersion.OneZeroZero;
            }

            if (s_UnitTestOutputVersion != SarifVersion.Unknown)
            {
                analyzeOptions.SarifOutputVersion = s_UnitTestOutputVersion;
            }

            int result = base.Run(analyzeOptions);

            // In BinSkim, no rule is ever applicable to every target type. For example,
            // we have checks that are only relevant to either 32-bit or 64-bit binaries.
            // Because of this, the return code bit for RuleNotApplicableToTarget is not
            // interesting (it will always be set). 
            return analyzeOptions.RichReturnCode 
                ? (int)((uint)result & ~(uint)RuntimeConditions.RuleNotApplicableToTarget) 
                : result;
        }

        protected override void AnalyzeTarget(IEnumerable<Skimmer<BinaryAnalyzerContext>> skimmers, BinaryAnalyzerContext context, HashSet<string> disabledSkimmers)
        {
            base.AnalyzeTarget(skimmers, context, disabledSkimmers);
        }

        internal static SarifVersion s_UnitTestOutputVersion = SarifVersion.Unknown;
    }
}