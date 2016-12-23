// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reflection.PortableExecutable;

using Dia2Lib;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule))]
    public class EnableStackProtection : BinarySkimmerBase
    {
        /// <summary>
        /// BA2011
        /// </summary>
        public override string Id { get { return RuleIds.EnableStackProtectionId; } }

        /// <summary>
        /// Binaries should be built with the stack protector buffer security
        /// feature (/GS) enabled in order to increase the difficulty of
        /// exploiting stack buffer overflow memory corruption
        /// vulnerabilities. To resolve this issue, ensure that all modules
        /// compiled into the binary are compiled with the stack protector
        /// enabled by supplying /GS on the Visual C++ compiler command line.
        /// </summary>
        public override string FullDescription
        {
            get { return RuleResources.BA2011_EnableStackProtection_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2011_Pass),                    
                    nameof(RuleResources.BA2011_Error),
                    nameof(RuleResources.BA2011_Error_UnknownModuleLanguage),
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
            Pdb pdb = context.Pdb;

            if (pdb == null)
            {
                Errors.LogExceptionLoadingPdb(context, context.PdbParseException);
                return;
            }

            TruncatedCompilandRecordList noGsModules = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList unknownLanguageModules = new TruncatedCompilandRecordList();

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                // Detection applies to C/C++ produced by MS compiler only
                if (omDetails.WellKnownCompiler != WellKnownCompilers.MicrosoftNativeCompiler)
                {
                    continue;
                }

                if (omDetails.Language == Language.Unknown)
                {
                    // See if this module contributed to an executable section. 
                    // If not, we can ignore the module.
                    if (pdb.CompilandWithIdIsInExecutableSectionContrib(om.SymIndexId))
                    {
                        unknownLanguageModules.Add(om.CreateCompilandRecord());
                    }
                    continue;
                }

                if (!omDetails.HasSecurityChecks && om.CreateChildIterator(SymTagEnum.SymTagFunction).Any())
                {
                    noGsModules.Add(om.CreateCompilandRecord());
                }
            }

            if (unknownLanguageModules.Empty && noGsModules.Empty)
            {
                // '{0}' is a C or C++ binary built with the stack protector buffer security 
                // feature enabled for all modules, making it more difficult for an attacker to 
                // exploit stack buffer overflow memory corruption vulnerabilities. 
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                        nameof(RuleResources.BA2011_Pass),
                        context.TargetUri.GetFileName()));
                return;
            }

            if (!unknownLanguageModules.Empty)
            {
                // '{0}' contains code from unknown language, preventing a comprehensive analysis of the 
                // stack protector buffer security features. The language could not be identified for
                // the following modules: {1}.
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                        nameof(RuleResources.BA2011_Error_UnknownModuleLanguage),
                        context.TargetUri.GetFileName(),
                        unknownLanguageModules.CreateSortedObjectList()));
            }

            if (!noGsModules.Empty)
            {
                // '{0}' is a C or C++ binary built with the stack protector buffer security feature 
                // disabled in one or more modules. The stack protector (/GS) is a security feature 
                // of the compiler which makes it more difficult to exploit stack buffer overflow 
                // memory corruption vulnerabilities. To resolve this issue, ensure that your code 
                // is compiled with the stack protector enabled by supplying /GS on the Visual C++ 
                // compiler command line. The affected modules were: {1}
                context.Logger.Log(this, 
                    RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                        nameof(RuleResources.BA2011_Error),
                        context.TargetUri.GetFileName(),
                        noGsModules.ToString()));
            }
        }
    }
}
