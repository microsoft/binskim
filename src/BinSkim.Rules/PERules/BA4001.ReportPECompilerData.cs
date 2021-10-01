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
    public class ReportPECompilerData : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA4001
        /// </summary>
        public override string Id => RuleIds.ReportPECompilerData;

        public override bool LogPdbLoadException => false;

        /// <summary>
        /// This rule emits CSV data to the console for every compiler/language/version
        /// combination that's observed in any PDB-linked compiland.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA4001_ReportPECompilerData_Description };

        public override bool EnabledByDefault => false;

        protected override IEnumerable<string> MessageResourceNames => Array.Empty<string>();

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            context.CompilerDataLogger.PrintHeader();

            if (pdb == null)
            {
                string errorMessage = target.PdbParseException.Message;
                context.CompilerDataLogger.WriteException(errorMessage);
                return;
            }

            var records = new Dictionary<CompilerData, ObjectModuleDetails>();

            if (target.PE.IsManaged)
            {
                var record = new CompilerData
                {
                    BinaryType = "PE",
                    CompilerName = ".NET Compiler",
                    Language = nameof(Language.MSIL),
                    DebuggingFileName = pdb.GlobalScope?.Name,
                    DebuggingFileGuid = pdb.GlobalScope?.Guid.ToString(),
                    FileVersion = target.PE.FileVersion?.FileVersion,
                    CompilerBackEndVersion = target.PE.LinkerVersion.ToString(),
                    CompilerFrontEndVersion = target.PE.LinkerVersion.ToString(),
                    AssemblyReferences = string.Join(';', target.PE.GetAssemblyReferenceStrings()),
                };

                if (!records.ContainsKey(record))
                {
                    records[record] = null;
                }
            }
            else
            {
                foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
                {
                    Symbol om = omView.Value;
                    ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                    var record = new CompilerData
                    {
                        BinaryType = "PE",
                        ModuleName = omDetails?.Name,
                        ModuleLibrary = omDetails?.Library,
                        Dialect = omDetails.GetDialect(out _),
                        CompilerName = omDetails.CompilerName,
                        CommandLine = omDetails.RawCommandLine,
                        Language = omDetails.Language.ToString(),
                        DebuggingFileName = pdb.GlobalScope?.Name,
                        FileVersion = target.PE.FileVersion?.FileVersion,
                        DebuggingFileGuid = pdb.GlobalScope?.Guid.ToString(),
                        CompilerBackEndVersion = omDetails.CompilerBackEndVersion.ToString(),
                        CompilerFrontEndVersion = omDetails.CompilerFrontEndVersion.ToString(),
                    };

                    if (!records.ContainsKey(record))
                    {
                        records[record] = omDetails;
                    }
                }
            }

            foreach (KeyValuePair<CompilerData, ObjectModuleDetails> kv in records)
            {
                context.CompilerDataLogger.Write(kv.Key);
            }
        }
    }
}
