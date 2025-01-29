// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class ReportPECompilerData : WindowsBinaryAndPdbSkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA4001. This reporting rule writes compiler data to AppInsights and 
        /// a CSV file (if configured) for every compilation unit that's scanned.
        /// </summary>
        public override string Id => RuleIds.ReportPortableExecutableCompilerData;

        public override bool LogPdbLoadException => false;

        /// <summary>
        /// This rule emits CSV data to the console for every compiler/language/version
        /// combination that's observed in any PDB-linked compiland.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA4001_ReportPECompilerData_Description };

        protected override ICollection<string> MessageResourceNames => Array.Empty<string>();

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                CompilerDataLogger.CsvOutputPath,
            }.ToImmutableArray();
        }

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            if (!context.CompilerDataLogger.Enabled)
            {
                return;
            }

            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            if (pdb == null)
            {
                string errorMessage = target.PdbParseException.Message;
                context.CompilerDataLogger.WriteException(context, errorMessage);
                return;
            }

            var records = new Dictionary<CompilerData, ObjectModuleDetails>();

            // Add the last modified date for the target and the associated pdb
            string pdbLastAccessDateUtc, targetLastAccessDateUtc;
            try
            {
                pdbLastAccessDateUtc = File.GetLastAccessTimeUtc(pdb.PdbLocation).ToString();
                targetLastAccessDateUtc = File.GetLastAccessTimeUtc(target.TargetUri.AbsolutePath).ToString();
            }
            catch (Exception)
            {
                pdbLastAccessDateUtc = string.Empty;
                targetLastAccessDateUtc = string.Empty;
            }

            if (target.PE.IsManaged)
            {
                var record = new CompilerData
                {
                    BinaryType = "PE",
                    CompilerName = pdb.GetCompilerName() ?? pdb.GetCompilerNameFromCompilandDetails() ?? ".NET Compiler", // Inexpensive -> expensive -> fallback
                    Language = nameof(Language.MSIL),
                    DebuggingFileName = pdb.GlobalScope?.Name,
                    DebuggingFileGuid = pdb.GlobalScope?.Guid.ToString(),
                    DebuggingFileLastModifiedDateUtc = pdbLastAccessDateUtc,
                    TargetLastModifiedDateUtc = targetLastAccessDateUtc,
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
                        DebuggingFileLastModifiedDateUtc = pdbLastAccessDateUtc,
                        TargetLastModifiedDateUtc = targetLastAccessDateUtc,
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
                context.CompilerDataLogger.Write(context, kv.Key);
            }
        }
    }
}
