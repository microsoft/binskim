// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class DoNotMarkWritableSectionsAsExecutable : PEBinarySkimmerBase
    {
        /// <summary>
        /// BA2021
        /// </summary>
        public override string Id => RuleIds.DoNotMarkWritableSectionsAsExecutable;

        /// <summary>
        /// PE sections should not be marked as both writable and executable. This condition
        /// makes it easier for an attacker to exploit memory corruption vulnerabilities,
        /// as it may provide an attacker executable location(s) to inject shellcode.
        /// To resolve this issue, configure your toolchain to not emit memory sections that
        /// are writable and executable. For example, look for uses of /SECTION on the
        /// linker command line for C and C++ programs, or #pragma section in C and C++
        /// source code, which mark a section with both attributes.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2021_DoNotMarkWritableSectionsAsExecutable_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2021_Pass),
                    nameof(RuleResources.BA2021_Error),
                    nameof(RuleResources.BA2021_Error_UnexpectedSectionAligment),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        private const int PAGE_SIZE = 0x1000;

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsKernelModeBinary;
            if (portableExecutable.IsKernelMode) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();

            PEHeader peHeader = target.PE.PEHeaders.PEHeader;

            // TODO: do we really require this check? What is the proposed fix to this issue? 
            if (peHeader.SectionAlignment < PAGE_SIZE)
            {
                // '{0}' has a section alignment ({1}) that is less than its page size ({2}).
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2021_Error_UnexpectedSectionAligment),
                        context.TargetUri.GetFileName(),
                        "0x" + peHeader.SectionAlignment.ToString("x"),
                        "0x" + PAGE_SIZE.ToString("x")));
                return;
            }

            ImmutableArray<SectionHeader> sectionHeaders = target.PE.PEHeaders.SectionHeaders;

            var badSections = new List<string>();

            if (sectionHeaders != null)
            {
                foreach (SectionHeader sectionHeader in sectionHeaders)
                {
                    SectionCharacteristics wxFlags = SectionCharacteristics.MemWrite | SectionCharacteristics.MemExecute;

                    if ((sectionHeader.SectionCharacteristics & wxFlags) == wxFlags)
                    {
                        badSections.Add(sectionHeader.Name);
                    }
                }
            }

            if (badSections.Count == 0)
            {
                // '{0}' contains no data or code sections marked as both shared and executable.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA2021_Pass),
                        context.TargetUri.GetFileName()));
                return;
            }

            string badSectionsText = string.Join(";", badSections);

            // '{0}' contains PE section(s)({ 1}) that are both writable and executable. 
            // Writable and executable memory segments make it easier for an attacker to
            //exploit memory corruption vulnerabilities, because it may give an attacker 
            // executable location(s) to inject shellcode. To resolve this
            // issue, configure your toolchain to not emit memory sections that are 
            // writable and executable. For example, look for uses of /SECTION on the 
            // linker command line for C and C++ programs, or  #pragma section in C and 
            // C++ source code, which mark a section with both attributes.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                    nameof(RuleResources.BA2021_Error),
                    context.TargetUri.GetFileName(),
                    badSectionsText));
        }
    }
}
