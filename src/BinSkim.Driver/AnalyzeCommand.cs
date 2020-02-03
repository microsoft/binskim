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

namespace Microsoft.CodeAnalysis.IL
{
    internal class AnalyzeCommand : AnalyzeCommandBase<BinaryAnalyzerContext, AnalyzeOptions>
    {
        public static HashSet<string> ValidAnalysisFileExtensions = new HashSet<string>(
            new string[] { ".dll", ".exe", ".sys" }
            );

        public override IEnumerable<Assembly> DefaultPlugInAssemblies
        {
            get => new Assembly[] { typeof(MarkImageAsNXCompatible).Assembly };
            set => throw new InvalidOperationException();
        }

        protected override BinaryAnalyzerContext CreateContext(AnalyzeOptions options, IAnalysisLogger logger, RuntimeConditions runtimeErrors, string filePath = null)
        {
            BinaryAnalyzerContext binaryAnalyzerContext = base.CreateContext(options, logger, runtimeErrors, filePath);
            binaryAnalyzerContext.SymbolPath = options.SymbolsPath;
            binaryAnalyzerContext.LocalSymbolDirectories = options.LocalSymbolDirectories;
            return binaryAnalyzerContext;
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

        internal static SarifVersion s_UnitTestOutputVersion = SarifVersion.Unknown;
    }
}