// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules.Helpers
{
    public static class SymbolHelper
    {
        /// <summary>
        /// This is similar to the rule BA2004 for native binary.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="pdb"></param>
        /// <param name="compilandsBinaryWithOneOrMoreInsecureFileHashes"></param>
        /// <param name="compilandsLibraryWithOneOrMoreInsecureFileHashes"></param>
        /// <returns><see cref="ValidationState"/></returns>
        public static ValidationState AnalyzeNativeSourceCodeHashing(
            Symbol symbol,
            Pdb pdb,
            out List<ObjectModuleDetails> compilandsBinaryWithOneOrMoreInsecureFileHashes,
            out List<ObjectModuleDetails> compilandsLibraryWithOneOrMoreInsecureFileHashes)
        {
            ObjectModuleDetails omDetails = symbol.GetObjectModuleDetails();
            compilandsBinaryWithOneOrMoreInsecureFileHashes = new List<ObjectModuleDetails>();
            compilandsLibraryWithOneOrMoreInsecureFileHashes = new List<ObjectModuleDetails>();

            if (omDetails.Language != Language.C &&
                omDetails.Language != Language.Cxx)
            {
                return ValidationState.Ignore;
            }

            if (!omDetails.HasDebugInfo)
            {
                return ValidationState.Ignore;
            }

            CompilandRecord record = symbol.CreateCompilandRecord();

            foreach (DisposableEnumerableView<SourceFile> sfView in pdb.CreateSourceFileIterator(symbol))
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

            if (compilandsBinaryWithOneOrMoreInsecureFileHashes.Count > 0)
            {
                return ValidationState.Error;
            }
            else if (compilandsLibraryWithOneOrMoreInsecureFileHashes.Count > 0)
            {
                return ValidationState.Warning;
            }
            else
            {
                return ValidationState.Pass;
            }
        }
    }
}
