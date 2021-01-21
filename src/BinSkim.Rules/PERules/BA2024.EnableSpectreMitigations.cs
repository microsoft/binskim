// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal struct CompilerVersionToMitigation
    {
        public Version MinimalSupportedVersion;
        public Version MaximumSupportedVersion;
        public CompilerMitigations SupportedMitigations;
    }

    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class EnableSpectreMitigations : WindowsBinaryAndPdbSkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2024
        /// </summary>
        public override string Id => RuleIds.EnableSpectreMitigations;

        /// <summary>
        /// Application code should be compiled with the most up-to-date toolsets possible
        /// in order to take advantage of the most current compile-time security features.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2024_EnableSpectreMitigations_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2024_Warning),
                    nameof(RuleResources.BA2024_Warning_OptimizationsDisabled),
                    nameof(RuleResources.BA2024_Warning_SpectreMitigationNotEnabled),
                    nameof(RuleResources.BA2024_Warning_SpectreMitigationExplicitlyDisabled),
                    nameof(RuleResources.BA2024_Pass),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)};

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                Reporting,
                AllowedLibraries,
                MitigatedCompilers,
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.EnableSpectreMitigations + "." + nameof(EnableSpectreMitigations);

        ////// /Qspectre support
        //private const string vs2017_15_6_prev4 = "vs2017_15.6_preview4";
        //private const string vs2017_15_5_qspectrepatch = "vs2017_15.5_/qspectre_patch";
        //private const string vs2017_15_0_patch = "vs2017_15.0_patch";
        //private const string vs2015_update3_patch = "vs2015_update3_patch";

        ////// /d2guardspecload support
        //private const string VS2017_15_5 = "VS2017_15.5";
        //private const string VS2017_15_6_PREV1 = "VS2017_15.6_PREVIEW1";

        internal static PerLanguageOption<StringToVersionMap> AllowedLibraries { get; } =
            new PerLanguageOption<StringToVersionMap>(
                AnalyzerName, nameof(AllowedLibraries), defaultValue: () => BuildAllowedLibraries());

        internal static PerLanguageOption<ReportingOptions> Reporting { get; } =
            new PerLanguageOption<ReportingOptions>(
                AnalyzerName, nameof(Reporting), defaultValue: () => CodeAnalysis.Sarif.ReportingOptions.Default);

        internal static PerLanguageOption<PropertiesDictionary> MitigatedCompilers { get; } =
            new PerLanguageOption<PropertiesDictionary>(AnalyzerName, nameof(MitigatedCompilers), defaultValue: () => BuildMitigatedCompilersData());

        // Internal so that we can reset this during testing.  In practice this should never get reset, but we use several different configs during unit tests.
        // Please do not access this field outside of this class and unit tests.
        internal static Dictionary<MachineFamily, CompilerVersionToMitigation[]> compilerData = null;

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsNativeUniversalWindowsPlatformBinary;
            if (portableExecutable.IsNativeUniversalWindowsPlatform) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();

            Machine reflectionMachineType = target.PE.Machine;

            // The current Machine enum does not have support for Arm64, so translate to our Machine enum
            var machineType = (ExtendedMachine)reflectionMachineType;

            if (!machineType.CanBeMitigated())
            {
                // QUESTION:
                // Machine HW is unsupported for mitigations...
                // should this be in the CanAnalyze() method or here and issue a warning?
                return;
            }

            Pdb pdb = target.Pdb;

            var masmModules = new List<ObjectModuleDetails>();
            var mitigationNotEnabledModules = new List<ObjectModuleDetails>();
            var mitigationDisabledInDebugBuild = new List<ObjectModuleDetails>();
            var mitigationExplicitlyDisabledModules = new List<ObjectModuleDetails>();

            StringToVersionMap allowedLibraries = context.Policy.GetProperty(AllowedLibraries);

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                // See if the item is in our skip list.
                if (!string.IsNullOrEmpty(om.Lib))
                {
                    string libFileName = string.Concat(Path.GetFileName(om.Lib), ",", omDetails.Language.ToString()).ToLowerInvariant();

                    if (allowedLibraries.TryGetValue(libFileName, out Version minAllowedVersion) &&
                        omDetails.CompilerBackEndVersion >= minAllowedVersion)
                    {
                        continue;
                    }
                }

                // We already opted-out of IL Only binaries, so only check for native languages
                // or those that can appear in mixed binaries.
                switch (omDetails.Language)
                {
                    case Language.C:
                    case Language.Cxx:
                    {
                        if (omDetails.WellKnownCompiler != WellKnownCompilers.MicrosoftNativeCompiler)
                        {
                            // TODO: https://github.com/Microsoft/binskim/issues/114
                            continue;
                        }
                        break;
                    }

                    case Language.MASM:
                    {
                        masmModules.Add(omDetails);
                        continue;
                    }

                    case Language.LINK:
                    {
                        // Linker is not involved in the mitigations, so no need to check version or switches at this time.
                        continue;
                    }

                    case Language.CVTRES:
                    {
                        // Resource compiler is not involved in the mitigations, so no need to check version or switches at this time.
                        continue;
                    }

                    case Language.HLSL:
                    {
                        // HLSL compiler is not involved in the mitigations, so no need to check version or switches at this time.
                        continue;
                    }

                    // Mixed binaries (/clr) can contain non C++ compilands if they are linked in via netmodules
                    // .NET IL should be mitigated by the runtime if any mitigations are necessary
                    // At this point simply accept them as safe until this is disproven.
                    case Language.CSharp:
                    case Language.MSIL:
                    case Language.ILASM:
                    {
                        continue;
                    }

                    case Language.Unknown:
                    {
                        // The linker may emit debug information for modules from static libraries that do not contribute to actual code.
                        // do not contribute to actual code. Currently these come back as Language.Unknown :(
                        // TODO: https://github.com/Microsoft/binskim/issues/116
                        continue;
                    }

                    default:
                    {
                        // TODO: https://github.com/Microsoft/binskim/issues/117
                        // Review unknown languages for this and all checks
                    }
                    continue;
                }

                // Get the appropriate compiler version against which to check this compiland.
                // check that we are greater than or equal to the first fully supported release: 15.6 first
                Version omVersion = omDetails.CompilerBackEndVersion;

                CompilerMitigations availableMitigations = GetAvailableMitigations(context, machineType, omVersion);

                if (availableMitigations == CompilerMitigations.None)
                {
                    // Built with a compiler version {0} that does not support any Spectre
                    // mitigations. We do not report here. BA2006 will fire instead.
                    continue;
                }
                string[] mitigationSwitches = new string[] { "/Qspectre", "/guardspecload" };

                SwitchState effectiveState;

                // Go process the command line to check for switches
                effectiveState = omDetails.GetSwitchState(mitigationSwitches, null, SwitchState.SwitchDisabled, OrderOfPrecedence.LastWins);

                if (effectiveState == SwitchState.SwitchDisabled)
                {
                    SwitchState QSpectreState = SwitchState.SwitchNotFound;
                    SwitchState d2guardspecloadState = SwitchState.SwitchNotFound;

                    if (availableMitigations.HasFlag(CompilerMitigations.QSpectreAvailable))
                    {
                        QSpectreState = omDetails.GetSwitchState(mitigationSwitches[0] /*"/Qspectre"*/ , OrderOfPrecedence.LastWins);
                    }

                    if (availableMitigations.HasFlag(CompilerMitigations.D2GuardSpecLoadAvailable))
                    {
                        // /d2xxxx options show up in the PDB without the d2 string
                        // So search for just /guardspecload
                        d2guardspecloadState = omDetails.GetSwitchState(mitigationSwitches[1] /*"/guardspecload"*/, OrderOfPrecedence.LastWins);
                    }

                    // TODO: https://github.com/Microsoft/binskim/issues/119
                    // We should flag cases where /d2guardspecload is enabled but the 
                    // toolset supports /qSpectre (which should be preferred).

                    if (QSpectreState == SwitchState.SwitchNotFound && d2guardspecloadState == SwitchState.SwitchNotFound)
                    {
                        // Built with tools that support the Spectre mitigations but these have not been enabled.
                        mitigationNotEnabledModules.Add(omDetails);
                    }
                    else
                    {
                        // Built with the Spectre mitigations explicitly disabled.
                        mitigationExplicitlyDisabledModules.Add(omDetails);
                    }

                    continue;
                }

                if (!availableMitigations.HasFlag(CompilerMitigations.NonoptimizedCodeMitigated))
                {
                    string[] OdSwitches = { "/Od" };
                    // These switches override /Od - there is no one place to find this information on msdn at this time.
                    string[] OptimizeSwitches = { "/O1", "/O2", "/Ox", "/Og" };

                    bool debugEnabled = false;

                    if (omDetails.GetSwitchState(OdSwitches, OptimizeSwitches, SwitchState.SwitchEnabled, OrderOfPrecedence.LastWins) == SwitchState.SwitchEnabled)
                    {
                        debugEnabled = true;
                    }

                    if (debugEnabled)
                    {
                        // Built with /Od which disables Spectre mitigations.
                        mitigationDisabledInDebugBuild.Add(omDetails);
                        continue;
                    }
                }
            }

            string line;
            var sb = new StringBuilder();

            if (mitigationExplicitlyDisabledModules.Count > 0)
            {
                // The following modules were compiled with Spectre
                // mitigations explicitly disabled: {0}
                line = string.Format(
                        RuleResources.BA2024_Warning_SpectreMitigationExplicitlyDisabled,
                        mitigationExplicitlyDisabledModules.CreateOutputCoalescedByLibrary());
                sb.AppendLine(line);
            }

            if (mitigationNotEnabledModules.Count > 0)
            {
                // The following modules were compiled with a toolset that supports 
                // /Qspectre but the switch was not enabled on the command-line: {0}
                line = string.Format(
                        RuleResources.BA2024_Warning_SpectreMitigationNotEnabled,
                        mitigationNotEnabledModules.CreateOutputCoalescedByLibrary());
                sb.AppendLine(line);
            }

            if (mitigationDisabledInDebugBuild.Count > 0)
            {
                // The following modules were compiled with optimizations disabled(/ Od),
                // a condition that disables Spectre mitigations: {0}
                line = string.Format(
                        RuleResources.BA2024_Warning_OptimizationsDisabled,
                        mitigationDisabledInDebugBuild.CreateOutputCoalescedByLibrary());
                sb.AppendLine(line);
            }

            if ((context.Policy.GetProperty(Reporting) & ReportingOptions.WarnIfMasmModulesPresent) == ReportingOptions.WarnIfMasmModulesPresent &&
                masmModules.Count > 0)
            {
                line = string.Format(
                        RuleResources.BA2024_Warning_MasmModulesDetected,
                        masmModules.CreateOutputCoalescedByLibrary());
                sb.AppendLine(line);
            }

            if (sb.Length > 0)
            {
                // '{0}' was compiled with one or more modules that do not properly enable code
                // generation mitigations for speculative execution side-channel attack (Spectre)
                // vulnerabilities. Spectre attacks can compromise hardware-based isolation,
                // allowing non-privileged users to retrieve potentially sensitive data from the
                // CPU cache. To resolve the issue, provide the /Qspectre switch on the compiler
                // command-line (or /d2guardspecload in cases where your compiler supports this
                // switch and it is not possible to update to a toolset that supports /Qspectre).
                // The following modules are out of policy: {1}
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                    nameof(RuleResources.BA2024_Warning),
                        context.TargetUri.GetFileName(),
                        sb.ToString()));
                return;
            }

            // All linked modules ‘{0}’ were compiled with mitigations enabled that help prevent Spectre (speculative execution side-channel attack) vulnerabilities.
            context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2024_Pass),
                        context.TargetUri.GetFileName()));
        }

        internal static Version GetClosestCompilerVersionWithSpectreMitigations(BinaryAnalyzerContext context, ExtendedMachine machine, Version omVersion)
        {
            Dictionary<MachineFamily, CompilerVersionToMitigation[]> compilerMitigationData = LoadCompilerDataFromConfig(context.Policy);
            MachineFamily machineFamily = machine.GetMachineFamily();

            if (!compilerMitigationData.ContainsKey(machineFamily))
            {
                // Mitigations are not supported on this platform at all.  No appropriate 'closest compiler version'.
                return null;
            }
            else
            {
                CompilerVersionToMitigation[] listOfMitigatedCompilers = compilerMitigationData[machineFamily];
                // If the compiler version is not supported, then either:
                // 1) it is earlier than any supported compiler version
                // 2) it is in-between two supported compiler versions (e.x. VS2017.1-4)--it is larger than some supported version numbers and smaller than others.
                // 3) it's greater than any of them.
                // We want to give users the 'next greatest' compiler version that supports the spectre mitigations--this should be the "smallest available upgrade."
                var previousMaximum = new Version(0, 0, 0, 0);
                for (int i = 0; i < listOfMitigatedCompilers.Length; i++)
                {
                    if (omVersion > previousMaximum &&
                        omVersion <= listOfMitigatedCompilers[i].MinimalSupportedVersion &&
                        (listOfMitigatedCompilers[i].SupportedMitigations & (CompilerMitigations.QSpectreAvailable | CompilerMitigations.D2GuardSpecLoadAvailable)) != 0)
                    {
                        return listOfMitigatedCompilers[i].MinimalSupportedVersion;
                    }
                    else
                    {
                        previousMaximum = listOfMitigatedCompilers[i].MaximumSupportedVersion;
                    }
                }

                // If we're here, we're in situation (3)--the compiler is greater than any we recognize.  We'll return the largest compiler we know supports Spectre mitigations.
                // With appropriate configuration (i.e. a catch-all entry with a maximum of *.*.*.* is present), we should never really hit this case.
                return listOfMitigatedCompilers[listOfMitigatedCompilers.Length - 1].MinimalSupportedVersion;
            }
        }

        /// <summary>
        /// Get the Spectre compiler mitigations available for a particular compiler version and machine type.
        /// </summary>
        internal static CompilerMitigations GetAvailableMitigations(BinaryAnalyzerContext context, ExtendedMachine machine, Version omVersion)
        {
            Dictionary<MachineFamily, CompilerVersionToMitigation[]> compilerMitigationData = LoadCompilerDataFromConfig(context.Policy);
            MachineFamily machineFamily = machine.GetMachineFamily();

            if (!compilerMitigationData.ContainsKey(machineFamily))
            {
                return CompilerMitigations.None;
            }
            else
            {
                CompilerVersionToMitigation[] listOfMitigatedCompilers = compilerMitigationData[machineFamily];
                for (int i = 0; i < listOfMitigatedCompilers.Length; i++)
                {
                    if (omVersion >= listOfMitigatedCompilers[i].MinimalSupportedVersion && omVersion < listOfMitigatedCompilers[i].MaximumSupportedVersion)
                    {
                        return listOfMitigatedCompilers[i].SupportedMitigations;
                    }
                }
            }
            return CompilerMitigations.None;
        }

        private static StringToVersionMap BuildAllowedLibraries()
        {
            var result = new StringToVersionMap();

            // Example entries
            // result["cExample.lib,c"] = new Version("1.0.0.0") 
            // result["cplusplusExample.lib,cxx"] = new Version("1.0.0.0")
            // result["masmExample.lib,masm"] = new Version("1.0.0.0")

            return result;
        }

        internal static PerLanguageOption<string> Description { get; } =
            new PerLanguageOption<string>(
                AnalyzerName, nameof(Description), defaultValue: () => string.Empty);

        private static PropertiesDictionary BuildMitigatedCompilersData()
        {
            // As per https://blogs.msdn.microsoft.com/vcblog/2018/01/15/spectre-mitigations-in-msvc/ 
            /*
            In current versions of the MSVC compiler, the /Qspectre switch only works on optimized code. 
            You should make sure to compile your code with any of the optimization switches (e.g., /O2 or /O1 but NOT /Od) to have the mitigation applied. 
            Similarly, inspect any code that uses #pragma optimize([stg], off). Work is ongoing now to make the /Qspectre mitigation work on unoptimized code. 

            AND

            What versions of MSVC support the /Qspectre switch?
            All versions of Visual Studio 2017 version 15.5 and all Previews of Visual Studio version 15.6 already include an undocumented switch, /d2guardspecload, that is currently equivalent to /Qspectre. 
            You can use /d2guardspecload to apply the same mitigations to your code. 
            Please update to using /Qspectre as soon as you get a compiler that supports the switch as the /Qspectre switch will be maintained with new mitigations going forward. 

            The /Qspectre switch will be available in MSVC toolsets included in all future releases of Visual Studio (including Previews).  
            We will also release updates to some existing versions of Visual Studio to include support for /Qspectre. 
            Releases of Visual Studio and Previews are announced on the Visual Studio Blog; update notifications are included in the Notification Hub. 
            Visual Studio updates that include support for /Qspectre will be announced on the Visual C++ Team Blog and the @visualc Twitter feed.

            We initially plan to include support for /Qspectre in the following:
            Visual Studio 2017 version 15.6 Preview 4
            An upcoming servicing update to Visual Studio 2017 version 15.5
            A servicing update to Visual Studio 2017 “RTW”
            A servicing update to Visual Studio 2015 Update 3

            If you’re using an older version of MSVC we strongly encourage you to upgrade to a more recent compiler for this and other security improvements that have been developed in the last few years. 
            Additionally, you’ll benefit from increased conformance, code quality, and faster compile times as well as many productivity improvements in Visual Studio.

             */

            // UPDATED version and support information: https://blogs.msdn.microsoft.com/vcblog/2018/04/09/spectre-mitigation-changes-in-visual-studio-2017-version-15-7-preview-3/
            /* 
             * 
            With Visual Studio 2017 version 15.7 Preview 3 we have two new features to announce with regards to our Spectre mitigations. 
            First, the /Qspectre switch is now supported regardless of the selected optimization level. 
            Second, we have provided Spectre-mitigated implementations of the Microsoft Visual C++ libraries.

             */

            var compilersData = new PropertiesDictionary();

            // Mitigations for x86 family of processors
            var x86Data = new PropertiesDictionary();
            var armData = new PropertiesDictionary();

            // Format of this data and the ranges:
            // ThrowIfMitigationDataIsInvalid requires a range here from the start of this support 
            //     to a version less than the compiler that implements the next level of mitigation support.

            // X86\X64 support
            // VS2015 15.0 Update 3 Versions
            //       D2GuardSpecLoad version will not be back-ported
            // This terminates at 19.0 due to gaps in support in the 19.10 and 19.11 compilers
            x86Data.Add("19.00.24232.0 - 19.0.*.*",
                (CompilerMitigations.QSpectreAvailable).ToString());

            // VS2017 RTM
            // This terminates at 19.10.25099 due to gaps in support in unreleased compilers
            x86Data.Add("19.10.25024.0 - 19.10.25099.*",
                (CompilerMitigations.D2GuardSpecLoadAvailable | CompilerMitigations.QSpectreAvailable).ToString());

            // VS2017 - 15.5.x
            x86Data.Add("19.12.25830.2 - 19.12.25834.*",
                (CompilerMitigations.D2GuardSpecLoadAvailable).ToString());

            // This terminates at 19.12.*.* due to a gap in support for 15.6 early previews
            x86Data.Add("19.12.25835.0 - 19.12.*.*",
                (CompilerMitigations.D2GuardSpecLoadAvailable | CompilerMitigations.QSpectreAvailable).ToString());

            // VS2017 - 15.6 Preview 3
            x86Data.Add("19.13.26029.0 - 19.13.26117.*",
                (CompilerMitigations.D2GuardSpecLoadAvailable).ToString());

            // Version flows into 15.7 as there is no gap in support for these switches moving forwards
            x86Data.Add("19.13.26118.0 - 19.14.26328.*",
                (CompilerMitigations.D2GuardSpecLoadAvailable | CompilerMitigations.QSpectreAvailable).ToString());

            // VS2017 15.7 Preview 3
            // Add first support for mitigation under /Od as per https://blogs.msdn.microsoft.com/vcblog/2018/04/09/spectre-mitigation-changes-in-visual-studio-2017-version-15-7-preview-3/
            // This assumes that future versions of Visual Studio (post 15.6) will always have these mitigations available.
            x86Data.Add("19.14.26329.0 - *.*.*.*",
                (CompilerMitigations.D2GuardSpecLoadAvailable | CompilerMitigations.QSpectreAvailable | CompilerMitigations.NonoptimizedCodeMitigated).ToString());

            // ARM\ARM64 support
            // VS2017 - 15.6 Preview 4
            armData.Add("19.13.26214.0 - 19.14.26328.*",
                (CompilerMitigations.D2GuardSpecLoadAvailable | CompilerMitigations.QSpectreAvailable).ToString());

            // VS2017 15.7 Preview 3
            armData.Add("19.14.26329.0 - *.*.*.*",
                (CompilerMitigations.D2GuardSpecLoadAvailable | CompilerMitigations.QSpectreAvailable | CompilerMitigations.NonoptimizedCodeMitigated).ToString());

            compilersData.Add(nameof(MachineFamily.X86), x86Data);
            compilersData.Add(nameof(MachineFamily.Arm), armData);

            return compilersData;
        }

        internal static Dictionary<MachineFamily, CompilerVersionToMitigation[]> LoadCompilerDataFromConfig(PropertiesDictionary policy)
        {
            if (compilerData == null)
            {
                compilerData = new Dictionary<MachineFamily, CompilerVersionToMitigation[]>();
                PropertiesDictionary configData = policy.GetProperty(MitigatedCompilers);
                foreach (string key in configData.Keys)
                {
                    var machine = (MachineFamily)Enum.Parse(typeof(MachineFamily), key); // Neaten this up.
                    compilerData.Add(machine, CreateSortedVersionDictionary((PropertiesDictionary)configData[key]));
                }
            }
            return compilerData;
        }

        internal static CompilerVersionToMitigation[] CreateSortedVersionDictionary(PropertiesDictionary versionList)
        {
            var mitigatedCompilerList = new List<CompilerVersionToMitigation>();
            foreach (string key in versionList.Keys)
            {
                string[] versions = key.Split('-').Select((s) => s.Replace("*", int.MaxValue.ToString())).ToArray();
                var mitigationData = new CompilerVersionToMitigation()
                {
                    MinimalSupportedVersion = new Version(versions[0]),
                    MaximumSupportedVersion = new Version(versions[1]),
                    SupportedMitigations = (CompilerMitigations)Enum.Parse(typeof(CompilerMitigations), versionList[key].ToString()),
                };
                mitigatedCompilerList.Add(mitigationData);
            }
            mitigatedCompilerList.Sort((a, b) => a.MinimalSupportedVersion.CompareTo(b.MinimalSupportedVersion));

            ThrowIfMitigationDataIsInvalid(mitigatedCompilerList);

            return mitigatedCompilerList.ToArray();
        }

        private static void ThrowIfMitigationDataIsInvalid(List<CompilerVersionToMitigation> compilerVersionToMitigation)
        {
            for (int i = 0; i < compilerVersionToMitigation.Count; i++)
            {
                // The start of each mitigation should be before the end.
                if (compilerVersionToMitigation[i].MaximumSupportedVersion < compilerVersionToMitigation[i].MinimalSupportedVersion)
                {
                    throw new InvalidOperationException(RuleResources.BA2024_InitializationException);
                }

                // Validate--we should not have overlapping ranges.
                // This intentionally allows for overlap of 1 version, as 'end' is not inclusive.
                if (i < compilerVersionToMitigation.Count - 1)
                {
                    if (compilerVersionToMitigation[i].MaximumSupportedVersion > compilerVersionToMitigation[i + 1].MinimalSupportedVersion)
                    {
                        throw new InvalidOperationException(RuleResources.BA2024_InitializationException);
                    }
                }
            }
        }
    }
}

// Currently the SARIF SDK requires this namespace in order to deserialize custom context object types
// The SDK needs to provide a mechanism for consumers to specify their own namespaces
// https://github.com/Microsoft/sarif-sdk/issues/758
namespace Microsoft.CodeAnalysis.Sarif
{
    public static class MitigationExtensions
    {
        public static MachineFamily GetMachineFamily(this ExtendedMachine extendedMachine)
        {
            return extendedMachine.IsArmFamily() ? MachineFamily.Arm :
                   extendedMachine.IsX86Family() ? MachineFamily.X86 :
                                                   MachineFamily.Unknown;
        }

        public static bool CanBeMitigated(this ExtendedMachine machine)
        {
            return IsArmFamily(machine) || IsX86Family(machine);
        }

        public static bool IsArmFamily(this ExtendedMachine machine)
        {
            bool isFamily = false;

            switch (machine)
            {
                case ExtendedMachine.Arm:
                case ExtendedMachine.Arm64:
                case ExtendedMachine.ArmThumb2:
                    isFamily = true;
                    break;
                default:
                    break;
            }

            return isFamily;
        }

        public static bool IsX86Family(this ExtendedMachine machine)
        {
            bool isFamily = false;

            switch (machine)
            {
                case ExtendedMachine.Amd64:
                case ExtendedMachine.I386:
                    isFamily = true;
                    break;
                default:
                    break;
            }

            return isFamily;
        }
    }

    // ARM64 is not a supported type in System.Reflection.PortableExecutable.Machine, so we need a way to express this.
    // We only care about machine types where the mitigations are available so this enum is very narrow.
    // Make this as source compatible as possible so we can remove this with minimal code changes
    // IMAGE_MACHINE_* values from winnt.h and
    // https://msdn.microsoft.com/en-us/library/windows/desktop/ms680547(v=vs.85).aspx#optional_header__image_only_

    public enum ExtendedMachine : ushort
    {
        Amd64 = 0x8664,
        Arm = 0x1c0,
        Arm64 = 0xaa64,
        ArmThumb2 = 0x1c4,
        I386 = 0x14c,
    }

    public enum MachineFamily
    {
        Unknown = 0,
        X86,
        Arm
    }

    [Flags]
    public enum ReportingOptions
    {
        Default = 0x0,
        WarnIfMasmModulesPresent = 0x1
    }

    [Flags]
    public enum CompilerMitigations
    {
        None = 0x0,
        QSpectreAvailable = 0x1,
        D2GuardSpecLoadAvailable = 0x2,
        NonoptimizedCodeMitigated = 0x4,
    }
}
