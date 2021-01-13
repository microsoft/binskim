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
    public class AnalyzeCommand : AnalyzeCommandBase<BinaryAnalyzerContext, AnalyzeOptions>
    {
        public static HashSet<string> ValidAnalysisFileExtensions = new HashSet<string>(
            new string[] { ".dll", ".exe", ".sys" }
            );

        public override IEnumerable<Assembly> DefaultPlugInAssemblies
        {
            get => new Assembly[] { typeof(MarkImageAsNXCompatible).Assembly };
            set => throw new InvalidOperationException();
        }

        protected override BinaryAnalyzerContext CreateContext(AnalyzeOptions options, IAnalysisLogger logger, RuntimeConditions runtimeErrors, PropertiesDictionary policy = null, string filePath = null)
        {
            BinaryAnalyzerContext binaryAnalyzerContext = base.CreateContext(options, logger, runtimeErrors, policy, filePath);

            binaryAnalyzerContext.SymbolPath = options.SymbolsPath;
            binaryAnalyzerContext.TracePdbLoads = options.Traces.Contains(nameof(Traces.PdbLoad));
            binaryAnalyzerContext.LocalSymbolDirectories = options.LocalSymbolDirectories;

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

            int result = base.Run(analyzeOptions);

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
