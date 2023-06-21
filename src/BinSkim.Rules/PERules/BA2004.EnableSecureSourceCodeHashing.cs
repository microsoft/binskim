// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Reflection.Metadata.Ecma335;

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
            nameof(RuleResources.BA2004_Warning_NativeWithNoHashStaticLibraryCompilands),
            nameof(RuleResources.BA2004_Warning_NativeWithInsecureStaticLibraryCompilands),
            nameof(RuleResources.BA2004_Error_Managed),
            nameof(RuleResources.BA2004_Error_NativeWithInsecureDirectCompilands),
            nameof(RuleResources.BA2004_Warning_NativeWithNoHashDirectCompilands),
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

            var compilandsLibraryWithOneOrMoreInsecureFileHashes = new List<ObjectModuleDetails>();
            var compilandsLibraryWithOneOrMoreNoFileHashes = new List<ObjectModuleDetails>();

            var compilandsBinaryWithOneOrMoreInsecureFileHashes = new List<ObjectModuleDetails>();
            var compilandsBinaryWithOneOrMoreNoFileHashes = new List<ObjectModuleDetails>();

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
                        if (isMsvc)
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
                            else if (sf.FileName.EndsWith(".winmd"))
                            {
                                // This is a Windows application reference
                                // assembly, a Win RT API 'metadata' file.
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
                        // Adding the `MD5` or `SHA1` etc, comment delimited by a backquote/backtick (`) to the library name
                        string libraryNameWithHashComment = string.Format($"{omDetails.Library} `{sf.HashType}`");
                        var objectModuleDetailsWithHashComment = new ObjectModuleDetails(omDetails.Name, libraryNameWithHashComment, omDetails.CompilerName, omDetails.CompilerFrontEndVersion, omDetails.CompilerBackEndVersion, omDetails.RawCommandLine, omDetails.Language, omDetails.HasSecurityChecks, omDetails.HasDebugInfo);

                        if (!string.IsNullOrEmpty(record.Library))
                        {
                            if (sf.HashType != HashType.None)
                            {
                                compilandsLibraryWithOneOrMoreInsecureFileHashes.Add(objectModuleDetailsWithHashComment);
                            }
                            else
                            {
                                compilandsLibraryWithOneOrMoreNoFileHashes.Add(objectModuleDetailsWithHashComment);
                            }
                        }
                        else
                        {
                            if (sf.HashType != HashType.None)
                            {
                                compilandsBinaryWithOneOrMoreInsecureFileHashes.Add(objectModuleDetailsWithHashComment);
                            }
                            else
                            {
                                compilandsBinaryWithOneOrMoreNoFileHashes.Add(objectModuleDetailsWithHashComment);
                            }
                        }
                    }

                    // We only need to check a single source file per compiland, as the relevant
                    // command-line options will be applied to all files in the translation unit.
                    break;
                }
            }

            GenerateCompilandsAndLog(context, compilandsLibraryWithOneOrMoreInsecureFileHashes, compilandsLibraryWithOneOrMoreNoFileHashes, compilandsBinaryWithOneOrMoreInsecureFileHashes, compilandsBinaryWithOneOrMoreNoFileHashes);
        }

        private void GenerateCompilandsAndLog(BinaryAnalyzerContext context, List<ObjectModuleDetails> compilandsLibraryWithOneOrMoreInsecureFileHashes, List<ObjectModuleDetails> compilandsLibraryWithOneOrMoreNoFileHashes, List<ObjectModuleDetails> compilandsBinaryWithOneOrMoreInsecureFileHashes, List<ObjectModuleDetails> compilandsBinaryWithOneOrMoreNoFileHashes)
        {
            bool anyWarningsOrErrors = false;

            if (compilandsLibraryWithOneOrMoreInsecureFileHashes.Count > 0)
            {
                string compilands = compilandsLibraryWithOneOrMoreInsecureFileHashes.CreateOutputCoalescedByCompiler();

                // The hash type is embedded as a comment in with the Library name and this extracts it
                (_, string hashType) = RulesExtensionMethods.ExtractNameAndComment(compilandsLibraryWithOneOrMoreInsecureFileHashes[0].Library);

                // '{0}' is a native binary that links one or more static libraries that 
                // include object files which were hashed using an insecure ({1}) source 
                // code hashing algorithm. Insecure hashing algorithms are subject to 
                // collision attacks and its use can compromise supply chain integrity. 
                // Pass '/ZH:SHA_256' on the cl.exe command-line to enable secure source 
                // code hashing. The following modules are out of policy: {2}
                context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Warning,
                        context,
                        region: null,
                        nameof(RuleResources.BA2004_Warning_NativeWithInsecureStaticLibraryCompilands),
                        context.CurrentTarget.Uri.GetFileName(),
                        hashType,
                        compilands));

                anyWarningsOrErrors = true;
            }

            if (compilandsLibraryWithOneOrMoreNoFileHashes.Count > 0)
            {
                string compilands = compilandsLibraryWithOneOrMoreNoFileHashes.CreateOutputCoalescedByCompiler();

                // '{0}' is a native binary that directly compiles and links one or more
                // object files which were not hashed with a checksum algorithm. Not having
                // a checksum hash can compromise supply chain integrity. Pass '/ZH:SHA_256'
                // on the cl.exe command-line to enable secure source code hashing. The
                // absence of hash data may indicate a compiler problem. Passing /PH to
                // generate #pragma file_hash data when preprocessing may resolve the issue.
                // The following modules are out of policy: {1}
                context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Warning,
                        context,
                        region: null,
                        nameof(RuleResources.BA2004_Warning_NativeWithNoHashStaticLibraryCompilands),
                        context.CurrentTarget.Uri.GetFileName(),
                        compilands));

                anyWarningsOrErrors = true;
            }

            if (compilandsBinaryWithOneOrMoreInsecureFileHashes.Count > 0)
            {
                string compilands = compilandsBinaryWithOneOrMoreInsecureFileHashes.CreateOutputCoalescedByCompiler();

                // The hash type is embedded as a comment in with the Library name and this extracts it
                (_, string hashType) = RulesExtensionMethods.ExtractNameAndComment(compilandsBinaryWithOneOrMoreInsecureFileHashes[0].Library);

                // '{0}' is a native binary that directly compiles and links one or more
                //  object files which were hashed using an insecure ({1}) source code 
                // hashing algorithm. Insecure source code hashing algorithms are subject
                // to collision attacks and its use can compromise supply chain integrity.
                // Pass '/ZH:SHA_256' on the cl.exe command-line to enable secure source
                // code hashing. The following modules are out of policy: {2}
                context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Error,
                        context,
                        region: null,
                        nameof(RuleResources.BA2004_Error_NativeWithInsecureDirectCompilands),
                        context.CurrentTarget.Uri.GetFileName(),
                        hashType,
                        compilands));

                anyWarningsOrErrors = true;
            }

            if (compilandsBinaryWithOneOrMoreNoFileHashes.Count > 0)
            {
                string compilands = compilandsBinaryWithOneOrMoreNoFileHashes.CreateOutputCoalescedByCompiler();

                // '{0}' is a native binary that directly compiles and links one or more 
                // object files which were not hashed with a checksum algorithm. Not having 
                // a checksum hash can compromise supply chain integrity. Pass '/ZH:SHA_256' 
                // on the cl.exe command-line to enable secure source code hashing. The
                // absence of hash data may indicate a compiler problem. Passing /PH to
                // generate #pragma file_hash data when preprocessing may resolve the issue.
                // The following modules are out of policy: {1}
                context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Warning,
                        context,
                        region: null,
                        nameof(RuleResources.BA2004_Warning_NativeWithNoHashDirectCompilands),
                        context.CurrentTarget.Uri.GetFileName(),
                        compilands));

                anyWarningsOrErrors = true;
            }

            if (!anyWarningsOrErrors)
            {
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
