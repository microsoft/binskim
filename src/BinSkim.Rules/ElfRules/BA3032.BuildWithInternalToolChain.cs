/// <summary>
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection.PortableExecutable;

using ELFSharp.ELF;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    // Delete the following attribute if this rule doesn't require special
    // configuration capabilities. Delete this code comment in all cases.
    //
    // You should extend this class from WindowsBinaryAndPdbSkimmerBase 
    // instead of PEBinarySkimmerBase if you require PDB parsing in 
    // your check. Extend ELFBinarySkimmerBase for *nix binary checks.
    // 
    [Export(typeof(IOptionsProvider))]
    [Export(typeof(ReportingDescriptor))]
    [Export(typeof(Skimmer<BinaryAnalyzerContext>))]
    public class BuildWithInternalToolChain : ElfBinarySkimmer
    {
        /// <summary>
        /// BA3032
        /// </summary>
        public override string Id => RuleIds.BuildWithInternalToolChain;

        /// <summary>
        /// Recapitulate the full text of the rule description returned below
        /// here as a summary comment.
        /// </summary>

        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA3032_BuildWithInternalToolChain_Description };

        protected override ICollection<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA3032_Pass),
                    nameof(RuleResources.BA3032_Error),
                    nameof(RuleResources.BA3032_BuildWithInternalToolChain_Description)
                };

        public static PerLanguageOption<StringToVersionMap> MinimumToolVersions { get; } =
            new PerLanguageOption<StringToVersionMap>(
                AnalyzerName, nameof(MinimumToolVersions), defaultValue: () => BuildMinimumToolVersionsMap());
        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                MinimumRequiredLinkerVersion
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.BuildWithInternalToolChain + "." + nameof(BuildWithInternalToolChain);

        public static PerLanguageOption<Version> MinimumRequiredLinkerVersion { get; } =
            new PerLanguageOption<Version>(
                AnalyzerName, nameof(MinimumRequiredLinkerVersion), defaultValue: () => new Version("14.0"));

        public override AnalysisApplicability CanAnalyzeElf(ElfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            IELF elf = target.ELF;

            if (elf.Type == FileType.Core || elf.Type == FileType.None || elf.Type == FileType.Relocatable)
            {
                reasonForNotAnalyzing = MetadataConditions.ElfIsCoreNoneOrRelocatable;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            if (!target.Compilers.Any(c => c.Compiler == ElfCompilerType.Clang))
            {
                reasonForNotAnalyzing = MetadataConditions.ElfNotBuiltWithClang;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;
            Pdb pdb = target.Pdb;

            var languageToOutOfPolicyModules = new SortedDictionary<Language, List<ObjectModuleDetails>>();

      
            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                if (string.IsNullOrEmpty(omDetails.CompilerName) ||
                            !((omDetails.CompilerName.Contains(CompilerNames.ClangLLVMRustcPrefix) ||
                            omDetails.CompilerName.Contains(CompilerNames.ClangLLVMPrefix) ||
                            omDetails.CompilerName.Contains(CompilerNames.ClangPrefix)) &&
                            omDetails.CompilerFrontEndVersion >= new Version(1, 86, 0, 0))) //omDetails.Languag
                                                                                            //e == Language.Rust &&
                {   //fail
                    //todo add context 
                    if (!languageToOutOfPolicyModules.TryGetValue(omDetails.Language, out List<ObjectModuleDetails> outOfPolicyModules))
                    {
                        outOfPolicyModules = new List<ObjectModuleDetails>();
                        languageToOutOfPolicyModules.Add(omDetails.Language, outOfPolicyModules);
                    }
                    Version minCompilerVersion = omDetails.CompilerFrontEndVersion;
                    string minimumRequiredCompilers = BuildMinimumCompilersList(context, languageToOutOfPolicyModules);
                    string outOfPolicyModulesText = BuildOutOfPolicyModulesList(languageToOutOfPolicyModules);
                    // '{0}' was compiled with one or more modules which were not built using
                    // minimum required tool versions ({1}). More recent toolchains
                    // contain mitigations that make it more difficult for an attacker to exploit
                    // vulnerabilities in programs they produce. To resolve this issue, compile
                    // and /or link your binary with more recent tools. If you are servicing a
                    // product where the tool chain cannot be modified (e.g. producing a hotfix
                    // for an already shipped version) ignore this warning. Modules built outside
                    // of policy: {2}
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                        nameof(RuleResources.BA3032_Error),
                            context.CurrentTarget.Uri.GetFileName(),
                            minimumRequiredCompilers,
                            outOfPolicyModulesText));
                    return;
                }
            }

            // '{0}' enables /beEvenMoreSecure on both the compiler and linker
            // command-lines, preventing a broad range of conditions that 
            // bad actors can use to engage in their malignant, unfortunately
            // often-profitable foolishness.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA3032_Pass),
                        context.CurrentTarget.Uri.GetFileName()));
        }

        // Not considered a meaningful method name. Be sure to do a better job.
        private bool IsSecure(PEBinary _)
        {
            // Add relevant PE-level examination 
            return false;
        }

        private string BuildMinimumCompilersList(BinaryAnalyzerContext context, SortedDictionary<Language, List<ObjectModuleDetails>> languageToOutOfPolicyModules)
        {
            var languages = new List<string>();

            foreach (Language language in languageToOutOfPolicyModules.Keys)
            {
                Version version = context.Policy.GetProperty(MinimumToolVersions)[language.ToString()];
                languages.Add($"{language} ({version})");
            }
            return string.Join(", ", languages);
        }

        private static StringToVersionMap BuildMinimumToolVersionsMap()
        {
            var result = new StringToVersionMap
            {
                [nameof(Language.Rust)] = new Version(1, 86, 0, 0),
                [nameof(Language.Unknown)] = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)
                
            };


            return result;
        }

        private string BuildOutOfPolicyModulesList(SortedDictionary<Language, List<ObjectModuleDetails>> languageToOutOfPolicyModules)
        {
            var coalescedModules = new List<string>();

            foreach (Language language in languageToOutOfPolicyModules.Keys)
            {
                string modulesText = languageToOutOfPolicyModules[language].CreateOutputCoalescedByCompiler();
                coalescedModules.Add(modulesText);
            }
            return string.Join(string.Empty, coalescedModules);
        }
    }
}
