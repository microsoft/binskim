// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class EnableSecureSourceCodeHashing : WindowsBinaryAndPdbSkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2004
        /// </summary>
        public override string Id => RuleIds.EnableSecureSourceCodeHashing;

        public override MultiformatMessageString FullDescription => new MultiformatMessageString
        { Text = RuleResources.BA2004_EnableSecureSourceCodeHashing_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA2004_Pass),
            nameof(RuleResources.BA2004_Warning_Native),
            nameof(RuleResources.BA2004_Warning_Managed),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            // TODO: currently our test binary for this check is a dll that does not
            // compile against any external library. BinSkim regards this as a resource
            // only binary and skips the test. We should improve the IsResourceOnly
            // helper to properly identify this dependency-free test binary as code.

            //            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            //            if (portableExecutable.IsResourceOnly) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            if (context.PEBinary().PE.IsManaged)
            {
                AnalyzeManagedAssemblyAndPdb(context);
                return;
            }

            AnalyzeNativeBinaryAndPdb(context);
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private void AnalyzeManagedAssemblyAndPdb(BinaryAnalyzerContext context)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            // TODO: use DiaSymReader?
        }

        public void AnalyzeNativeBinaryAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb di = target.Pdb;

            var compilandsBinaryWithOneOrMoreInsecureFileHashes = new List<ObjectModuleDetails>();
            var compilandsLibraryWithOneOrMoreInsecureFileHashes = new List<ObjectModuleDetails>();

            foreach (DisposableEnumerableView<Symbol> omView in di.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                if (omDetails.Language != Language.C &&
                    omDetails.Language != Language.Cxx)
                {
                    continue;
                }

                if (!omDetails.HasDebugInfo)
                {
                    continue;
                }

                CompilandRecord record = om.CreateCompilandRecord();

                foreach (DisposableEnumerableView<SourceFile> sfView in di.CreateSourceFileIterator(om))
                {
                    SourceFile sf = sfView.Value;

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
            string compilands = compilandsWithOneOrMoreInsecureFileHashes.CreateOutputCoalescedByLibrary();

            //'{0}' is a native binary that links one or more object files which were hashed
            // using an insecure checksum algorithm (MD5). MD5 is subject to collision attacks
            // and its use can compromise supply chain integrity. Pass '/ZH:SHA-256' on the
            // cl.exe command-line to enable secure source code hashing. The following modules
            // are out of policy: {1} 
            context.Logger.Log(this,
                RuleUtilities.BuildResult(failureLevel, context, null,
                nameof(RuleResources.BA2004_Warning_Native),
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
