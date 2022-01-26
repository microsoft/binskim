// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        public override string Id => RuleIds.EnableSecureSourceCodeHashing;

        public override bool LogPdbLoadException => false;

        public override MultiformatMessageString FullDescription => new MultiformatMessageString
        { Text = RuleResources.BA2004_EnableSecureSourceCodeHashing_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2004_Pass),
            nameof(RuleResources.BA2004_Warning_NativeWithInsecureStaticLibraryCompilands),
            nameof(RuleResources.BA2004_Error_Managed),
            nameof(RuleResources.BA2004_Error_NativeWithInsecureDirectCompilands),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
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

            if (target.PE.ManagedPdbSourceFileChecksumAlgorithm(pdb.FileType, pdb) != ChecksumAlgorithmType.Sha256)
            {
                // '{0}' is a managed binary compiled with an insecure (SHA-1) source code hashing algorithm.
                // SHA-1 is subject to collision attacks and its use can compromise supply chain integrity.
                // Pass '-checksumalgorithm:SHA256' on the csc.exe command-line or populate the project
                // <ChecksumAlgorithm> property with 'SHA256' to enable secure source code hashing.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Fail, context, null,
                    nameof(RuleResources.BA2004_Error_Managed),
                        context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' is a {1} binary which was compiled with a secure (SHA-256)
            // source code hashing algorithm.
            context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2004_Pass),
                        context.TargetUri.GetFileName(),
                        "managed"));
        }

        public void AnalyzeNativeBinaryAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            var compilandsBinaryWithOneOrMoreInsecureFileHashes = new List<ObjectModuleDetails>();
            var compilandsLibraryWithOneOrMoreInsecureFileHashes = new List<ObjectModuleDetails>();

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

                bool isMSVC = (omDetails.WellKnownCompiler == WellKnownCompilers.MicrosoftC ||
                               omDetails.WellKnownCompiler == WellKnownCompilers.MicrosoftCxx);

                string pchHeaderFile = string.Empty;
                string pchFileName = string.Empty;

                if (isMSVC)
                {
                    // Check to see if the object was compiled using /Yc or /Yu for precompiled headers
                    string[] pchOptionSwitches = { "/Yc", "/Yu" };
                    if (omDetails.GetOptionValue(pchOptionSwitches, OrderOfPrecedence.FirstWins, ref pchHeaderFile) == true)
                    {
                        // Now check to see if a pch file name was specified using /Fp:
                        string[] pchFileNameOptions = { "/Fp:" };
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
                        if (isMSVC)
                        {
                            // We know of 3 scenarios where this occurs today:
                            // If we encounter one of these, we should continue the loop to the next SourceFile,
                            // else fallthrough to the other checks for normal processing and errors.

                            string sfName = Path.GetFileName(sf.FileName);

                            // 1. Some compiler injected code that is listed as being in "predefined C++ types (compiler internal)"
                            if (sfName == MSVCPredefinedTypesFileName)
                            {
                                continue;
                            }
                            else if (pchFileName != string.Empty)
                            {
                                // 2. The file used to create a precompiled header using the /Yc switch
                                // TODO - We need a prepass on the library / final link to determine which file was
                                //        used to create the pch, as this is the file that will have a HashType.None
                                // 3. The pch file itself
                                if (sfName == Path.GetFileName(pchFileName))
                                {
                                    continue;
                                }
                                // TODO - check this against the filename used to create the pch.  For now just let it pass
                                else // if(sfName == pchCreationTUFileName)
                                {
                                    continue;
                                }
                            }
                        }
                    }

                    if (sf.HashType != HashType.SHA256)
                    {
                        if (!string.IsNullOrEmpty(record.Library))
                        {
                            compilandsLibraryWithOneOrMoreInsecureFileHashes.Add(omDetails);
                        }
                        else
                        {
                            compilandsBinaryWithOneOrMoreInsecureFileHashes.Add(omDetails);
                        }
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
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2004_Pass),
                        context.TargetUri.GetFileName(),
                        "native"));
        }

        private void GenerateCompilandsAndLog(BinaryAnalyzerContext context, List<ObjectModuleDetails> compilandsWithOneOrMoreInsecureFileHashes, FailureLevel failureLevel)
        {
            string compilands = compilandsWithOneOrMoreInsecureFileHashes.CreateOutputCoalescedByCompiler();

            //'{0}' is a native binary that links one or more object files which were hashed
            // using an insecure checksum algorithm (MD5). MD5 is subject to collision attacks
            // and its use can compromise supply chain integrity. Pass '/ZH:SHA-256' on the
            // cl.exe command-line to enable secure source code hashing. The following modules
            // are out of policy: {1}
            if (failureLevel == FailureLevel.Warning)
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(failureLevel,
                                              context,
                                              null,
                                              nameof(RuleResources.BA2004_Warning_NativeWithInsecureStaticLibraryCompilands),
                                              context.TargetUri.GetFileName(),
                                              compilands));
                return;
            }

            context.Logger.Log(this,
                RuleUtilities.BuildResult(failureLevel,
                                          context,
                                          null,
                                          nameof(RuleResources.BA2004_Error_NativeWithInsecureDirectCompilands),
                                          context.TargetUri.GetFileName(),
                                          compilands));
        }

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                //                RequiredCompilerWarnings,
            }.ToImmutableArray();
        }
    }
}
