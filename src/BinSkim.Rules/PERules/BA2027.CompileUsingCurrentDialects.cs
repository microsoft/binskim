// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class CompileUsingCurrentDialects : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA2027
        /// </summary>
        public override string Id => RuleIds.CompileUsingCurrentDialects;

        /// <summary>
        /// The '/std' setting enables supported C and C++ language features from the 
        /// specified version of the C or C++ language standard. Compile using current 
        /// dialects enables current standard-specific features and behavior.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString
        {
            Text = RuleResources.BA2027_CompileUsingCurrentDialects_Description
        };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2027_Pass),
            nameof(RuleResources.BA2027_Warning),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability notApplicable = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            if (portableExecutable.IsILOnly)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
                return notApplicable;
            }

            if (portableExecutable.IsResourceOnly)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
                return notApplicable;
            }

            if (portableExecutable.IsNativeUniversalWindowsPlatform)
            {
                reasonForNotAnalyzing = MetadataConditions.ImageIsNativeUniversalWindowsPlatformBinary;
                return notApplicable;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            var notCompiledUsingCurrentDialectsModules = new List<AnalyzeResult>();

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails details = om.GetObjectModuleDetails();

                if (details.Name != details.Library)
                {
                    continue;
                }

                string[] cVersion;
                string[] cVersionExcluded = null;
                if (details.WellKnownCompiler == WellKnownCompilers.MicrosoftC)
                {
                    cVersion = new string[] { "std:c" };
                    cVersionExcluded = new string[] { "std:c++" };
                }
                else if (details.WellKnownCompiler == WellKnownCompilers.MicrosoftCxx)
                {
                    cVersion = new string[] { "std:c++" };
                }
                else
                {
                    continue;
                }

                if (!details.HasDebugInfo)
                {
                    continue;
                }

                string cVersionNumberString = string.Empty;
                details.GetOptionValue(cVersion, OrderOfPrecedence.FirstWins, ref cVersionNumberString, cVersionExcluded);
                AnalyzeResultType resultType = AnalyzeResultType.NotCurrent;

                if (string.IsNullOrWhiteSpace(cVersionNumberString))
                {
                    resultType = AnalyzeResultType.NotSet;
                }
                else
                {
                    int cVersionNumber;
                    if (cVersionNumberString.Equals("latest", StringComparison.OrdinalIgnoreCase))
                    {
                        resultType = AnalyzeResultType.Latest;
                    }
                    else if (int.TryParse(cVersionNumberString, out cVersionNumber))
                    {
                        resultType = cVersionNumber >= 17 ? AnalyzeResultType.Current : AnalyzeResultType.NotCurrent;
                    }
                    else
                    {
                        resultType = AnalyzeResultType.Unsupported;
                    }
                }

                if (resultType != AnalyzeResultType.Current && resultType != AnalyzeResultType.Latest)
                {
                    if (!notCompiledUsingCurrentDialectsModules.Any(l => l.Details.Library == details.Library))
                    {
                        notCompiledUsingCurrentDialectsModules.Add(new AnalyzeResult() { Details = details, Version = cVersionNumberString, Type = resultType });
                    }
                }
            }

            if (notCompiledUsingCurrentDialectsModules.Count > 0)
            {
                string line;
                var sb = new StringBuilder();

                foreach (AnalyzeResult result in notCompiledUsingCurrentDialectsModules)
                {
                    // Library: {0}, Language: {1}, Version: {2}
                    line = string.Format(RuleResources.BA2027_Warning_Item,
                        Path.GetFileName(result.Details.Library),
                        result.Details.Language,
                        result.Type == AnalyzeResultType.NotSet ? "NotSet" : result.Version);
                    sb.AppendLine(line);
                }

                // '{0}' is a Windows PE that wasn't compiled with current dialects.
                // Compile using current dialects enables current standard-specific
                // features and behavior. To resolve this problem, pass version 17
                // or later on the cl.exe command-line, e.g. '/std:c++17' for C++
                // and '/std:c17' for C or set the corresponding 'Language Standard'
                // property in the 'C/C++ -> General' Configuration property page.
                // The following modules were not compiled with current dialects:
                // {1}
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                    nameof(RuleResources.BA2027_Warning),
                    context.TargetUri.GetFileName(),
                        sb.ToString()));
            }
            else
            {
                // '{0}' is a Windows PE that was compiled with current dialects.
                // Compile using current dialects enables current standard-specific
                // features and behavior.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2027_Pass),
                    context.TargetUri.GetFileName()));
                return;
            }
        }

        private class AnalyzeResult
        {
            public string Version { get; set; }
            public AnalyzeResultType Type { get; set; }
            public ObjectModuleDetails Details { get; set; }
        }

        private enum AnalyzeResultType
        {
            Latest,
            NotSet,
            Current,
            NotCurrent,
            Unsupported
        }
    }
}
