// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Sdk;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(IBinarySkimmer)), Export(typeof(IRuleDescriptor))]
    public class DoNotMarkImportsSectionAsExecutable : BinarySkimmerBase
    {
        public override string Id { get { return RuleIds.DoNotMarkImportsSectionAsExecutableId; } }

        public override string FullDescription
        {
            get { return RulesResources.DoNotMarkImportsSectionAsExecutable_Description; }
        }
        
        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyManagedAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            string executableImportSection = null;
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;
            DirectoryEntry importTable = peHeader.ImportTableDirectory;

            if (importTable.RelativeVirtualAddress != 0 && context.PE.PEHeaders.SectionHeaders != null)
            {
                int importSize = peHeader.ImportTableDirectory.Size;
                foreach (SectionHeader sectionHeader in context.PE.PEHeaders.SectionHeaders)
                {
                    SectionCharacteristics memExecute = SectionCharacteristics.MemExecute;
                    if ((sectionHeader.SectionCharacteristics & memExecute) == 0)
                    {
                        continue;
                    }

                    int size = sectionHeader.SizeOfRawData;
                    int address = sectionHeader.VirtualAddress;

                    if ((address <= importTable.RelativeVirtualAddress) &&
                        (address + size >= importTable.RelativeVirtualAddress + importTable.Size))
                    {
                        // Our import section is in a writable section - bad
                        executableImportSection = sectionHeader.Name;
                        break;
                    }
                }
            }

            if (executableImportSection != null)
            {
                // '{0}' has the imports section marked executable. Because the loader will always mark 
                // the imports section as writable, it is important to mark this section as non-executable, 
                // so that an attacker cannot place shellcode here. To resolve this issue, ensure that your 
                //program does not mark the imports section as executable. Look for uses of /SECTION or 
                // /MERGE on the linker command line, or #pragma segment in source code, which change the 
                // imports section to be executable, or which merge the ".rdata" segment into an executable 
                // section.
                context.Logger.Log(ResultKind.Error, context,
                        RuleUtilities.BuildMessage(context,
                            RulesResources.DoNotMarkImportsSectionAsExecutable_Fail));
                return;
            }

            // '{0}' does not have an imports section that is marked as executable.
            context.Logger.Log(ResultKind.Pass, context,
                RuleUtilities.BuildMessage(context,
                    RulesResources.DoNotMarkImportsSectionAsExecutable_Pass));
        }
    }
}
