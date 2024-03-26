// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class ReportElfOrMachoCompilerData : DwarfSkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA4002. This reporting rule writes compiler data to AppInsights and 
        /// a CSV file (if configured) for every compilation unit that's scanned.
        /// </summary>
        public override string Id => RuleIds.ReportElfOrMachoCompilerData;

        /// <summary>
        /// This rule emits CSV data to the console
        /// for every compiler/language/version combination that's observed.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA4002_ReportELFCompilerData_Description };

        protected override IEnumerable<string> MessageResourceNames => Array.Empty<string>();

        public override AnalysisApplicability CanAnalyzeDwarf(IDwarfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                CompilerDataLogger.CsvOutputPath,
            }.ToImmutableArray();
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            if (!context.CompilerDataLogger.Enabled)
            {
                return;
            }

            IDwarfBinary binary = context.DwarfBinary();

            if (binary is ElfBinary)
            {
                this.WriteCompilerData(context, binary.CommandLineInfos, binary.Compilers);
            }

            if (binary is MachOBinary machO)
            {
                machO.MachOs.ToList().ForEach
                (
                    machO => this.WriteCompilerData(context, machO.CommandLineInfos, machO.Compilers)
                );
            }
        }

        private void WriteCompilerData(BinaryAnalyzerContext context, List<DwarfCompileCommandLineInfo> commandLineInfos, ICompiler[] compilers)
        {
            var processedRecords = new HashSet<CompilerData>();

            foreach (ICompiler compiler in compilers)
            {
                if (compiler.Compiler == ElfCompilerType.Unknown)
                {
                    continue;
                }

                if (commandLineInfos.Count == 0)
                {
                    // if it does not contain valid command line,
                    // still display a line for the file with valid compiler info.
                    commandLineInfos.Add(new DwarfCompileCommandLineInfo()
                    {
                        CommandLine = string.Empty,
                        Language = DwarfLanguage.Unknown,
                    });
                }

                foreach (DwarfCompileCommandLineInfo info in commandLineInfos)
                {
                    var record = new CompilerData
                    {
                        BinaryType = "ELF",
                        ModuleName = info.FileName,
                        Dialect = info.GetDialect(),
                        ModuleLibrary = string.Empty,
                        CommandLine = info.CommandLine,
                        CompilerName = compiler.Compiler.ToString(),
                        CompilerBackEndVersion = compiler.Version.ToString(),
                        CompilerFrontEndVersion = compiler.Version.ToString(),
                        Language = info.Language == DwarfLanguage.Unknown ? string.Empty : info.Language.ToString(),
                    };

                    if (processedRecords.Contains(record))
                    {
                        continue;
                    }

                    processedRecords.Add(record);
                    context.CompilerDataLogger.Write(context, record);
                }
            }
        }
    }
}
