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
            // ugh
            compilerVersion = new Version(0, 0, 0, 0);
            QSpectre = false;
            d2specguard = false;
        }

        public MitigatedVersion(Version ver, bool spectre, bool d2)
        {
            compilerVersion = ver;
            QSpectre = spectre;
            d2specguard = d2;
        }
        
        public Version compilerVersion;
        public bool QSpectre;
        public bool d2specguard;

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
        /// BA2006
        /// </summary>
        public override string Id { get { return RuleIds.BuildWithSpectreMitigationId; } }

        /// <summary>
        /// Application code should be compiled with the most up-to-date toolsets possible
        /// in order to take advantage of the most current compile-time security features.
        /// </summary>
        public override string FullDescription
        {
            get { return RuleResources.BA2024_BuildWithSpectreMitigation_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.NotApplicable_InvalidMetadata),
                    nameof(RuleResources.BA2024_Error_BuildWithSpectreMitigation_SpectreMitigationDisabled),
                    nameof(RuleResources.BA2024_Error_BuildWithSpectreMitigation_BadCompilerVersion),
                    nameof(RuleResources.BA2024_Warning_BuildWithSpectreMitigation_MASMDetected),
                    nameof(RuleResources.BA2024_Pass),
                    nameof(RuleResources.BA2024_Pass_WithMASM)};
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

        private const string MIN_VC_QSPECTRE = "MinimumCompilerVersionForQspectre";
        private const string MIN_VC_15_5_D2 = "MinimumDev15_5CompilerVersionForD2Qspectre";
        private const string MIN_VC_15_D2 = "MinimumDev15CompilerVersionForD2Qspectre";
        private const string MIN_VC_14_D2 = "MinimumDev14CompilerVersionForD2Qspectre";
        private const string MIN_VC_12_D2 = "MinimumDev12CompilerVersionForD2Qspectre";

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

            var minCompilerVersions = context.Policy.GetProperty(MinimumToolVersions);

            TruncatedCompilandRecordList badModuleList = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList masmModuleList = new TruncatedCompilandRecordList();

            StringToVersionMap allowedLibraries = context.Policy.GetProperty(AllowedLibraries);
            StringToMitigatedVersionMap minimumCompilers = context.Policy.GetProperty(MinimumToolVersions);

            var sortedCompilerVersions = minimumCompilers.ToImmutableSortedSet();

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                if (omDetails.WellKnownCompiler != WellKnownCompilers.MicrosoftNativeCompiler)
                {
                    continue;
                }

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
                switch (omLanguage)
                {
                    case Language.C:
                    case Language.Cxx:
                        actualVersion = omDetails.CompilerVersion;
                        break;
                    case Language.MASM:
                        // built with a Microsoft assembler, BinSkim cannot verify this file, please manually verify all code has the appropriate mitigations
                        masmModuleList.Add(
                            om.CreateCompilandRecordWithSuffix(
                                String.Format(CultureInfo.InvariantCulture,
                                              RuleResources.BA2024_Warning_BuildWithSpectreMitigation_MASMDetected)));
                        continue;

                    default:
                        continue;
                }

                // Get the appropriate compiler Version against which to check this compiland
                bool supportsQSPectre = false;
                bool supportsd2guardspecload = false;

                // check that we are greater than or equal to the first fully supported release: 15.6 first
                Version omVer = omDetails.CompilerVersion;
                if (omVer >= minimumCompilers[MIN_VC_QSPECTRE].compilerVersion)
                {
                    supportsQSPectre = minimumCompilers[MIN_VC_QSPECTRE].QSpectre;
                    supportsd2guardspecload = minimumCompilers[MIN_VC_QSPECTRE].d2specguard;
                }
                else
                {
                    // Now check the patched versions (and the release!
                    foreach (var compilerVersionEntry in sortedCompilerVersions)
                    {
                        Version ver = compilerVersionEntry.Value.compilerVersion;

                        if (ver.Major == omVer.Major
                            && ver.Minor == omVer.Minor
                            && ver.Build >= omVer.Build
                            && ver.Revision >= omVer.Revision)
                        {
                            // Compiler version 
                            supportsQSPectre = compilerVersionEntry.Value.QSpectre;
                            supportsd2guardspecload = compilerVersionEntry.Value.d2specguard;
                        }
                    }
                }

                if (!supportsd2guardspecload && !supportsQSPectre)
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
                if (supportsQSPectre)
                {
                    QSpectreState = omDetails.GetSwitchState("/Qspectre", OrderOfPrecedence.LastWins);
                }

                if(supportsd2guardspecload)
                {
                    d2guardspecloadState = omDetails.GetSwitchState("/d2guardspecload", OrderOfPrecedence.LastWins);
                }

                SwitchState effectiveState = SwitchState.SwitchNotFound;

                // if either QSpectre or d2guardspecload are enabled AND neither is explicitly disabled then we are protected
                //      (use of both is confusing so issue an error in this scenario even though they are effectively the same switch)
                if ((QSpectreState == SwitchState.SwitchEnabled || d2guardspecloadState == SwitchState.SwitchEnabled) && 
                    (QSpectreState != SwitchState.SwitchDisabled && d2guardspecloadState != SwitchState.SwitchDisabled))
                {
                    effectiveState = SwitchState.SwitchEnabled;
                }

                if(effectiveState != SwitchState.SwitchEnabled)
                {
                    // built with the Spectre mitigations explicitly disabled (/Qspectre-).
                    badModuleList.Add(
                        om.CreateCompilandRecordWithSuffix(
                            string.Format(CultureInfo.InvariantCulture,
                            RuleResources.BA2024_Error_BuildWithSpectreMitigation_SpectreMitigationDisabled)));
                }
            }

            ResultLevel analysisResult = ResultLevel.Pass;

            if (!masmModuleList.Empty)
            {
                // All C/C++ modules linked into {0} have been verified to be compiled with Spectre mitigations enabled, but MASM files were also detected.  
                // MASM code cannot be verified by this tool.  
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
                // '{0}' was compiled with one or more modules which were not built using tool versions containing the Spectre mitigation switches.
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
            var result = new StringToMitigatedVersionMap();

            result[MIN_VC_QSPECTRE] = new MitigatedVersion(new Version(19, 6, 0, 0), true, true);
            result[MIN_VC_15_5_D2] = new MitigatedVersion(new Version(19, 5, 5, 0), true, true);
            result[MIN_VC_15_D2] = new MitigatedVersion(new Version(19, 0, 0, 0), false, true);
            result[MIN_VC_14_D2] = new MitigatedVersion(new Version(18, 0, 0, 0), false, true);
            result[MIN_VC_12_D2] = new MitigatedVersion(new Version(17, 0, 0, 0), false, true);

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
