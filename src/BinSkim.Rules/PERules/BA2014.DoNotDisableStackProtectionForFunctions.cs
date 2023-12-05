﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor)), Export(typeof(IOptionsProvider))]
    public class DoNotDisableStackProtectionForFunctions : WindowsBinaryAndPdbSkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2014
        /// </summary>
        public override string Id => RuleIds.DoNotDisableStackProtectionForFunctions;

        /// <summary>
        /// Application code should not disable stack protection for individual functions.
        /// The stack protector (/GS) is a security feature of the Windows native compiler
        /// which makes it more difficult to exploit stack buffer overflow memory corruption
        /// vulnerabilities. Disabling the stack protector, even on a function-by-function
        /// basis, can compromise the security of code. To resolve this issue, remove
        /// occurrences of __declspec(safebuffers) from your code. If the additional code
        /// inserted by the stack protector has been shown in profiling to cause a significant
        /// performance problem for your application, attempt to move stack buffer
        /// modifications out of the hot path of execution to allow the compiler to avoid
        /// inserting stack protector checks in these locations rather than disabling the
        /// stack protector altogether.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2014_DoNotDisableStackProtectionForFunctions_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2014_Pass),
                    nameof(RuleResources.BA2014_Error),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                ApprovedFunctionsThatDisableStackProtection,
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.DoNotDisableStackProtectionForFunctions + "." + nameof(DoNotDisableStackProtectionForFunctions);

        private static StringSet BuildApprovedFunctionsStringSet()
        {
            var result = new StringSet
            {
                "_TlgWrite",
                "__vcrt_trace_logging_provider::_TlgWrite"
            };

            result.UnionWith(StackProtectionUtilities.GSInitializationFunctionNames);
            return result;
        }

        public static PerLanguageOption<StringSet> ApprovedFunctionsThatDisableStackProtection { get; } =
            new PerLanguageOption<StringSet>(
                AnalyzerName, nameof(StringSet), defaultValue: () => BuildApprovedFunctionsStringSet());

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            AnalysisApplicability applicability = StackProtectionUtilities.CommonCanAnalyze(target, out reasonForNotAnalyzing);

            return applicability;
        }

        public override void AnalyzePortableExecutableAndPdb(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            Pdb pdb = target.Pdb;

            var names = new List<string>();
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
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                        nameof(RuleResources.BA2014_Error),
                        context.CurrentTarget.Uri.GetFileName(),
                        functionNames));
                return;
            }

            // '{0}' is a C or C++ binary built with the stack protector buffer 
            // security feature enabled which does not disable protection for 
            // any individual functions (via __declspec(safebuffers), making it 
            // more difficult for an attacker to exploit stack buffer overflow 
            // memory corruption vulnerabilities.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2014_Pass),
                    context.CurrentTarget.Uri.GetFileName()));
        }
    }
}
