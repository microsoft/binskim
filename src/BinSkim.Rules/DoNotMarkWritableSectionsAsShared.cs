// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRuleDescriptor))]
    public class DoNotMarkWritableSectionsAsShared : BinarySkimmerBase
    {
        /// <summary>
        /// BA2019
        /// </summary>
        public override string Id { get { return RuleIds.DoNotMarkWritableSectionsAsSharedId; } }

        /// <summary>
        /// Code or data sections should not be marked as both shared and writable. Because
        /// these sections are shared across processes, this condition might permit a
        /// process with low privilege to mutate memory in a higher privilege process.
        /// If you do not actually require that a section be both writable and shared,
        /// remove one or both of these attributes (by modifying your .DEF file, the
        /// appropriate linker /section switch arguments, etc.). If you are required to
        /// share common data across processes (for inter-process communication (IPC) or
        /// other purposes) use CreateFileMapping with proper security attributes or an
        /// actual IPC mechanism instead (COM, named pipes, LPC, etc.).
        /// </summary>

        public override string FullDescription
        {
            get { return RuleResources.BA2019_DoNotMarkWritableSectionsAsShared_Description; }
        }

        protected override IEnumerable<string> FormatSpecifierIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2019_Pass),
                    nameof(RuleResources.BA2019_Error)};
            }
        }

        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsXBoxBinary;
            if (portableExecutable.IsXBox) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            var sectionHeaders = context.PE.PEHeaders.SectionHeaders;

            List<string> badSections = new List<string>();

            if (sectionHeaders != null)
            {
                foreach (SectionHeader sectionHeader in sectionHeaders)
                {
                    SectionCharacteristics wsFlags = SectionCharacteristics.MemWrite | SectionCharacteristics.MemShared;

                    if ((sectionHeader.SectionCharacteristics & wsFlags) == wsFlags) // IMAGE_SCN_MEM_WRITE & IMAGE_SCN_MEM_SHARED
                    {
                        badSections.Add(sectionHeader.Name);
                    }
                }
            }

            if (badSections.Count == 0)
            {
                // Image '{0}' contains no data or code sections marked as both shared and writable.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA2019_Pass)));
                return;
            }

            string badSectionsText = String.Join(";", badSections);

            // {0} contains one or more code or data sections ({1}) which are marked as both
            // shared and writable. Because these sections are shared across processes, this
            // condition might permit a process with low privilege to mutate memory in a higher
            // privilege process. If you do not actually require that the section be both
            // writable and shared, remove one or both of these attributes (by modifying your
            // .DEF file, the appropriate linker /section switch arguments, etc.). If you are
            // required to share common data across processes (for IPC or other purposes) use
            // CreateFileMapping with proper security attributes or an actual IPC mechanism 
            // instead (COM, named pipes, LPC, etc.).
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Error, context, null,
                    nameof(RuleResources.BA2019_Error),
                    badSectionsText));
        }
    }
}
