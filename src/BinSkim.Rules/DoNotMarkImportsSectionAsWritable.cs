// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule))]
    public class DoNotMarkImportsSectionAsWritable : BinarySkimmerBase
    {
        /// <summary>
        /// BA2010
        /// </summary>
        public override string Id { get { return RuleIds.DoNotMarkImportsSectionAsExecutableId; } }

        /// <summary>
        /// PE sections should not be marked as both writable and executable. This condition
        /// makes it easier for an attacker to exploit memory corruption vulnerabilities,
        /// as it may provide an attacker executable location(s) to inject shellcode.
        /// Because the loader will always mark the imports section as writable, it is
        /// therefore important to mark this section as non-executable. To resolve this
        /// issue, ensure that your program does not mark the imports section executable.
        /// Look for uses of /SECTION or /MERGE on the linker command line, or #pragma
        /// segment in source code, which change the imports section to be executable, or
        /// which merge the ".rdata" segment into an executable section.
        /// </summary>

        public override string FullDescription
        {
            get { return RuleResources.BA2023_DoNotMarkImportsSectionAsWritable_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2010_Pass),
                    nameof(RuleResources.BA2010_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = MetadataConditions.ImageIsILOnlyManagedAssembly;
            if (portableExecutable.IsILOnly) { return result; }

            reasonForNotAnalyzing = null;
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
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                        nameof(RuleResources.BA2010_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' does not have an imports section that is marked as executable.
            context.Logger.Log(this, 
                RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                    nameof(RuleResources.BA2010_Pass), 
                    context.TargetUri.GetFileName()));
        }
    }
}
