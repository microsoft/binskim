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
    public class EnableClangSafeStack : ElfBinarySkimmer
    {
        // Symbol is the same for both C and C++, despite the "cpp" or "cc" in file name.
        // Clang V7 - V9: "safestack.cc.o"
        // Clang V10 - V14: "safestack.cpp.o"
        private static readonly string[] symbolSafeStack = new string[] { "safestack.cpp.o", "safestack.cc.o" };

        /// <summary>
        /// BA3031
        /// </summary>
        public override string Id => RuleIds.EnableClangSafeStack;

        /// <summary>
        /// The SafeStack instrumentation pass protects programs by implementing two separate program stacks, 
        /// one for return addresses and local variables, and the other for everything else.
        /// To enable SafeStack, pass '-fsanitize=safe-stack' flag to both compile and link command lines.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3031_EnableClangSafeStack_Description };

        protected override ICollection<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA3031_Pass),
                    nameof(RuleResources.BA3031_Error),
                    nameof(RuleResources.BA3031_Error_ClangVersionMayNeedUpgrade),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzeElf(ElfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            IELF elf = target.ELF;

            if (elf.Type == FileType.Core || elf.Type == FileType.None || elf.Type == FileType.Relocatable)
            {
                reasonForNotAnalyzing = MetadataConditions.ElfIsCoreNoneOrRelocatable;
                return AnalysisApplicability.NotApplicableToSpecifiedTarget;
            }

            if (!target.Compilers.Any(c => c.Compiler == ElfCompilerType.Clang))
            {
                reasonForNotAnalyzing = MetadataConditions.ElfNotBuiltWithClang;
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

            IEnumerable<ISymbolEntry> symbols = ElfUtility.GetAllSymbols(elf);

            foreach (ISymbolEntry symbol in symbols)
            {
                if (symbol.Type == SymbolType.File && symbolSafeStack.Contains(symbol.Name))
                {
                    context.Logger.Log(this,
                       RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                           nameof(RuleResources.BA3031_Pass),
                           context.CurrentTarget.Uri.GetFileName()));
                    return;
                }
            }

            // SafeStack was first introduced in Clang 3.7.0
            // https://releases.llvm.org/3.7.0/tools/clang/docs/SafeStack.html
            if (!context.ElfBinary().Compilers.Any(c => c.Compiler == ElfCompilerType.Clang &&
            (c.Version.Major >= 4 || (c.Version.Major == 3 && c.Version.Minor >= 7))))
            {
                context.Logger.Log(this,
                       RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                           nameof(RuleResources.BA3031_Error_ClangVersionMayNeedUpgrade),
                           context.CurrentTarget.Uri.GetFileName()));
                return;
            }

            context.Logger.Log(this,
                       RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                           nameof(RuleResources.BA3031_Error),
                           context.CurrentTarget.Uri.GetFileName()));
        }
    }
}
