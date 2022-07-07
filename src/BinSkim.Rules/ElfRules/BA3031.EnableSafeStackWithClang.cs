// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class EnableSafeStackWithClang : ElfBinarySkimmer
    {
        // Symbol is the same for both C and C++, despite the "cpp" or "cc" in file name.
        // Clang V7 - V9: "safestack.cc.o"
        // Clang V10 - V14: "safestack.cpp.o"
        private static readonly string[] symbolSafeStack = new string[] { "safestack.cpp.o", "safestack.cc.o" };

        /// <summary>
        /// BA3031
        /// </summary>
        public override string Id => RuleIds.EnableSafeStackWithClang;

        /// <summary>
        /// The SafeStack instrumentation pass protects programs by implementing two separate program stacks, 
        /// one for return addresses and local variables, and the other for everything else.
        /// To enable SafeStack, pass '-fsanitize=safe-stack' flag to both compile and link command lines.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3031_EnableSafeStackWithClang_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA3031_Pass),
                    nameof(RuleResources.BA3031_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzeElf(ElfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            IELF elf = target.ELF;

            if (elf.Type == FileType.Core || elf.Type == FileType.None || elf.Type == FileType.Relocatable)
            {
                reasonForNotAnalyzing = MetadataConditions.ElfIsCoreNoneOrObject;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            if (!target.Compilers.Any(c => c.Compiler == ElfCompilerType.Clang && c.Version.Major >= 7))
            {
                reasonForNotAnalyzing = MetadataConditions.ElfNotBuiltWithClangV7OrLater;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        /// <summary>
        /// Checks if SafeStack is enabled by Symbols.
        /// </summary>
        public override void Analyze(BinaryAnalyzerContext context)
        {
            IELF elf = context.ElfBinary().ELF;

            IEnumerable<ISymbolEntry> symbols =
                ElfUtility.GetAllSymbols(elf).Where(sym => sym.Type == SymbolType.File);

            if (symbols.Any(s => symbolSafeStack.Contains(s.Name)))
            {
                context.Logger.Log(this,
                       RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                           nameof(RuleResources.BA3031_Pass),
                           context.TargetUri.GetFileName()));
            }
            else
            {
                context.Logger.Log(this,
                       RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                           nameof(RuleResources.BA3031_Error),
                           context.TargetUri.GetFileName()));
            }
        }
    }
}
