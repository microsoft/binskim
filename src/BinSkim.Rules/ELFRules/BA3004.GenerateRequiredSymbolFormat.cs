// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class GenerateRequiredSymbolFormat : ElfBinarySkimmer
    {
        /// <summary>
        /// BA3004
        /// </summary>
        public override string Id => RuleIds.GenerateRequiredSymbolFormat;

        /// <summary>
        /// This check ensures that debugging dwarf version used is 5.
        /// The dwarf version 5 contains more information and should be used.
        /// Use the compiler flags '-gdwarf-5' to enable this.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA3004_GenerateRequiredSymbolFormat_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
            nameof(RuleResources.BA3004_Pass),
            nameof(RuleResources.BA3004_Error),
            nameof(RuleResources.NotApplicable_InvalidMetadata)
        };

        public override AnalysisApplicability CanAnalyzeElf(ElfBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            ElfBinary elfBinary = context.ELFBinary();
            int dwarfVersion = elfBinary.DwarfVersion;
            if (dwarfVersion == -1)
            {
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.None, context, null,
                        nameof(RuleResources.NotApplicable_InvalidMetadata),
                        context.TargetUri.GetFileName()));
                return;
            }

            if (dwarfVersion < 5)
            {
                // '{0}' is using debugging dwarf version '{1}'.
                // The dwarf version 5 contains more information and should be used.
                // To enable the debugging version 5 use '-gdwarf-5'.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA3004_Error),
                        context.TargetUri.GetFileName(), dwarfVersion.ToString()));
                return;
            }

            // The version of the debugging dwarf format is '{0}' for the file '{1}'
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA3004_Pass),
                    dwarfVersion.ToString(), context.TargetUri.GetFileName()));
        }
    }
}
