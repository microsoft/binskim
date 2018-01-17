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

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class MitigatedVersion 
    {
        public MitigatedVersion()
        {
            compilerVersion = new Version(20, 0, 0, 0);
            QSpectre = false;
            d2specguard = false;
            debugCodeMitigated = false;
        }

        public MitigatedVersion(Version ver, bool spectre, bool d2, bool debug)
        {
            compilerVersion = ver;
            QSpectre = spectre;
            d2specguard = d2;
            debugCodeMitigated = debug;
        }
        
        public Version compilerVersion;
        public bool QSpectre;
        public bool d2specguard;
        public bool debugCodeMitigated;

        public override bool Equals(object obj)
        {
            return compilerVersion.Equals(obj);
        }

        public override int GetHashCode()
        {
            return compilerVersion.GetHashCode();
        }


        public static bool operator ==(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.compilerVersion == ver2.compilerVersion;  }
        public static bool operator !=(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.compilerVersion != ver2.compilerVersion; }

        public static bool operator <(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.compilerVersion < ver2.compilerVersion; }
        public static bool operator >(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.compilerVersion > ver2.compilerVersion; }

        public static bool operator <=(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.compilerVersion <= ver2.compilerVersion; }
        public static bool operator >=(MitigatedVersion ver1, MitigatedVersion ver2) { return ver1.compilerVersion >= ver2.compilerVersion; }
    }

    public class StringToMitigatedVersionMap : TypedPropertiesDictionary<MitigatedVersion>
    {
    }

    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule)), Export(typeof(IOptionsProvider))]
    public class BuildWithSpectreMitigation : BinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2024
        /// </summary>
        public override string Id { get { return RuleIds.BuildWithSpectreMitigationId; } }

        /// <summary>
        /// Application code should be compiled with the most up-to-date toolsets possible
        /// in order to take advantage of the most current compile-time security features.
        /// </summary>
        public override string FullDescription
        {
            // Application code should be compiled with the Spectre mitigations switch (/Qspectre) and toolsets that support it.
            get { return RuleResources.BA2024_BuildWithSpectreMitigation_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2024_Error),
                    nameof(RuleResources.BA2024_Error_BuildWithSpectreMitigation_BadCompilerVersion),
                    nameof(RuleResources.BA2024_Error_BuiildWithSpectreMitigation_UnrecognizedCompiler),
                    nameof(RuleResources.BA2024_Error_BuildWithSpectreMitigation_SpectreMitigationDisabled),
                    nameof(RuleResources.BA2024_Error_BuildWithSpectreMitigation_SpectreMitigationMissing),
                    nameof(RuleResources.BA2024_Pass),
                    nameof(RuleResources.BA2024_Pass_WithMASM),
                    nameof(RuleResources.BA2024_Warning_BuildWithSpectreMitigation_MASMDetected),
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

        private const string AnalyzerName = RuleIds.BuildWithSpectreMitigationId + "." + nameof(BuildWithSpectreMitigation);

        // /Qspectre support
        private const string VS2017_15_6_PREV4         = "VS2017_15.6_PREVIEW4";
        private const string VS2017_15_5_QSPECTREPATCH = "VS2017_15.5_/QSPECTRE_PATCH";
        private const string VS2017_15_0_PATCH         = "VS2017_15.0_PATCH";
        private const string VS2015_UPDATE3_PATCH      = "VS2015_UPDATE3_PATCH";

        // /d2guardspecload support
        private const string VS2017_15_5       = "VS2017_15.5";
        private const string VS2017_15_6_PREV1 = "VS2017_15.6_PREVIEW1";


        public static PerLanguageOption<StringToMitigatedVersionMap> MinimumToolVersions { get; } =
            new PerLanguageOption<StringToMitigatedVersionMap>(
                AnalyzerName, nameof(MinimumToolVersions), defaultValue: () => { return BuildMinimumToolVersionsMap(); });

        public static PerLanguageOption<StringToVersionMap> AllowedLibraries { get; } =
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

            TruncatedCompilandRecordList badModuleList = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList masmModuleList = new TruncatedCompilandRecordList();

            StringToVersionMap allowedLibraries = context.Policy.GetProperty(AllowedLibraries);
            StringToMitigatedVersionMap minimumCompilers = context.Policy.GetProperty(MinimumToolVersions);

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                // See if the item is in our skip list
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
                // or those that can appear in mixed binaries
                switch (omLanguage)
                {
                    case Language.C:
                    case Language.Cxx:
                        if (omDetails.WellKnownCompiler != WellKnownCompilers.MicrosoftNativeCompiler)
                        {
                            // TODO-paddymcd-MSFT: Add error for a non Microsoft C / C++ Compiler
                            // Add a place holder bad compiler record
                            // built with unrecognized compiler.
                            badModuleList.Add(
                                om.CreateCompilandRecordWithSuffix(
                                    String.Format(CultureInfo.InvariantCulture,
                                    RuleResources.BA2024_Error_BuiildWithSpectreMitigation_UnrecognizedCompiler)));
                            continue;
                        }
                        else
                        {
                            actualVersion = omDetails.CompilerVersion;
                        }
                        break;

                    case Language.MASM:
                        // built with an assembler, BinSkim cannot verify this file, please manually verify all code has the appropriate mitigations
                        masmModuleList.Add(
                            om.CreateCompilandRecordWithSuffix(
                                String.Format(CultureInfo.InvariantCulture,
                                              RuleResources.BA2024_Warning_BuildWithSpectreMitigation_MASMDetected)));
                        continue;

                    case Language.LINK:
                        // Linker is not involved in the mitigations, so no need to check version or switches at this time.
                        continue;
                    case Language.CVTRES:
                        // Resource compiler is not involved in the mitigations, so no need to check version or switches at this time.
                        continue;
                    case Language.HLSL:
                        // HLSL compiler is not involved in the mitigations, so no need to check version or switches at this time.
                        continue;

                        // Can mixed binaries (/clr) can contain non C++ compilands if they are linked in via netmodules
                        // .NET IL should be mitigated by the runtime if any mitigations are necessary
                        // At this point simply accept them as safe until this is disproven
                    case Language.CSharp:
                    case Language.MSIL:
                    case Language.ILASM:
                        continue;

                    case Language.Unknown:
                        // The linker may emit debug information for modules from static libraries that do not contribute to actual code.
                        // Currently these come back as Language.Unknown :(
                        // TODO-paddymcd-MSFT: Update the PDB handler to distinguish between empty contributions and an actual unknown Languages
                        continue;

                    default:
                        // Can mixed binaries (/clr) contain non C++ compilands?  I don't think so.  
                        // If this turns out to be the case we will have to accept those modules that we 
                        // know can only produce .NET IL as mitigated
                        // TODO-paddymcd-MSFT: Add warning for unrecognized languages
                        // built with unrecognized compiler.
                        badModuleList.Add(
                            om.CreateCompilandRecordWithSuffix(
                                String.Format(CultureInfo.InvariantCulture,
                                RuleResources.BA2024_Error_BuiildWithSpectreMitigation_UnrecognizedCompiler)));
                        continue;
                }

                // Get the appropriate compiler Version against which to check this compiland
                bool supportsQspectre = false;
                bool supportsd2guardspecload = false;

                // check that we are greater than or equal to the first fully supported release: 15.6 first
                Version omVer = omDetails.CompilerVersion;
                if (omVer >= minimumCompilers[VS2017_15_6_PREV4].compilerVersion)
                {
                    supportsQspectre = minimumCompilers[VS2017_15_6_PREV4].QSpectre;
                    supportsd2guardspecload = minimumCompilers[VS2017_15_6_PREV4].d2specguard;
                }
                else
                {
                    // Now check the patched versions that we match on the major, minor versions and then are greater than or equal to on the rest...
                    foreach (var compilerVersionEntry in minimumCompilers)
                    {
                        Version ver = compilerVersionEntry.Value.compilerVersion;

                        if (ver.Major == omVer.Major
                            && ver.Minor == omVer.Minor 
                            && ver >= omVer)
                        {
                            supportsQspectre = compilerVersionEntry.Value.QSpectre;
                            supportsd2guardspecload = compilerVersionEntry.Value.d2specguard;
                        }
                    }
                }

                if (!supportsd2guardspecload && !supportsQspectre)
                {
                    // built with a compiler version {0} that does not support the Spectre mitigations switch (/Qspectre).
                    badModuleList.Add(
                        om.CreateCompilandRecordWithSuffix(
                            String.Format(CultureInfo.InvariantCulture,
                            RuleResources.BA2024_Error_BuildWithSpectreMitigation_BadCompilerVersion,
                            omDetails.CompilerVersion)));
                    continue;
                }

                SwitchState QSpectreState = SwitchState.SwitchNotFound;
                SwitchState d2guardspecloadState = SwitchState.SwitchNotFound;

                // Go process the command line to check for switches
                if (supportsQspectre)
                {
                    QSpectreState = omDetails.GetSwitchState("/Qspectre", OrderOfPrecedence.LastWins);
                }

                if(supportsd2guardspecload)
                {
                    d2guardspecloadState = omDetails.GetSwitchState("/d2guardspecload", OrderOfPrecedence.LastWins);
                }

                // TODO-paddymcd-MSFT: Check all the /O optimization flags to determine if we are /Od or not
                // /Od may disable the Spectre Mitigations.

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
                    // built with the Spectre mitigations explicitly disabled.
                    badModuleList.Add(
                        om.CreateCompilandRecordWithSuffix(
                            string.Format(CultureInfo.InvariantCulture,
                            RuleResources.BA2024_Error_BuildWithSpectreMitigation_SpectreMitigationDisabled)));
                }
                else if(effectiveState == SwitchState.SwitchNotFound)
                {
                    // built with tools that support the Spectre mitigations but these have not been enabled.
                    badModuleList.Add(
                        om.CreateCompilandRecordWithSuffix(
                            string.Format(CultureInfo.InvariantCulture,
                            RuleResources.BA2024_Error_BuildWithSpectreMitigation_SpectreMitigationMissing)));
                }
            }

            ResultLevel analysisResult = ResultLevel.Pass;

            if (!masmModuleList.Empty)
            {
                // MASM files were detected in {0}.  MASM code cannot be verified by this tool.
                // MASM modules: {1}

                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Warning, context, null,
                    nameof(RuleResources.BA2024_Pass_WithMASM),
                    context.TargetUri.GetFileName(),
                    masmModuleList.CreateSortedObjectList()));

                // fall through in case we have errors as well
                analysisResult = ResultLevel.Warning;
            }

            if (!badModuleList.Empty)
            {
                // '{0}' was compiled with one or more modules without the Spectre mitigations enabled or were not built using tool versions containing the Spectre mitigations. 
                // More recent toolchains contain mitigations that make it more difficult for an attacker to exploit vulnerabilities in programs they produce. 
                // To resolve this issue, compile and/or link your binary with more recent tools. 
                // Modules built outside of policy: {1}
                context.Logger.Log(this, 
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                    nameof(RuleResources.BA2024_Error),
                        context.TargetUri.GetFileName(),
                        badModuleList.CreateSortedObjectList()));
                return;
            }

            if (analysisResult == ResultLevel.Pass)
            {
                // Only issue a Pass if there were no warnings
                // All modules linked into {0} have been verified to be compiled with Spectre mitigations enabled.
                context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                        nameof(RuleResources.BA2024_Pass),
                            context.TargetUri.GetFileName()));
            }
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
            result[VS2017_15_5] = new MitigatedVersion(new Version(19, 12, 25830, 2), false, true, false);
            // 15.6 preview 1 went out with the minor version not bumped: 19.12.25907.0
            // This will be caught by the 15.5 rtw check - we need the first 19.13 version (preview 2)
            result[VS2017_15_6_PREV1] = new MitigatedVersion(new Version(19, 13, 26029, 0), false, true, false);

            // /Qspectre and /d2guardspecload
            // TODO-paddymcd-MSFT: VS2017_15_6_PREV4 19.13.26115 is a placeholder internal build that doesn't yet support
            //                     /Qspectre.  Update this once we have the official build
            result[VS2017_15_6_PREV4] = new MitigatedVersion(new Version(19, 13, 26115, 0), true, true, false);
            // Add patched versions of the compiler as they become available.
            // result[VS2017_15_5_QSPECTREPATCH] = new MitigatedVersion(new Version(19, 12, ?, ?), true, true, false);
            // result[VS2017_15_0_PATCH] = new MitigatedVersion(new Version(19, 10, ?, ?), true, true, false);
            // result[VS2015_UPDATE3_PATCH] = new MitigatedVersion(new Version(19, 0, ?, ?), true, true, false);


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
}
