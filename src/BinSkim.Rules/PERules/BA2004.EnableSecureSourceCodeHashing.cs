// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

using static Microsoft.CodeAnalysis.BinaryParsers.CommandLineHelper;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class EnableSecureSourceCodeHashing : WindowsBinaryAndPdbSkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2004
        /// </summary>
        public const string MSVCPredefinedTypesFileName = "predefined C++ types (compiler internal)";
        public const string MSVCCliAttributeTypesFileName = "CLI attribute types (compiler internal)";
        public const string MSVCStandardApplicationFrameworkFileName = "stdafx.obj";
        public const string AssemblyAttributesObjFileName = "AssemblyAttributes.obj";
        public const string AssemblyInfoObjFileName = "AssemblyInfo.obj";


        public override string Id => RuleIds.EnableSecureSourceCodeHashing;

        public override bool LogPdbLoadException => false;

        public override MultiformatMessageString FullDescription => new MultiformatMessageString
        { Text = RuleResources.BA2004_EnableSecureSourceCodeHashing_Description };

        protected override ICollection<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2004_Pass),
            nameof(RuleResources.BA2004_Warning_NativeWithInsecureStaticLibraryCompilands),
            nameof(RuleResources.BA2004_Error_Managed),
            nameof(RuleResources.BA2004_Error_NativeWithInsecureDirectCompilands),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PE pe = context.PEBinary().PE;

            if (pe != null && pe.IsManaged && !pe.IsMixedMode)
            {
                AnalyzeManagedAssemblyAndPdb(context);
                return;
            }

            AnalyzeNativeBinaryAndPdb(context);
        }

        private void AnalyzeManagedAssemblyAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            if (pdb == null)
            {
                return;
            }

            ChecksumAlgorithmType algorithmType = target.PE.ManagedPdbSourceFileChecksumAlgorithm(pdb.FileType, pdb);
            if (algorithmType != ChecksumAlgorithmType.Sha256)
            {
                // '{0}' is a managed binary compiled with an insecure ({1}) source code hashing algorithm.
                // {1} is subject to collision attacks and its use can compromise supply chain integrity.
                // Pass '-checksumalgorithm:SHA256' on the csc.exe command-line or populate the project
                // <ChecksumAlgorithm> property with 'SHA256' to enable secure source code hashing.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Fail, context, null,
                    nameof(RuleResources.BA2004_Error_Managed),
                        context.CurrentTarget.Uri.GetFileName(),
                        algorithmType.ToString()));
                return;
            }

            // '{0}' is a {1} binary which was compiled with a secure (SHA-256)
            // source code hashing algorithm.
            context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2004_Pass),
                        context.CurrentTarget.Uri.GetFileName(),
                        "managed"));
        }

        public void AnalyzeNativeBinaryAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            if (pdb == null)
            {
                return;
            }

            var compilandsBinaryWithOneOrMoreInsecureFileHashes = new Dictionary<HashType, List<ObjectModuleDetails>>();
            var compilandsLibraryWithOneOrMoreInsecureFileHashes = new Dictionary<HashType, List<ObjectModuleDetails>>();

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                if (omDetails.Language != Language.C &&
                    omDetails.Language != Language.Cxx &&
                    omDetails.Language != Language.MASM)
                {
                    continue;
                }

                if (!omDetails.HasDebugInfo)
                {
                    continue;
                }

                if (IsLikelyUwpDummyObj(omDetails.Language, omDetails.Library, omDetails.Name))
                {
                    continue;
                }

                if (omDetails.Name.EndsWith(MSVCStandardApplicationFrameworkFileName) ||
                    omDetails.Name.EndsWith(AssemblyAttributesObjFileName) ||
                    omDetails.Name.EndsWith(AssemblyInfoObjFileName))
                {
                    continue;
                }

                bool isMsvc = (omDetails.WellKnownCompiler == WellKnownCompilers.MicrosoftC ||
                               omDetails.WellKnownCompiler == WellKnownCompilers.MicrosoftCxx);

                string pchHeaderFile = string.Empty;
                string pchFileName = string.Empty;

                if (isMsvc)
                {
                    // Check to see if the object was compiled using /Yc or /Yu for precompiled headers
                    string[] pchOptionSwitches = { "/Yc", "/Yu" };
                    if (omDetails.GetOptionValue(pchOptionSwitches, OrderOfPrecedence.FirstWins, ref pchHeaderFile) == true)
                    {
                        // Now check to see if a pch file name was specified using /Fp:
                        string[] pchFileNameOptions = { "/Fp" };
                        if (omDetails.GetOptionValue(pchFileNameOptions, OrderOfPrecedence.FirstWins, ref pchFileName) != true)
                        {
                            // no pch filename specified, so the filename defaults to the pchHeaderFile with the extension swapped to ".pch"
                            pchFileName = Path.ChangeExtension(pchHeaderFile, "pch");
                        }
                    }
                }

                CompilandRecord record = om.CreateCompilandRecord();

                foreach (DisposableEnumerableView<SourceFile> sfView in pdb.CreateSourceFileIterator(om))
                {
                    SourceFile sf = sfView.Value;

                    if (sf.HashType == HashType.None)
                    {
                        if (isMsvc)
                        {
                            // We know of 3 scenarios where this occurs today:
                            // If we encounter one of these, we should continue the loop to the next SourceFile,
                            // else fallthrough to the other checks for normal processing and errors.

                            string sfName = Path.GetFileName(sf.FileName);

                            // 1. Some compiler injected code that is listed as being in
                            // "predefined C++ types (compiler internal)" or "CLI attribute types(compiler internal)".
                            if (sfName == MSVCPredefinedTypesFileName || sfName == MSVCCliAttributeTypesFileName)
                            {
                                continue;
                            }
                            else if (sf.FileName.EndsWith(".winmd"))
                            {
                                // This is a Windows application reference
                                // assembly, a Win RT API 'metadata' file.
                                continue;
                            }
                            else if (sf.FileName.EndsWith(".pch"))
                            {
                                // Precompiled headers currently does not emit hash.
                                continue;
                            }
                        }
                    }

                    if (sf.HashType != HashType.SHA256)
                    {
                        Dictionary<HashType, List<ObjectModuleDetails>> compilands;

                        compilands = !string.IsNullOrEmpty(record.Library)
                            ? compilandsLibraryWithOneOrMoreInsecureFileHashes
                            : compilandsBinaryWithOneOrMoreInsecureFileHashes;

                        if (!compilands.TryGetValue(sf.HashType, out List<ObjectModuleDetails> objectModuleDetails))
                        {
                            objectModuleDetails = compilands[sf.HashType] = new List<ObjectModuleDetails>();
                        }

                        objectModuleDetails.Add(omDetails);
                    }

                    // We only need to check a single source file per compiland, as the relevant
                    // command-line options will be applied to all files in the translation unit.
                    break;
                }
            }

            if (compilandsLibraryWithOneOrMoreInsecureFileHashes.Count > 0 || compilandsBinaryWithOneOrMoreInsecureFileHashes.Count > 0)
            {
                if (compilandsLibraryWithOneOrMoreInsecureFileHashes.Count > 0)
                {
                    GenerateCompilandsAndLog(context, compilandsLibraryWithOneOrMoreInsecureFileHashes, FailureLevel.Warning);
                }

                if (compilandsBinaryWithOneOrMoreInsecureFileHashes.Count > 0)
                {
                    GenerateCompilandsAndLog(context, compilandsBinaryWithOneOrMoreInsecureFileHashes, FailureLevel.Error);
                }

                return;
            }

            // '{0}' is a {1} binary which was compiled with a secure (SHA-256)
            // source code hashing algorithm.
            context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass,
                                              context,
                                              region: null,
                                              nameof(RuleResources.BA2004_Pass),
                                              context.CurrentTarget.Uri.GetFileName(),
                                              "native"));
        }

        private void GenerateCompilandsAndLog(BinaryAnalyzerContext context, Dictionary<HashType, List<ObjectModuleDetails>> compilandsWithOneOrMoreInsecureFileHashes, FailureLevel failureLevel)
        {
            string message;
            List<ObjectModuleDetails> objectModuleDetails;

            if (compilandsWithOneOrMoreInsecureFileHashes.TryGetValue(HashType.None, out objectModuleDetails))
            {
                message = objectModuleDetails.CreateOutputCoalescedByCompiler("No hash value present");
                GenerateCompilandsAndLog(context, message, FailureLevel.Warning);
                compilandsWithOneOrMoreInsecureFileHashes.Remove(HashType.None);
            }

            if (compilandsWithOneOrMoreInsecureFileHashes.Count > 0)
            {
                string[] messages = new string[compilandsWithOneOrMoreInsecureFileHashes.Count];

                int hashTypeCount = 0;
                foreach (HashType hashType in compilandsWithOneOrMoreInsecureFileHashes.Keys)
                {
                    objectModuleDetails = compilandsWithOneOrMoreInsecureFileHashes[hashType];
                    messages[hashTypeCount++] = objectModuleDetails.CreateOutputCoalescedByCompiler(hashType.ToString());
                }

                message = string.Join(Environment.NewLine, messages);
                GenerateCompilandsAndLog(context, message, failureLevel);
            }
        }

        private void GenerateCompilandsAndLog(BinaryAnalyzerContext context, string message, FailureLevel failureLevel)
        {
            if (failureLevel == FailureLevel.Warning)
            {
                // '{0}' is a native binary that links one or more static libraries that include object files which were
                // hashed using an insecure checksum algorithm. Insecure checksum algorithms are subject to collision
                // attacks and its use can compromise supply chain integrity. Pass '/ZH:SHA_256' on the cl.exe
                // command-line to enable secure source code hashing. The following modules are out of policy: {1}
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(failureLevel,
                                              context,
                                              region: null,
                                              nameof(RuleResources.BA2004_Warning_NativeWithInsecureStaticLibraryCompilands),
                                              context.CurrentTarget.Uri.GetFileName(),
                                              message));
                return;
            }

            // '{0}' is a native binary that directly compiles and links one or more object files which were hashed
            // using an insecure checksum algorithm or for which no hash data is present. Insecure checksum
            // algorithms are subject to collision attacks and their use can compromise supply chain integrity.
            // Pass '/ZH:SHA_256' on the cl.exe command-line to enable secure source code hashing. The absence of
            // hash data may result from the use of #line directives. Passing /PH to generate #pragma file_hash
            // data when preprocessing may resolve the issue. The following modules are out of policy: {1}
            context.Logger.Log(this,
                RuleUtilities.BuildResult(failureLevel,
                                          context,
                                          region: null,
                                          nameof(RuleResources.BA2004_Error_NativeWithInsecureDirectCompilands),
                                          context.CurrentTarget.Uri.GetFileName(),
                                          message));
        }

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                //                RequiredCompilerWarnings,
            }.ToImmutableArray();
        }

        internal static bool IsLikelyUwpDummyObj(Language language, string library, string name) =>
            language == Language.MASM &&
            library != null &&
            library.Equals(name, StringComparison.Ordinal) &&
            library.Equals(@"c:\dummy.obj", StringComparison.Ordinal);
    }
}
