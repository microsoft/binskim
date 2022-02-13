// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
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
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class BuildWithSecureTools : WindowsBinaryAndPdbSkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2006
        /// </summary>
        public override string Id => RuleIds.BuildWithSecureTools;

        /// <summary>
        /// Application code should be compiled with the most up-to-date tool sets
        /// possible to take advantage of the most current compile-time security
        /// features. Among other things, these features provide address space
        /// layout randomization, help prevent arbitrary code execution and enable
        /// code generation that can help prevent speculative execution side-channel
        /// attacks.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2006_BuildWithSecureTools_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2006_Error),
                    nameof(RuleResources.BA2006_Error_BadModule),
                    nameof(RuleResources.BA2006_Pass),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)};

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                AllowedLibraries,
                MinimumToolVersions,
                AdvancedMitigationsEnforced
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.BuildWithSecureTools + "." + nameof(BuildWithSecureTools);

        [ThreadStatic]
        private static StringBuilder s_sb;

        public static PerLanguageOption<Version> MinimumCCompilerVersion { get; } =
            new PerLanguageOption<Version>(AnalyzerName, nameof(Language.C), defaultValue: () => new Version());

        public static PerLanguageOption<Version> MinimumCxxCompilerVersion { get; } =
            new PerLanguageOption<Version>(AnalyzerName, nameof(Language.Cxx), defaultValue: () => new Version());

        public static PerLanguageOption<Version> MinimumUnknownCompilerVersion { get; } =
            new PerLanguageOption<Version>(AnalyzerName, nameof(Language.Unknown), defaultValue: () => new Version());

        public static PerLanguageOption<Version> MinimumXboxCompilerVersion { get; } =
            new PerLanguageOption<Version>(AnalyzerName, nameof(MinimumXboxCompilerVersion), defaultValue: () => new Version());

        public static PerLanguageOption<StringToVersionMap> MinimumToolVersions { get; } =
            new PerLanguageOption<StringToVersionMap>(
                AnalyzerName, nameof(MinimumToolVersions), defaultValue: () => BuildMinimumToolVersionsMap());

        public static PerLanguageOption<StringToVersionMap> AllowedLibraries { get; } =
            new PerLanguageOption<StringToVersionMap>(
                AnalyzerName, nameof(AllowedLibraries), defaultValue: () => BuildAllowedLibraries());

        public static PerLanguageOption<AdvancedMitigations> AdvancedMitigationsEnforced { get; } =
            new PerLanguageOption<AdvancedMitigations>(
                AnalyzerName, nameof(AdvancedMitigationsEnforced), defaultValue: () => AdvancedMitigations.None);

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            /*
             * This is disabled for now. It's not clear that we can
             * actually detect when a binary is compiled with a version
             * of csc.exe that is too stale. The reasons are twofold:
             * 1) Older versions of csc.exe did not version the version
             *    data that is persisted to the PE. i.e., '48.0' was
             *    emitted for numerous versions of the compiler.
             * 2) Our PDB reader does not current appear able to crack
             *    PDBs generated by the older C# compilers. Needs to
             *    be investigated.
            if (target.PE.IsManaged && !target.PE.IsMixedMode)
            {
                AnalyzeManagedPE(context);
            }
            */

            Version minCompilerVersion;

            var inPolicyCompilers = new HashSet<string>();

            StringToVersionMap allowedLibraries = context.Policy.GetProperty(AllowedLibraries);

            var languageToOutOfPolicyModules = new SortedDictionary<Language, List<ObjectModuleDetails>>();

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                if (omDetails.WellKnownCompiler != WellKnownCompilers.MicrosoftC &&
                    omDetails.WellKnownCompiler != WellKnownCompilers.MicrosoftCxx)
                {
                    // TODO: MikeFan (1/6/2022)
                    // We need to take a step back and comprehensively review our compiler/language support.
                    // https://github.com/Microsoft/binskim/issues/114
                    continue;
                }

                minCompilerVersion = RetrieveMinimumCompilerVersion(context, omDetails.Language);

                // See if the item is in our skip list
                if (!string.IsNullOrEmpty(om.Lib))
                {
                    string libFileName = string.Concat(System.IO.Path.GetFileName(om.Lib), ",", omDetails.Language.ToString()).ToLowerInvariant();

                    if (allowedLibraries.TryGetValue(libFileName, out Version minAllowedVersion) &&
                        omDetails.CompilerBackEndVersion >= minAllowedVersion)
                    {
                        continue;
                    }
                }

                Version actualVersion;
                Version minimumVersion = minCompilerVersion;
                Language omLanguage = omDetails.Language;
                switch (omLanguage)
                {
                    case Language.C:
                    case Language.Cxx:
                    {
                        actualVersion = Minimum(omDetails.CompilerBackEndVersion, omDetails.CompilerFrontEndVersion);
                        break;
                    }

                    case Language.LINK:
                    case Language.MASM:
                    case Language.CVTRES:
                    {
                        actualVersion = omDetails.CompilerBackEndVersion;
                        break;
                    }

                    default:
                        continue;
                }

                bool foundIssue = actualVersion < minimumVersion;

                AdvancedMitigations advancedMitigations = context.Policy.GetProperty(AdvancedMitigationsEnforced);
                if (!foundIssue &&
                    target.PE != null &&
                    (advancedMitigations & AdvancedMitigations.Spectre) == AdvancedMitigations.Spectre)
                {
                    var machineType = (ExtendedMachine)target.PE.Machine;

                    // Current toolchain is within the version range to validate.
                    // Now we'll retrieve relevant compiler mitigation details to
                    // ensure this object module's build and revision meet
                    // expectations.
                    CompilerMitigations newMitigationData =
                        EnableSpectreMitigations.GetAvailableMitigations(context, machineType, actualVersion);

                    // Current compiler version does not support Spectre mitigations.
                    foundIssue = !newMitigationData.HasFlag(CompilerMitigations.D2GuardSpecLoadAvailable)
                                 && !newMitigationData.HasFlag(CompilerMitigations.QSpectreAvailable);

                    if (foundIssue)
                    {
                        // Get the closest compiler version that has mitigations--i.e. if the user is using a 19.0 (VS2015) compiler, we should be recommending an upgrade to the
                        // 19.0 version that has the mitigations, not an upgrade to a 19.10+ (VS2017) compiler.
                        // Limitation--if there are multiple 'upgrade to' versions to recommend, this just going to give users the last one we see in the error.
                        minCompilerVersion = EnableSpectreMitigations.GetClosestCompilerVersionWithSpectreMitigations(context, machineType, actualVersion);

                        // Indicates Spectre mitigations are not supported on this platform.  We won't flag this case.
                        if (minCompilerVersion == null)
                        {
                            foundIssue = false;
                        }
                    }
                }

                if (foundIssue)
                {
                    if (!languageToOutOfPolicyModules.TryGetValue(omDetails.Language, out List<ObjectModuleDetails> outOfPolicyModules))
                    {
                        outOfPolicyModules = new List<ObjectModuleDetails>();
                        languageToOutOfPolicyModules.Add(omDetails.Language, outOfPolicyModules);
                    }

                    outOfPolicyModules.Add(omDetails);
                }
                else
                {
                    inPolicyCompilers.Add(BuildCompilerIdentifier(omDetails));
                }
            }

            if (languageToOutOfPolicyModules.Count != 0)
            {
                GenerateMessageParametersAndLog(context, languageToOutOfPolicyModules);
                return;
            }

            string[] sortedInPolicyCompilers = inPolicyCompilers.ToArray();
            Array.Sort(sortedInPolicyCompilers);

            // All linked modules of '{0}' satisfy configured policy (observed compilers: {1}).
            Result result = RuleUtilities.BuildResult(ResultKind.Pass,
                                                      context,
                                                      null,
                                                      nameof(RuleResources.BA2006_Pass),
                                                      context.TargetUri.GetFileName(),
                                                      string.Join(", ", sortedInPolicyCompilers));

            context.Logger.Log(this, result);
        }

        internal void GenerateMessageParametersAndLog(BinaryAnalyzerContext context,
                                                      SortedDictionary<Language, List<ObjectModuleDetails>> languageToBadModules)
        {
            s_sb ??= new StringBuilder();
            s_sb.Clear();

            var languages = new List<string>();
            foreach (KeyValuePair<Language, List<ObjectModuleDetails>> kp in languageToBadModules)
            {
                Language language = kp.Key;
                List<ObjectModuleDetails> outOfPolicyModules = kp.Value;

                s_sb.Append(outOfPolicyModules.CreateOutputCoalescedByCompiler());

                Version version = RetrieveMinimumCompilerVersion(context, language);
                languages.Add($"{language} ({version})");
            }

            string outOfPolicyModulesText = s_sb.ToString();
            string minimumRequiredCompilers = string.Join(", ", languages);

            Debug.Assert(!string.IsNullOrWhiteSpace(outOfPolicyModulesText) ||
                         !string.IsNullOrWhiteSpace(minimumRequiredCompilers));

            // '{0}' was compiled with one or more modules which were not built using
            // minimum required tool versions ({1}). More recent toolchains
            // contain mitigations that make it more difficult for an attacker to exploit
            // vulnerabilities in programs they produce. To resolve this issue, compile
            // and /or link your binary with more recent tools. If you are servicing a
            // product where the tool chain cannot be modified (e.g. producing a hotfix
            // for an already shipped version) ignore this warning. Modules built outside
            // of policy: {2}
            Result result = RuleUtilities.BuildResult(FailureLevel.Error,
                                                      context,
                                                      null,
                                                      nameof(RuleResources.BA2006_Error),
                                                      context.TargetUri.GetFileName(),
                                                      minimumRequiredCompilers,
                                                      outOfPolicyModulesText);
            context.Logger.Log(this, result);
        }

        private string BuildCompilerIdentifier(ObjectModuleDetails omDetails)
        {
            return omDetails.CompilerName + ":" + omDetails.Language + ":" + omDetails.CompilerBackEndVersion;
        }

        internal static Version RetrieveMinimumCompilerVersion(BinaryAnalyzerContext context, Language language)
        {
            switch (language)
            {
                case Language.C:
                case Language.Cxx:
                {
                    PEBinary target = context.PEBinary();
                    if (target.PE?.IsXBox == true)
                    {
                        return context.Policy.GetProperty(MinimumToolVersions).GetProperty(MinimumXboxCompilerVersion);
                    }
                    else
                    {
                        return (language == Language.C)
                            ? context.Policy.GetProperty(MinimumToolVersions).GetProperty(MinimumCCompilerVersion)
                            : context.Policy.GetProperty(MinimumToolVersions).GetProperty(MinimumCxxCompilerVersion);
                    }
                }

                /*
                    TODO: MikeFan (1/6/2022)
                    We need to take a step back and comprehensively review our compiler/language support.
                    https://github.com/Microsoft/binskim/issues/114

                    case Language.MASM:
                    {
                        minCompilerVersion =
                            context.Policy.GetProperty(MinimumToolVersions).GetVersionByKey(nameof(Language.MASM));
                        break;
                    }

                    case Language.CVTRES:
                    {
                        minCompilerVersion =
                            context.Policy.GetProperty(MinimumToolVersions).GetVersionByKey(nameof(Language.CVTRES));
                        break;
                    }

                    case Language.CSharp:
                    {
                        minCompilerVersion =
                            context.Policy.GetProperty(MinimumToolVersions).GetVersionByKey(nameof(Language.CSharp));
                        break;
                    }

                    Language data is not always included if it is only compiled with SymTagCompiland without SymTagCompilandDetails
                    https://docs.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/compilanddetails?view=vs-2022
                    Compiland information is split between symbols with a SymTagCompiland tag (low detail)
                    and a SymTagCompilandDetails tag (high detail).
                    case Language.Unknown:
                    {
                        minCompilerVersion =
                            context.Policy.GetProperty(MinimumToolVersions).GetVersionByKey(nameof(Language.Unknown));
                        break;
                    }
                    */

                default:
                {
                    break;
                }
            }

            return new Version();
        }

        public static Version Minimum(Version lhs, Version rhs)
        {
            return (lhs < rhs) ? lhs : rhs;
        }

        internal static StringToVersionMap BuildMinimumToolVersionsMap()
        {
            var result = new StringToVersionMap();
            result.SetProperty(MinimumCCompilerVersion, new Version(17, 0, 65501, 17013));
            result.SetProperty(MinimumCxxCompilerVersion, new Version(17, 0, 65501, 17013));
            result.SetProperty(MinimumUnknownCompilerVersion, new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
            result.SetProperty(MinimumXboxCompilerVersion, new Version(16, 0, 11886, 0));

            return result;
        }

        private static StringToVersionMap BuildAllowedLibraries()
        {
            var result = new StringToVersionMap();

            // Example entries
            result["libeay32.lib,unknown"] = new Version("0.0.0.0");
            // result["cplusplusExample.lib,cxx"] = new Version("1.0.0.0")
            // result["masmExample.lib,masm"] = new Version("1.0.0.0")

            return result;
        }
    }
}

namespace Microsoft.CodeAnalysis
{
    [Flags]
    public enum AdvancedMitigations
    {
        None = 0x0,
        Spectre = 0x1
    }
}
