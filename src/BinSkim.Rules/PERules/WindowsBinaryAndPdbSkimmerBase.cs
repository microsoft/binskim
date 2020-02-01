// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    // Windows specific binary and program database-reading skimmers.
    public abstract class WindowsBinaryAndPdbSkimmerBase : WindowsBinarySkimmerBase
    {
        public sealed override void Analyze(BinaryAnalyzerContext context)
        {
            // Uses PDB Parsing.
            BinaryParsers.PlatformSpecificHelpers.ThrowIfNotOnWindows();
            PEBinary target = context.PEBinary();

            if (target.Pdb == null)
            {
                Errors.LogExceptionLoadingPdb(context, target.PdbParseException);
                return;
            }

            this.AnalyzePortableExecutableAndPdb(context);
        }

        public sealed override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            AnalysisApplicability result = base.CanAnalyze(context, out reasonForNotAnalyzing);
            if (result != AnalysisApplicability.ApplicableToSpecifiedTarget) { return result; }

            PE portableExecutable = context.PEBinary().PE;
            result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsWixBinary;
            if (portableExecutable.IsWixBinary) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsILLibraryAssembly;
            if (portableExecutable.IsILLibrary) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsDotNetCoreBootstrapExe;
            if (portableExecutable.IsDotNetCoreBootstrapExe) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public abstract void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context);
    }
}
