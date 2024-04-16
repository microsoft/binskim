﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class InitializeStackProtection : WindowsBinaryAndPdbSkimmerBase
    {
        /// <summary>
        /// BA2013
        ///
        /// </summary>
        public override string Id => RuleIds.InitializeStackProtection;

        /// <summary>
        /// Binaries should properly initialize the stack protector (/GS) in order
        /// to increase the difficulty of exploiting stack buffer overflow memory
        /// corruption vulnerabilities. The stack protector requires access to
        /// entropy in order to be effective, which means a binary must initialize
        /// a random number generator at startup, by calling
        /// __security_init_cookie() as close to the binary's entry point as
        /// possible. Failing to do so will result in spurious buffer overflow
        /// detections on the part of the stack protector. To resolve this issue,
        /// use the default entry point provided by the C runtime, which will make
        /// this call for you, or call __security_init_cookie() manually in your
        /// custom entry point.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2013_InitializeStackProtection_Description };

        protected override ICollection<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2013_Pass),
                    nameof(RuleResources.BA2013_Pass_NoCode),
                    nameof(RuleResources.BA2013_NotApplicable_FeatureNotEnabled),
                    nameof(RuleResources.BA2013_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            return StackProtectionUtilities.CommonCanAnalyze(target, out reasonForNotAnalyzing);
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            bool noCode = !pdb.CreateGlobalFunctionIterator().Any() && !pdb.ContainsExecutableSectionContribs();

            if (noCode)
            {
                // '{0}' is a C or C++ binary that is not required to initialize the stack protection, as it does not contain executable code.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA2013_Pass_NoCode),
                        context.CurrentTarget.Uri.GetFileName()));
                return;
            }

            bool bHasGSCheck = pdb.CreateGlobalFunctionIterator(
                StackProtectionUtilities.GSCheckFunctionName, NameSearchOptions.nsfCaseSensitive).Any();

            bool bHasGSInit = StackProtectionUtilities.GSInitializationFunctionNames.Any(
                                functionName => pdb.CreateGlobalFunctionIterator(functionName,
                                                                                NameSearchOptions.nsfCaseSensitive).Any());

            if (!bHasGSCheck && !bHasGSInit)
            {
                // '{0}' is a C or C++ binary that does not enable the stack protection buffer
                // security feature. It is therefore not required to initialize the stack protector.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.NotApplicable, context, null,
                        nameof(RuleResources.BA2013_NotApplicable_FeatureNotEnabled),
                        context.CurrentTarget.Uri.GetFileName()));
                return;
            }

            if (!bHasGSInit)
            {
                // '{0}' is a C or C++ binary that does not initialize the stack protector. 
                // The stack protector(/ GS) is a security feature of the compiler which 
                // makes it more difficult to exploit stack buffer overflow memory 
                // corruption vulnerabilities. The stack protector requires access to 
                // entropy in order to be effective, which means a binary must initialize 
                // a random number generator at startup, by calling __security_init_cookie() 
                // as close to the binary's entry point as possible. Failing to do so will 
                // result in spurious buffer overflow detections on the part of the stack 
                // protector. To resolve this issue, use the default entry point provided 
                // by the C runtime, which will make this call for you, or call 
                // __security_init_cookie() manually in your custom entry point.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2013_Error),
                        context.CurrentTarget.Uri.GetFileName()));
                return;
            }

            // '{0}' is a C or C++ binary built with the buffer security feature 
            // that properly initializes the stack protecter. This has the 
            //effect of increasing the effectiveness of the feature and reducing 
            // spurious detections.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                   nameof(RuleResources.BA2013_Pass),
                        context.CurrentTarget.Uri.GetFileName()));
        }
    }
}
