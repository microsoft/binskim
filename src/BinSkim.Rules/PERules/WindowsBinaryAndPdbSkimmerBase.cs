// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

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

            Pdb di = target.Pdb;

            AnalyzePortableExecutableAndPdb(context);
        }

        public abstract void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context);
    }
}
