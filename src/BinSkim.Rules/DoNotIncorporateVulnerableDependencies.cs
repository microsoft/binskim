// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule)), Export(typeof(IOptionsProvider))]
    public class DoNotIncorporateVulnerableDependencies : BinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2002
        /// </summary>
        public override string Id { get { return RuleIds.DoNotIncorporateVulnerableDependenciesId; } }

        /// <summary>
        /// Binaries should not take dependencies on other code with known security vulnerabilities.
        /// </summary>
        public override string FullDescription
        {
            get { return RuleResources.BA2002_DoNotIncorporateVulnerableBinaries_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2002_Pass),
                    nameof(RuleResources.BA2002_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                VulnerableDependencies,
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.DoNotIncorporateVulnerableDependenciesId + "." + nameof(DoNotIncorporateVulnerableDependencies);

        public static PerLanguageOption<PropertiesDictionary> VulnerableDependencies { get; } =
            new PerLanguageOption<PropertiesDictionary>(
                AnalyzerName, nameof(VulnerableDependencies), defaultValue: () => { return BuildDefaultVulnerableDependenciesMap(); });

        private HashSet<string> _files;
        private Dictionary<string, VulnerableDependencyDescriptor> _filesToVulnerabilitiesMap;

        public override void Initialize(BinaryAnalyzerContext context)
        {
            if (context.Policy == null) { return; }

            _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _filesToVulnerabilitiesMap = new Dictionary<string, VulnerableDependencyDescriptor>();

            foreach (PropertiesDictionary dictionary in context.Policy.GetProperty(VulnerableDependencies).Values)
            {
                var descriptor = dictionary as VulnerableDependencyDescriptor;

                // This happens if we have deserialized settings from JSON rather than XML
                if (descriptor == null)
                {
                    descriptor = new VulnerableDependencyDescriptor(dictionary);
                }

                foreach (string fileHash in descriptor.FileHashes)
                {
                    _filesToVulnerabilitiesMap[fileHash] = descriptor;
                    _files.Add(fileHash.Split('#')[0]);
                }
            }

            return;
        }

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

            Dictionary<string, TruncatedCompilandRecordList> vulnerabilityToModules = new Dictionary<string, TruncatedCompilandRecordList>();
            TruncatedCompilandRecordList moduleList;

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails details = om.GetObjectModuleDetails();
                if (details.Language != Language.C && details.Language != Language.Cxx)
                {
                    continue;
                }

                if (!details.HasDebugInfo)
                {
                    continue;
                }

                foreach (DisposableEnumerableView<SourceFile> sfView in pdb.CreateSourceFileIterator(om))
                {
                    SourceFile sf = sfView.Value;
                    string fileName = Path.GetFileName(sf.FileName);

                    if (!_files.Contains(fileName) || sf.HashType == HashType.None)
                    {
                        continue;
                    }

                    string hash = fileName + "#" + BitConverter.ToString(sf.Hash);
                    VulnerableDependencyDescriptor descriptor;

                    if (_filesToVulnerabilitiesMap.TryGetValue(hash, out descriptor))
                    {
                        if (!vulnerabilityToModules.TryGetValue(descriptor.Id, out moduleList))
                        {
                            moduleList = vulnerabilityToModules[descriptor.Id] = new TruncatedCompilandRecordList();
                        }
                        moduleList.Add(om.CreateCompilandRecordWithSuffix(hash));
                    }
                }
            }

            if (vulnerabilityToModules.Count != 0)
            {
                foreach (string id in vulnerabilityToModules.Keys)
                {
                    moduleList = vulnerabilityToModules[id];
                    VulnerableDependencyDescriptor descriptor = (VulnerableDependencyDescriptor)context.Policy.GetProperty(VulnerableDependencies)[id];

                    // '{0}' was built with a version of {1} which is subject to the following issues: {2}. 
                    // To resolve this, {3}. The source files that triggered this were: {4}
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                            nameof(RuleResources.BA2002_Error),
                            context.TargetUri.GetFileName(),
                            descriptor.Name,
                            descriptor.VulnerabilityDescription,
                            descriptor.Resolution,
                            moduleList.CreateSortedObjectList()));
                }
                return;
            }

            // '{0}' does not incorporate any known vulnerable dependencies, as configured by current policy.
            context.Logger.Log(this, 
                RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                    nameof(RuleResources.BA2002_Pass),
                    context.TargetUri.GetFileName()));
        }

        private static PropertiesDictionary BuildDefaultVulnerableDependenciesMap()
        {
            var result = new PropertiesDictionary();

            var vulnerabilityDescriptor = new VulnerableDependencyDescriptor();

            vulnerabilityDescriptor.Name = "the Active Template Library (ATL)";
            vulnerabilityDescriptor.Id = "AtlVulnerability";
            vulnerabilityDescriptor.VulnerabilityDescription = "contains known remote execution bugs (see https://technet.microsoft.com/en-us/library/security/ms09-035.aspx).";
            vulnerabilityDescriptor.Resolution = "compile your binary using an up-to-date copy of ATL.";
            vulnerabilityDescriptor.FileHashes.Add("atlbase.h#FC-A7-3E-99-8B-D3-CC-E6-D6-28-75-F6-B4-27-DF-6E");
            vulnerabilityDescriptor.FileHashes.Add("atlimpl.cpp#7C-4C-5D-BE-B6-EF-CB-DF-AF-8E-54-E5-0E-C0-2A-FB");
            vulnerabilityDescriptor.FileHashes.Add("atlbase.h#31-F6-53-39-6A-51-B4-57-1E-F0-DD-C0-B3-54-8A-60");
            vulnerabilityDescriptor.FileHashes.Add("atlcom.h#95-EB-90-BE-CF-F8-DF-1B-3E-EC-79-0A-64-B4-96-54");
            vulnerabilityDescriptor.FileHashes.Add("atlcomcli.h#AC-EB-62-06-96-F2-ED-92-F8-F9-14-A0-50-48-80-25");
            vulnerabilityDescriptor.FileHashes.Add("atlcom.h#AE-5D-A4-A5-23-42-EA-F8-46-74-93-91-1C-4F-3B-93");
            vulnerabilityDescriptor.FileHashes.Add("atlcomcli.h#7B-C6-E4-10-50-D7-89-24-37-71-7F-1E-9D-97-84-B6");
            vulnerabilityDescriptor.FileHashes.Add("atlcom.h#0B-C1-32-3B-3B-19-84-64-07-F5-3A-7A-48-36-43-B0");
            vulnerabilityDescriptor.FileHashes.Add("atlcomcli.h#56-42-D5-31-BE-31-25-9B-E9-69-9F-2F-1F-68-CD-C2");
            vulnerabilityDescriptor.FileHashes.Add("atlcom.h#97-D2-E6-9A-A3-D5-F2-F1-BA-2A-51-A2-B6-C8-9A-4B");
            vulnerabilityDescriptor.FileHashes.Add("atlcomcli.h#A5-17-80-59-4D-4D-94-0C-68-0A-00-59-ED-6B-B3-1D");
            vulnerabilityDescriptor.FileHashes.Add("atlcom.h#97-D2-E6-9A-A3-D5-F2-F1-BA-2A-51-A2-B6-C8-9A-4B");
            vulnerabilityDescriptor.FileHashes.Add("atlcomcli.h#76-FB-17-FE-79-86-B9-7D-0E-09-97-85-9A-20-E9-4C");
            result[vulnerabilityDescriptor.Id] = vulnerabilityDescriptor;

            return result;
        }
    }
}
