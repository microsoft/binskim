// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class EnableDeterministicBuilds : PEBinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2003
        /// </summary>
        public override string Id => RuleIds.EnableDeterministicBuilds;

        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = "TODO" };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2003_Pass),
            nameof(RuleResources.BA2003_Fail),
            nameof(RuleResources.NotApplicable_InvalidMetadata),
        };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            reasonForNotAnalyzing = "";

            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;

        }

        public IEnumerable<IOption> GetOptions()
        {
            throw new NotImplementedException();
        }
    }
}
