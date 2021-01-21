// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
    public class DoNotMarkWritableSectionsAsShared : PEBinarySkimmerBase
    {
        /// <summary>
        /// BA2019
        /// </summary>
        public override string Id => RuleIds.DoNotMarkWritableSectionsAsShared;

        /// <summary>
        /// PE sections should not be marked as both writable and executable. This condition
        /// makes it easier for an attacker to exploit memory corruption vulnerabilities, as
        /// it may provide an attacker executable location(s) to inject shellcode. To resolve
        /// this issue, configure your tools to not emit memory sections that are writable
        /// and executable. For example, look for uses of /SECTION on the linker command line
        /// for C and C++ programs, or #pragma section in C and C++ source code, which mark a
        /// section with both attributes. Be sure to disable incremental linking in release
        /// builds, as this feature creates a writable and executable section named '.textbss'
        /// in order to function.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2019_DoNotMarkWritableSectionsAsShared_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2019_Pass),
                    nameof(RuleResources.BA2019_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;

            reasonForNotAnalyzing = MetadataConditions.ImageIsXBoxBinary;
            if (portableExecutable.IsXBox) { return result; }

            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();

            System.Collections.Immutable.ImmutableArray<SectionHeader> sectionHeaders = target.PE.PEHeaders.SectionHeaders;

            var badSections = new List<string>();

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
                        nameof(RuleResources.BA2019_Pass),
                        context.TargetUri.GetFileName()));
                return;
            }

            string badSectionsText = string.Join(";", badSections);

            // '{0}' contains PE section(s) ({1}) that are both writable and executable.
            // Writable and executable memory segments make it easier for an attacker
            // to exploit memory corruption vulnerabilities, because it may provide an
            // attacker executable location(s) to inject shellcode. To resolve this
            // issue, configure your tools to not emit memory sections that are writable
            // and executable. For example, look for uses of /SECTION on the linker
            // command line for C and C++ programs, or #pragma section in C and C++
            // source code, which mark a section with both attributes. Enabling
            // incremental linking via the /INCREMENTAL argument (the default for
            // Microsoft Visual Studio debug build) can also result in a writable and
            // executable section named 'textbss'. For this case, disable incremental
            // linking (or analyze an alternate build configuration that disables this
            // feature) to resolve the problem.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                    nameof(RuleResources.BA2019_Error),
                    context.TargetUri.GetFileName(),
                    badSectionsText));
        }
    }
}
