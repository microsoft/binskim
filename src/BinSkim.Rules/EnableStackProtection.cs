// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.BinSkim.Sdk;
using Microsoft.CodeAnalysis.Driver;

using Dia2Lib;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    [Export(typeof(IBinarySkimmer))]
    public class EnableStackProtection : IBinarySkimmer, IRuleContext
    {
        public string Id { get { return RuleConstants.EnableStackProtectionId; } }

        public string Name { get { return nameof(EnableStackProtection); } }

        public void Initialize(BinaryAnalyzerContext context) { return; }

        public AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            return StackProtectionUtilities.CommonCanAnalyze(context, out reasonForNotAnalyzing);
        }

        public void Analyze(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;
            Pdb pdb = context.Pdb;

            if (pdb == null)
            {
                context.Logger.Log(MessageKind.Fail, context,
                    RuleUtilities.BuildCouldNotLoadPdbMessage(context));
                return;
            }

            TruncatedCompilandRecordList noGsModules = new TruncatedCompilandRecordList();
            TruncatedCompilandRecordList unknownLanguageModules = new TruncatedCompilandRecordList();

            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails details = om.GetObjectModuleDetails();

                if (details.Language == Language.Unknown)
                {
                    // See if this module contributed to an executable section. 
                    // If not, we can ignore the module.
                    if (pdb.CompilandWithIdIsInExecutableSectionContrib(om.SymIndexId))
                    {
                        unknownLanguageModules.Add(om.CreateCompilandRecord());
                    }
                    continue;
                }

                // Detection applies to C/C++ produced by MS compiler only
                if ((details.Language != Language.C) && (details.Language != Language.Cxx) ||
                     details.Compiler != "Microsoft (R) Optimizing Compiler")
                {
                    continue;
                }

                if (!details.HasSecurityChecks && om.CreateChildIterator(SymTagEnum.SymTagFunction).Any())
                {
                    noGsModules.Add(om.CreateCompilandRecord());
                }
            }

            if (unknownLanguageModules.Empty && noGsModules.Empty)
            {
                // '{0}' is a C or C++ binary built with the stack protector buffer security 
                // feature enabled for all modules, making it more difficult for an attacker to 
                // exploit stack buffer overflow memory corruption vulnerabilities. 
                context.Logger.Log(MessageKind.Pass, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.EnableStackProtection_Pass));
                return;
            }

            if (!unknownLanguageModules.Empty)
            {
                // '{0}' contains code from unknown language, preventing a comprehensive analysis of the 
                // stack protector buffer security features. The language could not be identified for
                // the following modules: {1}.
                context.Logger.Log(MessageKind.Fail, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.EnableStackProtection_UnknownModuleLanguage_Fail,
                        unknownLanguageModules.ToString()));
            }

            if (!noGsModules.Empty)
            {
                // '{0}' is a C or C++ binary built with the stack protector buffer security feature 
                // disabled in one or more modules. The stack protector (/GS) is a security feature 
                // of the compiler which makes it more difficult to exploit stack buffer overflow 
                // memory corruption vulnerabilities. To resolve this issue, ensure that your code 
                // is compiled with the stack protector enabled by supplying /GS on the Visual C++ 
                // compiler command line. The affected modules were: {1}
                context.Logger.Log(MessageKind.Fail, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.EnableStackProtection_Fail,
                        noGsModules.ToString()));
            }
        }
    }
}
