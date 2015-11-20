// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinSkim.Sdk;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    [Export(typeof(IBinarySkimmer))]
    public class DoNotMarkWritableSectionsAsShared : IBinarySkimmer, IRuleContext
    {
        public string Id { get { return RuleConstants.DoNotMarkWritableSectionsAsSharedId; } }

        public string Name { get { return nameof(DoNotMarkWritableSectionsAsShared); } }

        public void Initialize(BinaryAnalyzerContext context) { return; }

        public AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = context.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsXBoxBinary;
            if (portableExecutable.IsXBox) { return result; }

            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public void Analyze(BinaryAnalyzerContext context)
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
                context.Logger.Log(MessageKind.Pass, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.DoNotMarkWritableSectionsAsShared_Pass));
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
            context.Logger.Log(MessageKind.Fail, context,
                RuleUtilities.BuildMessage(context,
                    RulesResources.DoNotMarkWritableSectionsAsShared_Fail, badSectionsText));
        }
    }
}
