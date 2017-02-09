// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.Linq;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule))]
    public class InitializeStackProtection : BinarySkimmerBase
    {
        /// <summary>
        /// BA2013
        /// 
        /// </summary>
        public override string Id { get { return RuleIds.InitializeStackProtectionId; } }

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

        public override string FullDescription
        {
            get { return RuleResources.BA2013_InitializeStackProtection_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2013_Pass),
                    nameof(RuleResources.BA2013_Pass_NoCode),
                    nameof(RuleResources.BA2013_NotApplicable_FeatureNotEnabled),
                    nameof(RuleResources.BA2013_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            return StackProtectionUtilities.CommonCanAnalyze(context, out reasonForNotAnalyzing);
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;

            if (context.Pdb == null)
            {
                Errors.LogExceptionLoadingPdb(context, context.PdbParseException);
                return;
            }

            Pdb di = context.Pdb;

            bool noCode = !di.CreateGlobalFunctionIterator().Any() && !di.ContainsExecutableSectionContribs();

            if (noCode)
            {
                // '{0}' is a C or C++ binary that is not required to initialize the stack protection, as it does not contain executable code.
                context.Logger.Log(this, 
                    RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                        nameof(RuleResources.BA2013_Pass_NoCode),
                        context.TargetUri.GetFileName()));
                return;
            }

            bool bHasGSCheck = di.CreateGlobalFunctionIterator(
                StackProtectionUtilities.GSCheckFunctionName, NameSearchOptions.nsfCaseSensitive).Any();

            bool bHasGSInit = StackProtectionUtilities.GSInitializationFunctionNames.Any(
                                functionName => di.CreateGlobalFunctionIterator(functionName,
                                                                                NameSearchOptions.nsfCaseSensitive).Any());

            if (!bHasGSCheck && !bHasGSInit)
            {
                // '{0}' is a C or C++ binary that does not enable the stack protection buffer
                // security feature. It is therefore not required to initialize the stack protector.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.NotApplicable, context, null,
                        nameof(RuleResources.BA2013_NotApplicable_FeatureNotEnabled),
                        context.TargetUri.GetFileName()));
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
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                        nameof(RuleResources.BA2013_Error),
                        context.TargetUri.GetFileName()));
                return;
            }

            // '{0}' is a C or C++ binary built with the buffer security feature 
            // that properly initializes the stack protecter. This has the 
            //effect of increasing the effectiveness of the feature and reducing 
            // spurious detections.
            context.Logger.Log(this, 
                RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                   nameof(RuleResources.BA2013_Pass),
                        context.TargetUri.GetFileName()));
        }
    }
}
