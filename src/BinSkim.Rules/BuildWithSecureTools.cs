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
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule)), Export(typeof(IOptionsProvider))]
    public class BuildWithSecureTools : BinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2006
        /// </summary>
        public override string Id { get { return RuleIds.BuildWithSecureToolsId; } }

        /// <summary>
        /// Application code should be compiled with the most up-to-date toolsets possible
        /// in order to take advantage of the most current compile-time security features.
        /// </summary>
        public override string FullDescription
        {
            get { return RuleResources.BA2006_BuildWithSecureTools_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2006_Error_BadModule),
                    nameof(RuleResources.BA2006_Pass),
                    nameof(RuleResources.BA2006_Error),
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

        private const string AnalyzerName = RuleIds.BuildWithSecureToolsId + "." + nameof(BuildWithSecureTools);

        private const string MIN_COMPILER_VER = "MinimumCompilerVersion";
        private const string MIN_XBOX_COMPILER_VER = "MinimumXboxCompilerVersion";

        public static PerLanguageOption<StringToVersionMap> MinimumToolVersions { get; } =
            new PerLanguageOption<StringToVersionMap>(
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

            Version minCompilerVersion;

            if (context.PE.IsXBox)
            {
                minCompilerVersion = context.Policy.GetProperty(MinimumToolVersions)[MIN_XBOX_COMPILER_VER];
            }
            else
            {
                minCompilerVersion = context.Policy.GetProperty(MinimumToolVersions)[MIN_COMPILER_VER];
            }

            TruncatedCompilandRecordList badModuleList = new TruncatedCompilandRecordList();
            StringToVersionMap allowedLibraries = context.Policy.GetProperty(AllowedLibraries);

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
                Version minimumVersion;
                Language omLanguage = omDetails.Language;
                switch (omLanguage)
                {
                    case Language.C:
                    case Language.Cxx:
                        actualVersion = Minimum(omDetails.CompilerVersion, omDetails.CompilerFrontEndVersion);
                        minimumVersion = minCompilerVersion;
                        break;

                    default:
                        continue;
                }

                if (actualVersion < minimumVersion)
                {
                    // built with {0} compiler version {1} (Front end version: {2})
                    badModuleList.Add(
                        om.CreateCompilandRecordWithSuffix(
                            String.Format(CultureInfo.InvariantCulture,
                            RuleResources.BA2006_Error_BadModule,
                            omLanguage, omDetails.CompilerVersion, omDetails.CompilerFrontEndVersion)));
                }
            }

            if (!badModuleList.Empty)
            {
                // '{0}' was compiled with one or more modules which were not built using
                // minimum required tool versions (compiler version {1}). More recent toolchains
                // contain mitigations that make it more difficult for an attacker to exploit
                // vulnerabilities in programs they produce. To resolve this issue, compile
                // and /or link your binary with more recent tools. If you are servicing a
                // product where the tool chain cannot be modified (e.g. producing a hotfix
                // for an already shipped version) ignore this warning. Modules built outside
                // of policy: {2}
                context.Logger.Log(this, 
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                    nameof(RuleResources.BA2006_Error),
                        context.TargetUri.GetFileName(),
                        minCompilerVersion.ToString(),
                        badModuleList.CreateSortedObjectList()));
                return;
            }

            // All linked modules of '{0}' generated by the Microsoft front-end
            // satisfy configured policy (compiler minimum version {1}).
            context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                    nameof(RuleResources.BA2006_Pass),
                        context.TargetUri.GetFileName(),
                        minCompilerVersion.ToString()));
        }

        public static Version Minimum(Version lhs, Version rhs)
        {
            if (lhs < rhs)
            {
                return lhs;
            }
            else
            {
                return rhs;
            }
        }

        private static StringToVersionMap BuildMinimumToolVersionsMap()
        {
            var result = new StringToVersionMap();

            result[MIN_COMPILER_VER] = new Version(17, 0, 65501, 17016);
            result[MIN_XBOX_COMPILER_VER] = new Version(16, 0, 11886, 0);

            return result;
        }

        private static StringToVersionMap BuildAllowedLibraries()
        {
            StringToVersionMap result = new StringToVersionMap();
            result["xboxkrnl.lib,C"] = new Version("1.0.0.0");
            result["xboxkrnl.lib,Cxx"] = new Version("1.0.0.0");
            result["xboxkrnl.lib,MASM"] = new Version("1.0.0.0");
            result["adsiid.lib,C"] = new Version("15.0.30729.207");
            result["ahadmin.lib,Cxx"] = new Version("15.0.30729.165");
            result["amstrmid.lib,C"] = new Version("15.0.30729.207");
            result["aux_ulib.lib,C"] = new Version("15.0.30729.207");
            result["bits.lib,C"] = new Version("15.0.30729.207");
            result["certidl.lib,C"] = new Version("15.0.30729.207");
            result["clfsmgmt.lib,Cxx"] = new Version("15.0.30729.207");
            result["corguids.lib,C"] = new Version("16.0.30311.1");
            result["dinput8.lib,C"] = new Version("15.0.30729.165");
            result["dmoguids.lib,C"] = new Version("15.0.30729.207");
            result["dtchelp.lib,Cxx"] = new Version("15.0.30729.165");
            result["ehstorguids.lib,Cxx"] = new Version("15.0.30729.207");
            result["fci.lib,C"] = new Version("15.0.30729.165");
            result["fdi.lib,C"] = new Version("15.0.30729.165");
            result["fileextd.lib,MASM"] = new Version("9.0.30729.165");
            result["fileextd.lib,C"] = new Version("15.0.30729.165");
            result["format.lib,Cxx"] = new Version("16.0.30311.1");
            result["gdiplus.lib,Cxx"] = new Version("15.0.30729.165");
            result["gpmuuid.lib,C"] = new Version("15.0.30729.165");
            result["htmlhelp.lib,Cxx"] = new Version("15.0.30729.165");
            result["iepmapi.lib,Cxx"] = new Version("15.0.30729.165");
            result["ksguid.lib,C"] = new Version("15.0.30729.207");
            result["ksuser.lib,C"] = new Version("15.0.30729.207");
            result["locationapi.lib,Cxx"] = new Version("15.0.30729.207");
            result["mbnapi_uuid.lib,C"] = new Version("15.0.30729.165");
            result["mfuuid.lib,Cxx"] = new Version("15.0.30729.207");
            result["mfuuid.lib,C"] = new Version("15.0.30729.207");
            result["mmc.lib,Cxx"] = new Version("15.0.30729.165");
            result["mmc.lib,C"] = new Version("15.0.30729.165");
            result["mqoa.lib,C"] = new Version("15.0.30729.165");
            result["msdasc.lib,C"] = new Version("15.0.30729.165");
            result["mstask.lib,Cxx"] = new Version("15.0.30729.165");
            result["msxml2.lib,C"] = new Version("15.0.30729.165");
            result["msxml6.lib,C"] = new Version("15.0.30729.165");
            result["muiload.lib,Cxx"] = new Version("15.0.30729.207");
            result["muiload.lib,C"] = new Version("15.0.30729.207");
            result["odbccp32.lib,C"] = new Version("15.0.30729.165");
            result["oledb.lib,C"] = new Version("15.0.30729.165");
            result["osptk.lib,C"] = new Version("15.0.30729.165");
            result["photoacquireuid.lib,Cxx"] = new Version("15.0.30729.207");
            result["portabledeviceguids.lib,Cxx"] = new Version("15.0.30729.207");
            result["sapi.lib,C"] = new Version("15.0.30729.165");
            result["sbtsv.lib,C"] = new Version("15.0.30729.207");
            result["scrnsave.lib,C"] = new Version("15.0.30729.165");
            result["scrnsavw.lib,C"] = new Version("15.0.30729.165");
            result["searchsdk.lib,C"] = new Version("15.0.30729.207");
            result["sensorsapi.lib,Cxx"] = new Version("15.0.30729.207");
            result["shell32.lib,C"] = new Version("15.0.30729.207");
            result["srclient.lib,Cxx"] = new Version("15.0.30729.165");
            result["strmiids.lib,C"] = new Version("15.0.30729.207");
            result["strsafe.lib,C"] = new Version("15.0.30729.165");
            result["structuredquery.lib,C"] = new Version("15.0.30729.165");
            result["svcguid.lib,C"] = new Version("15.0.30729.165");
            result["taskschd.lib,C"] = new Version("15.0.30729.165");
            result["transcodeimageuid.lib,Cxx"] = new Version("15.0.30729.165");
            result["tspubplugincom.lib,C"] = new Version("15.0.30729.207");
            result["unicows.lib,C"] = new Version("15.0.30729.165");
            result["uuid.lib,C"] = new Version("15.0.30729.165");
            result["uuid.lib,Cxx"] = new Version("15.0.30729.207");
            result["vds_uuid.lib,C"] = new Version("15.0.30729.207");
            result["vpccominterfaces.lib,C"] = new Version("15.0.30729.207");
            result["vss_uuid.lib,C"] = new Version("15.0.30729.207");
            result["wbemuuid.lib,C"] = new Version("15.0.30729.165");
            result["wcmguid.lib,C"] = new Version("15.0.30729.165");
            result["wiaguid.lib,C"] = new Version("15.0.30729.207");
            result["windowscodecs.lib,Cxx"] = new Version("15.0.30729.207");
            result["windowssideshowguids.lib,Cxx"] = new Version("15.0.30729.207");
            result["winstrm.lib,C"] = new Version("15.0.30729.165");
            result["wmcodecdspuuid.lib,C"] = new Version("15.0.30729.207");
            result["workspaceax.lib,C"] = new Version("15.0.30729.207");
            result["ws2_32.lib,C"] = new Version("15.0.30729.165");
            result["wsbapp_uuid.lib,C"] = new Version("15.0.30729.165");
            result["wuguid.lib,C"] = new Version("15.0.30729.165");
            result["xaswitch.lib,Cxx"] = new Version("15.0.30729.165");
            result["comsupp.lib,Cxx"] = new Version("16.0.30319.1");
            result["comsuppd.lib,Cxx"] = new Version("16.0.30319.1");
            result["comsuppw.lib,Cxx"] = new Version("16.0.30319.1");
            result["comsuppwd.lib,Cxx"] = new Version("16.0.30319.1");
            result["delayimp.lib,Cxx"] = new Version("16.0.30319.1");
            result["libcmt.lib,Cxx"] = new Version("16.0.40219.1");
            result["libcmt.lib,C"] = new Version("16.0.40219.1");
            result["libcmt.lib,MASM"] = new Version("10.0.40219.1");
            result["libcmtd.lib,Cxx"] = new Version("16.0.40219.1");
            result["libcmtd.lib,C"] = new Version("16.0.40219.1");
            result["libcmtd.lib,MASM"] = new Version("10.0.40219.1");
            result["libcpmt.lib,Cxx"] = new Version("16.0.40219.1");
            result["libcpmt.lib,C"] = new Version("16.0.40219.1");
            result["libcpmt1.lib,Cxx"] = new Version("16.0.40219.1");
            result["libcpmt1.lib,C"] = new Version("16.0.40219.1");
            result["libcpmtd.lib,Cxx"] = new Version("16.0.40219.1");
            result["libcpmtd.lib,C"] = new Version("16.0.40219.1");
            result["libcpmtd0.lib,Cxx"] = new Version("16.0.40219.1");
            result["libcpmtd0.lib,C"] = new Version("16.0.40219.1");
            result["libcpmtd1.lib,Cxx"] = new Version("16.0.40219.1");
            result["libcpmtd1.lib,C"] = new Version("16.0.40219.1");
            result["msvcmrt.lib,Cxx"] = new Version("16.0.40219.1");
            result["msvcmrtd.lib,Cxx"] = new Version("16.0.40219.1");
            result["msvcprt.lib,Cxx"] = new Version("16.0.40219.1");
            result["msvcprtd.lib,Cxx"] = new Version("16.0.40219.1");
            result["msvcrt.lib,Cxx"] = new Version("16.0.40219.1");
            result["msvcrt.lib,MASM"] = new Version("10.0.40219.1");
            result["msvcrt.lib,C"] = new Version("16.0.40219.1");
            result["msvcrtd.lib,Cxx"] = new Version("16.0.40219.1");
            result["msvcrtd.lib,MASM"] = new Version("10.0.40219.1");
            result["msvcrtd.lib,C"] = new Version("16.0.40219.1");
            result["msvcurt.lib,Cxx"] = new Version("16.0.40219.1");
            result["msvcurtd.lib,Cxx"] = new Version("16.0.40219.1");
            result["pgobootrun.lib,Cxx"] = new Version("16.0.30319.1");
            result["pgort.lib,Cxx"] = new Version("16.0.30319.1");
            result["ptrustm.lib,Cxx"] = new Version("16.0.40219.1");
            result["ptrustmd.lib,Cxx"] = new Version("16.0.40219.1");
            result["ptrustu.lib,Cxx"] = new Version("16.0.40219.1");
            result["ptrustud.lib,Cxx"] = new Version("16.0.40219.1");
            result["runtmchk.lib,Cxx"] = new Version("16.0.30319.1");
            result["runtmchk.lib,C"] = new Version("16.0.30319.1");
            result["runtmchk.lib,MASM"] = new Version("10.0.30319.1");
            result["vcomp.lib,MASM"] = new Version("10.0.40219.1");
            result["vcompd.lib,MASM"] = new Version("10.0.40219.1");
            result["pgort.lib,MASM"] = new Version("10.0.30319.1");
            result["atl.lib,C"] = new Version("16.0.30319.1");
            result["atls.lib,C"] = new Version("16.0.30319.1");
            result["atlsd.lib,C"] = new Version("16.0.30319.1");
            result["mfc100.lib,C"] = new Version("16.0.30319.1");
            result["mfc100u.lib,C"] = new Version("16.0.30319.1");
            result["mfc100d.lib,C"] = new Version("16.0.30319.1");
            result["mfc100ud.lib,C"] = new Version("16.0.30319.1");
            result["mfcm100.lib,C"] = new Version("16.0.30319.1");
            result["mfcm100d.lib,C"] = new Version("16.0.30319.1");
            result["mfcm100u.lib,C"] = new Version("16.0.30319.1");
            result["mfcm100ud.lib,C"] = new Version("16.0.30319.1");
            result["mfcs100.lib,C"] = new Version("16.0.30319.1");
            result["mfcs100d.lib,C"] = new Version("16.0.30319.1");
            result["mfcs100u.lib,C"] = new Version("16.0.30319.1");
            result["mfcs100ud.lib,C"] = new Version("16.0.30319.1");
            result["nafxcw.lib,C"] = new Version("16.0.30319.1");
            result["nafxcwd.lib,C"] = new Version("16.0.30319.1");
            result["uafxcw.lib,C"] = new Version("16.0.30319.1");
            result["uafxcwd.lib,C"] = new Version("16.0.30319.1");
            result["atl.lib,Cxx"] = new Version("16.0.30319.1");
            result["atls.lib,Cxx"] = new Version("16.0.30319.1");
            result["atlsd.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfc100.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfc100u.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfc100d.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfc100ud.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfcm100.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfcm100d.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfcm100u.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfcm100ud.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfcs100.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfcs100d.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfcs100u.lib,Cxx"] = new Version("16.0.30319.1");
            result["mfcs100ud.lib,Cxx"] = new Version("16.0.30319.1");
            result["nafxcw.lib,Cxx"] = new Version("16.0.30319.1");
            result["nafxcwd.lib,Cxx"] = new Version("16.0.30319.1");
            result["uafxcw.lib,Cxx"] = new Version("16.0.30319.1");
            result["uafxcwd.lib,Cxx"] = new Version("16.0.30319.1");

            return result;
        }
    }
}
