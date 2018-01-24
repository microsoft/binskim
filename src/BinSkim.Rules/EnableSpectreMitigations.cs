// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif;
using System.Text;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule)), Export(typeof(IOptionsProvider))]
    public class EnableSpectreMitigations : BinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2024
        /// </summary>
        public override string Id { get { return RuleIds.EnableSpectreMitigiations; } }

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
                MinimumToolVersions,
                AllowedLibraries
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.EnableSpectreMitigiations + "." + nameof(EnableSpectreMitigations);

        // /Qspectre support
        private const string VS2017_15_6_PREV4 = "VS2017_15.6_PREVIEW4";
        private const string VS2017_15_5_QSPECTREPATCH = "VS2017_15.5_/QSPECTRE_PATCH";
        private const string VS2017_15_0_PATCH = "VS2017_15.0_PATCH";
        private const string VS2015_UPDATE3_PATCH = "VS2015_UPDATE3_PATCH";

        // /d2guardspecload support
        private const string VS2017_15_5 = "VS2017_15.5";
        private const string VS2017_15_6_PREV1 = "VS2017_15.6_PREVIEW1";


        internal static PerLanguageOption<StringToMitigatedVersionMap> MinimumToolVersions { get; } =
            new PerLanguageOption<StringToMitigatedVersionMap>(
                AnalyzerName, nameof(MinimumToolVersions), defaultValue: () => { return BuildMinimumToolVersionsMap(); });

        internal static PerLanguageOption<StringToVersionMap> AllowedLibraries { get; } =
            new PerLanguageOption<StringToVersionMap>(
                AnalyzerName, nameof(AllowedLibraries), defaultValue: () => { return BuildAllowedLibraries(); });

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

            Pdb pdb = context.Pdb;
            if (pdb == null)
            {
                Errors.LogExceptionLoadingPdb(context, context.PdbParseException);
                return;
            }

            TruncatedCompilandRecordList mitigationNotEnabledModules = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList mitigationExplicitlyDisabledModules = new TruncatedCompilandRecordList();

            StringToVersionMap allowedLibraries = context.Policy.GetProperty(AllowedLibraries);
            StringToMitigatedVersionMap minimumCompilers = context.Policy.GetProperty(MinimumToolVersions);

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
                        // TODO: https://github.com/Microsoft/binskim/issues/115
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
                bool supportsQspectre = false;
                bool supportsd2guardspecload = false;

                // check that we are greater than or equal to the first fully supported release: 15.6 first
                Version omVer = omDetails.CompilerVersion;
                if (omVer >= minimumCompilers[VS2017_15_6_PREV4].CompilerVersion)
                {
                    supportsQspectre = minimumCompilers[VS2017_15_6_PREV4].QSpectreArgumentAvailable;
                    supportsd2guardspecload = minimumCompilers[VS2017_15_6_PREV4].D2GuardSpeclLoadArgumentAvailable;
                }
                else
                {
                    // Now check the patched versions that we match on the major, minor versions and then are greater than or equal to on the rest...
                    foreach (var compilerVersionEntry in minimumCompilers)
                    {
                        Version version = compilerVersionEntry.Value.CompilerVersion;

                        if (version.Major == omVer.Major &&
                            version.Minor == omVer.Minor &&
                            version >= omVer)
                        {
                            supportsQspectre = compilerVersionEntry.Value.QSpectreArgumentAvailable;
                            supportsd2guardspecload = compilerVersionEntry.Value.D2GuardSpeclLoadArgumentAvailable;
                        }
                    }
                }

                if (!supportsd2guardspecload && !supportsQspectre)
                {
                    // Built with a compiler version {0} that does not support the Spectre mitigations
                    // switch (/Qspectre). We do not report here. BA2006 will fire instead.
                    continue;
                }

                SwitchState QSpectreState = SwitchState.SwitchNotFound;
                SwitchState d2guardspecloadState = SwitchState.SwitchNotFound;

                // Go process the command line to check for switches.
                if (supportsQspectre)
                {
                    QSpectreState = omDetails.GetSwitchState("/Qspectre", OrderOfPrecedence.LastWins);
                }

                if (supportsd2guardspecload)
                {
                    // /d2xxxx options show up in the PDB without the d2 string.
                    // So search for just /guardspecload.
                    d2guardspecloadState = omDetails.GetSwitchState("/guardspecload", OrderOfPrecedence.LastWins);
                }

                // TODO: https://github.com/Microsoft/binskim/issues/118
                // Check all the /O optimization flags to determine if we are /Od or not
                // /Od may disable the Spectre Mitigations.

                // TODO: https://github.com/Microsoft/binskim/issues/119
                // We should flag cases where /d2guardspecload is enabled but the 
                // toolset supports /qSpectre (which should be preferred).

                SwitchState effectiveState = SwitchState.SwitchNotFound;

                // if either QSpectre or d2guardspecload are enabled AND neither is explicitly disabled then we are protected
                //      (use of both is confusing so issue an error in this scenario even though they are effectively the same switch)
                if ((QSpectreState == SwitchState.SwitchEnabled || d2guardspecloadState == SwitchState.SwitchEnabled) &&
                    (QSpectreState != SwitchState.SwitchDisabled && d2guardspecloadState != SwitchState.SwitchDisabled))
                {
                    effectiveState = SwitchState.SwitchEnabled;
                }

                if (effectiveState == SwitchState.SwitchDisabled)
                {
                    // Built with the Spectre mitigations explicitly disabled.
                    mitigationExplicitlyDisabledModules.Add(om.CreateCompilandRecord());
                }
                else if (effectiveState == SwitchState.SwitchNotFound)
                {
                    // Built with the Spectre mitigations explicitly disabled.
                    mitigationNotEnabledModules.Add(om.CreateCompilandRecord());
                }
            }

            string line;
            var sb = new StringBuilder();

            if (!mitigationExplicitlyDisabledModules.Empty)
            {
                line = string.Format(
                        RuleResources.BA2024_Error_SpectreMitigationExplicitlyDisabled,
                        mitigationExplicitlyDisabledModules.CreateSortedObjectList());
                sb.AppendLine(line);
            }

            if (!mitigationNotEnabledModules.Empty)
            {
                line = string.Format(
                        RuleResources.BA2024_Error_SpectreMitigationNotEnabled,
                        mitigationNotEnabledModules.CreateSortedObjectList());
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

        private static StringToMitigatedVersionMap BuildMinimumToolVersionsMap()
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

            var result = new StringToMitigatedVersionMap();

            // Only support /d2guardspecload
            result[VS2017_15_5] = new MitigatedVersion(new Version(19, 12, 25830, 2), CompilerMitigationSupport.D2GuardSpecLoadAvailable);

            // 15.6 preview 1 went out with the minor version not bumped: 19.12.25907.0
            // This will be caught by the 15.5 rtw check - we need the first 19.13 version (preview 2)
            result[VS2017_15_6_PREV1] = new MitigatedVersion(new Version(19, 13, 26029, 0), CompilerMitigationSupport.D2GuardSpecLoadAvailable);

            // /Qspectre and /d2guardspecload
            // TODO-paddymcd-MSFT: VS2017_15_6_PREV4 19.13.26115 is a placeholder internal build that doesn't yet support
            //                     /Qspectre.  Update this once we have the official build
            result[VS2017_15_6_PREV4] = new MitigatedVersion(
                new Version(19, 13, 26115, 0),
                CompilerMitigationSupport.QSpectreAvailable | CompilerMitigationSupport.D2GuardSpecLoadAvailable);

            // Add patched versions of the compiler as they become available.
/*
            result[VS2017_15_5_QSPECTREPATCH] = new MitigatedVersion(
                new Version(19, 10, ?, ?),
                CompilerMitigationSupport.QSpectreAvailable | CompilerMitigationSupport.D2GuardSpecLoadAvailable);

            result[VS2017_15_0_PATCH] = new MitigatedVersion(
                new Version(19, 10, ?, ?),
                CompilerMitigationSupport.QSpectreAvailable | CompilerMitigationSupport.D2GuardSpecLoadAvailable);

            result[VS2015_UPDATE3_PATCH] = new MitigatedVersion(
                new Version(19, 0, ?, ?),
                CompilerMitigationSupport.QSpectreAvailable | CompilerMitigationSupport.D2GuardSpecLoadAvailable);
*/

            return result;
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
    }

    [Flags]
    internal enum  CompilerMitigationSupport
    {
        None = 0x1,
        QSpectreAvailable = 0x2,
        D2GuardSpecLoadAvailable = 0x4,
        NonoptimizedCodeMitigated = 0x8,
    }

    internal class MitigatedVersion
    {
        public MitigatedVersion()
        {
            CompilerVersion = new Version(20, 0, 0, 0);
            _compiledMitigationSupport = CompilerMitigationSupport.None;
        }

        public MitigatedVersion(Version version, CompilerMitigationSupport compilerMitigationSupport)
        {
            CompilerVersion = version;
            _compiledMitigationSupport = compilerMitigationSupport;
        }

        CompilerMitigationSupport _compiledMitigationSupport;

        public Version CompilerVersion { get; private set; }

        public bool QSpectreArgumentAvailable
        {
            get
            {
                return (_compiledMitigationSupport & CompilerMitigationSupport.QSpectreAvailable) == CompilerMitigationSupport.QSpectreAvailable;
            }
        }

        public bool D2GuardSpeclLoadArgumentAvailable
        {
            get
            {
                return (_compiledMitigationSupport & CompilerMitigationSupport.D2GuardSpecLoadAvailable) == CompilerMitigationSupport.D2GuardSpecLoadAvailable;
            }
        }

        public bool NonoptimizedCodeMitigation
        {
            get
            {
                return (_compiledMitigationSupport & CompilerMitigationSupport.NonoptimizedCodeMitigated) == CompilerMitigationSupport.NonoptimizedCodeMitigated;
            }
        }

        public override bool Equals(object obj)
        {
            return CompilerVersion.Equals(obj);
        }

        public override int GetHashCode()
        {
            return CompilerVersion.GetHashCode();
        }

        public static bool operator ==(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.CompilerVersion == ver2.CompilerVersion; }
        public static bool operator !=(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.CompilerVersion != ver2.CompilerVersion; }

        public static bool operator <(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.CompilerVersion < ver2.CompilerVersion; }
        public static bool operator >(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.CompilerVersion > ver2.CompilerVersion; }

        public static bool operator <=(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.CompilerVersion <= ver2.CompilerVersion; }
        public static bool operator >=(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.CompilerVersion >= ver2.CompilerVersion; }
    }

    internal class StringToMitigatedVersionMap : TypedPropertiesDictionary<MitigatedVersion>
    {
    }
}