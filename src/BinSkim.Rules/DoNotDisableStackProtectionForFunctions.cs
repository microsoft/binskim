// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.BinSkim.Sdk;
using Microsoft.CodeAnalysis.Driver;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    [Export(typeof(IBinarySkimmer)), Export(typeof(IOptionsProvider))]
    public class DoNotDisableStackProtectionForFunctions : IBinarySkimmer, IRuleContext, IOptionsProvider
    {
        public string Id { get { return RuleConstants.DoNotDisableStackProtectionForFunctionsId; } }

        public string Name { get { return nameof(DoNotDisableStackProtectionForFunctions); } }

        public void Initialize(BinaryAnalyzerContext context) { return; }

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                ApprovedFunctionsThatDisableStackProtection,
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleConstants.DoNotDisableStackProtectionForFunctionsId + "." + nameof(DoNotDisableStackProtectionForFunctions);

        private static StringSet BuildApprovedFunctionsStringSet()
        {
            var result = new StringSet();
            result.Add("_TlgWrite");
            return result;
        }

        /// <summary>
        /// Enable namespace import optimization.
        /// </summary>
        public static PerLanguageOption<StringSet> ApprovedFunctionsThatDisableStackProtection { get; } =
            new PerLanguageOption<StringSet>(
                AnalyzerName, nameof(StringSet), defaultValue: () => { return BuildApprovedFunctionsStringSet(); });

        public void Initialize() { return; }

        public AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            AnalysisApplicability applicability = StackProtectionUtilities.CommonCanAnalyze(context, out reasonForNotAnalyzing);

            // Checks for missing policy should always be evaluated as the last action, so that 
            // we do not raise an error in cases where the analysis would not otherise be applied.
            if (applicability == AnalysisApplicability.ApplicableToSpecifiedTarget)
            {
                reasonForNotAnalyzing = RulesResources.DoNotShipVulnerabilities_MissingPolicy_InternalError;
                if (context.Policy == null) { return AnalysisApplicability.NotApplicableToAnyTargetWithoutPolicy; }
            }
            return applicability;
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

            List<string> names = new List<string>();
            foreach (DisposableEnumerableView<Symbol> functionView in pdb.CreateGlobalFunctionIterator())
            {
                Symbol function = functionView.Value;
                if (function.IsManaged) { continue; }
                if (!function.IsSafeBuffers) { continue; }

                string functionName = function.GetUndecoratedName();

                if (functionName == "__security_init_cookie" ||
                    context.Policy.GetProperty(ApprovedFunctionsThatDisableStackProtection).Contains(functionName))
                {
                    continue;
                }
                names.Add(functionName);
            }

            if (names.Count != 0)
            {
                string functionNames = string.Join(";", names);

                // '{0}' is a C or C++ binary built with function(s) ({1}) that disable the stack 
                // protector. The stack protector (/GS) is a security feature of the compiler 
                // which makes it more difficult to exploit stack buffer overflow memory 
                // corruption vulnerabilities. Disabling the stack protector, even on a 
                // function -by-function basis, is disallowed by SDL policy. To resolve this 
                // issue, remove occurrences of __declspec(safebuffers) from your code. If the 
                // additional code inserted by the stack protector has been shown in profiling 
                // to cause a significant performance problem for your application, attempt to
                // move stack buffer modifications out of the hot path of execution to allow the 
                // compiler to avoid inserting stack protector checks in these locations rather 
                // than disabling the stack protector altogether.
                context.Logger.Log(MessageKind.Fail, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.DoNotDisableStackProtectionForFunctions_Pass, functionNames));
                return;
            }

            // '{0}' is a C or C++ binary built with the stack protector buffer 
            // security feature enabled which does not disable protection for 
            // any individual functions (via __declspec(safebuffers), making it 
            // more difficult for an attacker to exploit stack buffer overflow 
            // memory corruption vulnerabilities.
            context.Logger.Log(MessageKind.Pass, context,
                RuleUtilities.BuildMessage(context,
                    RulesResources.DoNotDisableStackProtectionForFunctions_Pass));
        }
    }
}
