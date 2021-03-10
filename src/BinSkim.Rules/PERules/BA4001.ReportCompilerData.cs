// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class ReportCompilerData : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA4001
        /// </summary>
        public override string Id => RuleIds.ReportCompilerData;

        /// <summary>
        /// This rule emits CSV data to the console for every compiler/language/version
        /// combination that's observed in any PDB-linked compiland.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA4001_ReportCompilerData_Description };

        public override bool EnabledByDefault => false;

        protected override IEnumerable<string> MessageResourceNames => Array.Empty<string>();

        private bool PrintHeader = true;

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            if (pdb == null)
            {
                return;
            }

            var records = new Dictionary<string, ObjectModuleDetails>();

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                string record =
                    omDetails.CompilerName + "," +
                    omDetails.CompilerBackEndVersion + "," +
                    omDetails.CompilerFrontEndVersion + "," +
                    omDetails.Language;

                if (!records.TryGetValue(record, out ObjectModuleDetails value))
                {
                    records[record] = omDetails;
                }
            }

            if (PrintHeader)
            {
                Console.WriteLine("Target,Compiler Name,Compiler BackEnd Version,Compiler FrontEnd Version,Language,Module Name,Module Library");
                PrintHeader = false;
            }

            foreach (KeyValuePair<string, ObjectModuleDetails> kv in records)
            {
                string compilerData = kv.Key;
                ObjectModuleDetails omDetails = kv.Value;

                Console.Write(context.TargetUri.LocalPath + ",");
                Console.Write(compilerData + ",");
                Console.Write(omDetails.Name + ",");
                Console.WriteLine(omDetails.Library);
            }
        }
    }
}
