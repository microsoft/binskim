// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;
using System.Runtime;
using System.Linq;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal struct CompilerVersionToMitigation
    {
        public Version Start;
        public Version End;
        public CompilerMitigations MitigationState;
    }

    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule)), Export(typeof(IOptionsProvider))]
    public class EnableSpectreMitigations : BinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2024
        /// </summary>
        public override string Id { get { return RuleIds.EnableSpectreMitigations; } }

        /// <summary>
        /// Application code should be compiled with the most up-to-date toolsets possible
        /// in order to take advantage of the most current compile-time security features.
        /// </summary>
        public override string FullDescription
        {
            // Application code should be compiled with the Spectre mitigations switch (/Qspectre) and toolsets that support it.
            get { return RuleResources.BA2024_EnableSpectreMitigations_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2024_Error),
                    nameof(RuleResources.BA2024_Error_OptimizationsDisabled),
                    nameof(RuleResources.BA2024_Error_SpectreMitigationNotEnabled),
                    nameof(RuleResources.BA2024_Error_SpectreMitigationExplicitlyDisabled),
                    nameof(RuleResources.BA2024_Pass),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)};
            }
        }

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

        // /Qspectre support
        private const string VS2017_15_6_PREV4 = "VS2017_15.6_PREVIEW4";
        private const string VS2017_15_5_QSPECTREPATCH = "VS2017_15.5_/QSPECTRE_PATCH";
        private const string VS2017_15_0_PATCH = "VS2017_15.0_PATCH";
        private const string VS2015_UPDATE3_PATCH = "VS2015_UPDATE3_PATCH";

        // /d2guardspecload support
        private const string VS2017_15_5 = "VS2017_15.5";
        private const string VS2017_15_6_PREV1 = "VS2017_15.6_PREVIEW1";
        
        internal static PerLanguageOption<StringToVersionMap> AllowedLibraries { get; } =
            new PerLanguageOption<StringToVersionMap>(
                AnalyzerName, nameof(AllowedLibraries), defaultValue: () => { return BuildAllowedLibraries(); });

        internal static PerLanguageOption<ReportingOptions> Reporting { get; } =
            new PerLanguageOption<ReportingOptions>(
                AnalyzerName, nameof(Reporting), defaultValue: () => { return CodeAnalysis.Sarif.ReportingOptions.Default; });

        internal static PerLanguageOption<PropertiesDictionary> MitigatedCompilers { get; } =
            new PerLanguageOption<PropertiesDictionary>(AnalyzerName, nameof(MitigatedCompilers), defaultValue: () => { return BuildMitigatedCompilersData(); });

        private static Dictionary<MachineFamily, CompilerVersionToMitigation[]> compilerData = null;
        
        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyManagedAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;

            Machine reflectionMachineType = context.PE.Machine;

            // The current Machine enum does not have support for Arm64, so translate to our Machine enum
            ExtendedMachine machineType = (ExtendedMachine)reflectionMachineType;
            
            if (!machineType.CanBeMitigated())
            {
                // QUESTION:
                // Machine HW is unsupported for mitigations...
                // should this be in the CanAnalyze() method or here and issue a warning?
                return;
            }

            Pdb pdb = context.Pdb;
            if (pdb == null)
            {
                Errors.LogExceptionLoadingPdb(context, context.PdbParseException);
                return;
            }

            TruncatedCompilandRecordList masmModules = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList mitigationNotEnabledModules = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList mitigationDisabledInDebugBuild = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList mitigationExplicitlyDisabledModules = new TruncatedCompilandRecordList();

            StringToVersionMap allowedLibraries = context.Policy.GetProperty(AllowedLibraries);

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                // See if the item is in our skip list.
                if (!string.IsNullOrEmpty(om.Lib))
                {
                    string libFileName = string.Concat(System.IO.Path.GetFileName(om.Lib), ",", omDetails.Language.ToString()).ToLowerInvariant();
                    Version minAllowedVersion;

                    if (allowedLibraries.TryGetValue(libFileName, out minAllowedVersion) &&
                        omDetails.CompilerVersion >= minAllowedVersion)
                    {
                        continue;
                    }
                }

                Version actualVersion;
                Language omLanguage = omDetails.Language;

                // We already opted-out of IL Only binaries, so only check for native languages
                // or those that can appear in mixed binaries.
                switch (omLanguage)
                {
                    case Language.C:
                    case Language.Cxx:
                    {
                        if (omDetails.WellKnownCompiler != WellKnownCompilers.MicrosoftNativeCompiler)
                        {
                            // TODO: https://github.com/Microsoft/binskim/issues/114
                            continue;
                        }
                        else
                        {
                            actualVersion = omDetails.CompilerVersion;
                        }
                        break;
                    }

                    case Language.MASM:
                    {
                        masmModules.Add(om.CreateCompilandRecord());
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
                Version omVersion = omDetails.CompilerVersion;
                
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
                        mitigationNotEnabledModules.Add(om.CreateCompilandRecord());
                    }
                    else
                    {
                        // Built with the Spectre mitigations explicitly disabled.
                        mitigationExplicitlyDisabledModules.Add(om.CreateCompilandRecord());
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
                        mitigationDisabledInDebugBuild.Add(om.CreateCompilandRecord());
                        continue;
                    }
                }
            }

            string line;
            var sb = new StringBuilder();

            if (!mitigationExplicitlyDisabledModules.Empty)
            {
                // The following modules were compiled with Spectre
                // mitigations explicitly disabled: {0}
                line = string.Format(
                        RuleResources.BA2024_Error_SpectreMitigationExplicitlyDisabled,
                        mitigationExplicitlyDisabledModules.CreateSortedObjectList());
                sb.AppendLine(line);
            }

            if (!mitigationNotEnabledModules.Empty)
            {
                // The following modules were compiled with a toolset that supports 
                // /Qspectre but the switch was not enabled on the command-line: {0}
                line = string.Format(
                        RuleResources.BA2024_Error_SpectreMitigationNotEnabled,
                        mitigationNotEnabledModules.CreateSortedObjectList());
                sb.AppendLine(line);
            }

            if (!mitigationDisabledInDebugBuild.Empty)
            {
                // The following modules were compiled with optimizations disabled(/ Od),
                // a condition that disables Spectre mitigations: {0}
                line = string.Format(
                        RuleResources.BA2024_Error_OptimizationsDisabled,
                        mitigationDisabledInDebugBuild.CreateSortedObjectList());
                sb.AppendLine(line);
            }

            if ((context.Policy.GetProperty(Reporting) & ReportingOptions.WarnIfMasmModulesPresent) == ReportingOptions.WarnIfMasmModulesPresent &&
                !masmModules.Empty)
            {
                line = string.Format(
                        RuleResources.BA2024_Error_MasmModulesDetected,
                        masmModules.CreateSortedObjectList());
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
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                    nameof(RuleResources.BA2024_Error),
                        context.TargetUri.GetFileName(),
                        sb.ToString()));
                return;
            }

            // All linked modules ‘{0}’ were compiled with mitigations enabled that help prevent Spectre (speculative execution side-channel attack) vulnerabilities.
            context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                    nameof(RuleResources.BA2024_Pass),
                        context.TargetUri.GetFileName()));
        }

        /// <summary>
        /// Get the Spectre compiler compiler mitigations available for a particular compiler version and machine type.
        /// </summary>
        internal static CompilerMitigations GetAvailableMitigations(BinaryAnalyzerContext context, ExtendedMachine machine, Version omVersion)
        {
            var compilerMitigationData = LoadCompilerDataFromConfig(context.Policy);
            var machineFamily = machine.GetMachineFamily();

            if (!compilerMitigationData.ContainsKey(machineFamily))
            {
                return CompilerMitigations.None;
            }
            else
            {
                var listOfMitigatedCompilers = compilerMitigationData[machineFamily];
                for (int i = 0; i < listOfMitigatedCompilers.Length; i++)
                {
                    if (omVersion >= listOfMitigatedCompilers[i].Start && omVersion < listOfMitigatedCompilers[i].End)
                    {
                        return listOfMitigatedCompilers[i].MitigationState;
                    }
                }
            }
            return CompilerMitigations.None;
        }

        /// <summary>
        /// Get the lowest compiler version that supports Spectre mitigations.
        /// 
        /// Potential TODO during Update QSpectre minimum version once we have the official build. 
        ///       https://github.com/Microsoft/binskim/issues/134
        /// Should we instead be getting the minimum for a particular Visual Studio release?
        /// E.x. minimum compiler version for VS2015 if they are using a compiler below 2015.3 once those are released
        /// otherwise minimum compiler in VS2017 that supports these mitigations?
        /// </summary>
        internal static Version GetMostCurrentCompilerVersionsWithSpectreMitigations(BinaryAnalyzerContext context, ExtendedMachine machine)
        {
            var compilerMitigationData = LoadCompilerDataFromConfig(context.Policy);
            if (!compilerMitigationData.ContainsKey(machine.GetMachineFamily()))
            {
                // No compiler data for a compiler that supports the mitigations on this architecture.
                return new Version(0, 0, 0, 0);
            }
            else
            {
                var listOfCompilers = compilerMitigationData[machine.GetMachineFamily()];
                for (int i = 0; i < listOfCompilers.Length; i++)
                {
                    if (listOfCompilers[i].MitigationState != CompilerMitigations.None)
                    {
                        return listOfCompilers[i].Start;
                    }
                }
                // No compiler data for a compiler that supports the mitigations on this architecture
                return new Version(0, 0, 0, 0);
            }
        }

        private static StringToVersionMap BuildAllowedLibraries()
        {
            StringToVersionMap result = new StringToVersionMap();

            // Example entries
            // result["cExample.lib,c"] = new Version("1.0.0.0") 
            // result["cplusplusExample.lib,cxx"] = new Version("1.0.0.0")
            // result["masmExample.lib,masm"] = new Version("1.0.0.0")

            return result;
        }

        internal static PerLanguageOption<string> Description { get; } =
            new PerLanguageOption<string>(
                AnalyzerName, nameof(Description), defaultValue: () => { return String.Empty; });
        
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

            var compilersData = new PropertiesDictionary();

            // Mitigations for x86 family of processors
            var x86Data = new PropertiesDictionary();

            // VS2015 15.0 Update 3 Versions
            // TODO: Update QSpectre minimum version once we have the official build. 
            //       https://github.com/Microsoft/binskim/issues/134
            //    
            //       D2GuardSpecLoad version will not be back-ported
            x86Data.Add("19.0.*.* - 19.0.*.*", 
                (CompilerMitigations.QSpectreAvailable).ToString());

            // VS2017 RTM
            // TODO: Update QSpectre minimum version once we have the official build. 
            //       https://github.com/Microsoft/binskim/issues/134
            //    
            //       D2GuardSpecLoad version will not be back-ported
            x86Data.Add("19.10.*.* - 19.10.*.*", 
                (CompilerMitigations.QSpectreAvailable).ToString());

            // VS2017 - 15.5
            // TODO: Update QSpectre minimum version once we have the official build. 
            //       https://github.com/Microsoft/binskim/issues/134
            x86Data.Add("19.12.25830.2-19.12.*.*", 
                (CompilerMitigations.D2GuardSpecLoadAvailable).ToString());
            x86Data.Add("19.12.*.* - 19.12.*.*", 
                (CompilerMitigations.QSpectreAvailable | CompilerMitigations.D2GuardSpecLoadAvailable).ToString());

            // VS2017 - 15.6 Preview
            x86Data.Add("19.13.26029.0 - 19.13.26029.*", 
                (CompilerMitigations.D2GuardSpecLoadAvailable).ToString());

            // Update when we have an official build supporting QSpectre.
            x86Data.Add("19.13.*.* - 19.13.*.*", 
                (CompilerMitigations.QSpectreAvailable | CompilerMitigations.D2GuardSpecLoadAvailable).ToString());

            compilersData.Add(MachineFamily.X86.ToString(), x86Data);

            // TODO--Update when ARM mitigations are available.
            compilersData.Add(MachineFamily.Arm.ToString(), new PropertiesDictionary());

            return compilersData;
        }

        internal static Dictionary<MachineFamily, CompilerVersionToMitigation[]> LoadCompilerDataFromConfig(PropertiesDictionary policy)
        {
            if (compilerData == null)
            {
                compilerData = new Dictionary<MachineFamily, CompilerVersionToMitigation[]>();
                PropertiesDictionary configData = policy.GetProperty(MitigatedCompilers);
                foreach (var key in configData.Keys)
                {
                    MachineFamily machine = (MachineFamily)Enum.Parse(typeof(MachineFamily), key); // Neaten this up.
                    compilerData.Add(machine, CreateSortedVersionDictionary((PropertiesDictionary)configData[key]));
                }
            }
            return compilerData;
        }
        
        internal static CompilerVersionToMitigation[] CreateSortedVersionDictionary(PropertiesDictionary versionList)
        {
            List<CompilerVersionToMitigation> mitigatedCompilerList = new List<CompilerVersionToMitigation>();
            foreach (var key in versionList.Keys)
            {
                string[] versions = key.Split('-').Select((s) => { return s.Replace("*", int.MaxValue.ToString()); }).ToArray();
                var mitigationData = new CompilerVersionToMitigation()
                {
                    Start = new Version(versions[0]),
                    End = new Version(versions[1]),
                    MitigationState = (CompilerMitigations)Enum.Parse(typeof(CompilerMitigations), versionList[key].ToString()),
                };
                mitigatedCompilerList.Add(mitigationData);
            }
            mitigatedCompilerList.Sort((a, b) => a.Start.CompareTo(b.Start));

            ThrowIfMitigationDataIsInvalid(mitigatedCompilerList);

            return mitigatedCompilerList.ToArray();
        }
        
        private static void ThrowIfMitigationDataIsInvalid(List<CompilerVersionToMitigation> compilerVersionToMitigation)
        {
            for (int i = 0; i < compilerVersionToMitigation.Count; i++)
            {
                // The start of each mitigation should be before the end.
                if (compilerVersionToMitigation[i].End < compilerVersionToMitigation[i].Start)
                {
                    throw new InvalidOperationException(RuleResources.BA2024_InitializationException);
                }

                // Validate--we should not have overlapping ranges.
                // This intentionally allows for overlap of 1 version, as 'end' is not inclusive.
                if (i < compilerVersionToMitigation.Count - 1)
                {
                    if (compilerVersionToMitigation[i].End > compilerVersionToMitigation[i + 1].Start)
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
            if (extendedMachine.IsArmFamily()) { return MachineFamily.Arm; }

            if (extendedMachine.IsX86Family()) { return MachineFamily.X86; }

            return MachineFamily.Unknown;
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