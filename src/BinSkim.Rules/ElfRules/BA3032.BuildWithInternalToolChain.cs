/// <summary>
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    // Delete the following attribute if this rule doesn't require special
    // configuration capabilities. Delete this code comment in all cases.
    //
    // You should extend this class from WindowsBinaryAndPdbSkimmerBase 
    // instead of PEBinarySkimmerBase if you require PDB parsing in 
    // your check. Extend ELFBinarySkimmerBase for *nix binary checks.
    // 
    [Export(typeof(IOptionsProvider))]
    [Export(typeof(ReportingDescriptor))]
    [Export(typeof(Skimmer<BinaryAnalyzerContext>))]
    public class BuildWithInternalToolChain : PEBinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA3032
        /// </summary>
        public override string Id => RuleIds.BuildWithInternalToolChain;

        /// <summary>
        /// Recapitulate the full text of the rule description returned below
        /// here as a summary comment.
        /// </summary>

        public override MultiformatMessageString FullDescription =>
            new MultiformatMessageString { Text = RuleResources.BA3032_BuildWithInternalToolChain_Description };

        protected override ICollection<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA3032_Pass),
                    nameof(RuleResources.BA3032_Error),
                    nameof(RuleResources.BA3032_BuildWithInternalToolChain_Description)
                };

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                MinimumRequiredLinkerVersion
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.BuildWithInternalToolChain + "." + nameof(BuildWithInternalToolChain);

        public static PerLanguageOption<Version> MinimumRequiredLinkerVersion { get; } =
            new PerLanguageOption<Version>(
                AnalyzerName, nameof(MinimumRequiredLinkerVersion), defaultValue: () => new Version("14.0"));

        public AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            PE portableExecutable = target.PE;
            Pdb pdb = target.Pdb;
            AnalysisApplicability result = AnalysisApplicability.NotApplicableToSpecifiedTarget;
            
            // Review the range of metadata conditions and return NotApplicableToSpecifiedTarget
            // from this method for all cases where a binary is detected that is not valid to scan.
            //
            reasonForNotAnalyzing = MetadataConditions.ImageIsResourceOnlyBinary;
            if (portableExecutable.IsResourceOnly) { return result; }

            // Here's an example of parameterizing a rule from input XML. In this example,
            // we enforce that the linker is of a minimal version, otherwise the scan will
            // not occur (because the toolset producing the binary is not sufficiently 
            // current to enable the security mitigation).
            //
            Version minimumRequiredLinkerVersion = policy.GetProperty(MinimumRequiredLinkerVersion);

            if (portableExecutable.LinkerVersion < minimumRequiredLinkerVersion)
            {
                reasonForNotAnalyzing = string.Format(
                    MetadataConditions.ImageCompiledWithOutdatedTools,
                    portableExecutable.LinkerVersion,
                    minimumRequiredLinkerVersion);

                return result;
            }

            // If we get to this location, we've determined the binary is valid to analyze.
            // We clear the 'reasonForNotAnalyzing' output variable and return 
            // ApplicableToSpecifiedTarget.
            //
            reasonForNotAnalyzing = null;
            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();

                if (omDetails.Language == Language.Rust && 
                    omDetails.WellKnownCompiler != WellKnownCompilers.ClangLLVMRustc && 
                    omDetails.CompilerName.Contains(CompilerNames.ClangLLVMRustcPrefix)) //omDetails.Language == Language.Rust &&
                {   //fail
                    //todo add context 

                    // '{0}' was compiled with one or more modules which were not built using
                    // minimum required tool versions ({1}). More recent toolchains
                    // contain mitigations that make it more difficult for an attacker to exploit
                    // vulnerabilities in programs they produce. To resolve this issue, compile
                    // and /or link your binary with more recent tools. If you are servicing a
                    // product where the tool chain cannot be modified (e.g. producing a hotfix
                    // for an already shipped version) ignore this warning. Modules built outside
                    // of policy: {2}
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                        nameof(RuleResources.BA3032_BuildWithInternalToolChain_Description),
                            context.CurrentTarget.Uri.GetFileName(),
                            minimumRequiredCompilers,
                            outOfPolicyModulesText));
                    return AnalysisApplicability.ApplicableToSpecifiedTarget;
                }
            }
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PE pe = target.PE;
            Pdb pdb = target.Pdb;
            // Analysis may return one or more failures, each of which uses a format 
            // string stored in the MessageResourceNames array above to produce a result.
            // By convention, we recapitulate that format string when we return the 
            // associated result, to document the specific failure or pass condition.
            foreach (DisposableEnumerableView<Symbol> omView in pdb.CreateObjectModuleIterator())
            {
                Symbol om = omView.Value;
                ObjectModuleDetails omDetails = om.GetObjectModuleDetails();
                if (omDetails.Language == Language.Rust && omDetails.WellKnownCompiler != WellKnownCompilers.ClangLLVMRustc && omDetails.CompilerName.Contains(CompilerNames.ClangLLVMRustcPrefix))
                {
                    // '{0}' is not secure for some reaons. 
                    // To resolve this issue, pass /beEvenMoreSecure on both the compiler
                    // and linker command lines. Binaries also require the 
                    // /beSecure option in order to enable the enhanced setting.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                            nameof(RuleResources.BA3032_Error),
                            context.CurrentTarget.Uri.GetFileName()));
                    return;
                }
            }

            // '{0}' enables /beEvenMoreSecure on both the compiler and linker
            // command-lines, preventing a broad range of conditions that 
            // bad actors can use to engage in their malignant, unfortunately
            // often-profitable foolishness.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA3032_Pass),
                        context.CurrentTarget.Uri.GetFileName()));
        }

        // Not considered a meaningful method name. Be sure to do a better job.
        private bool IsSecure(PEBinary _)
        {
            // Add relevant PE-level examination 
            return false;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            return base.CanAnalyze(context, out reasonForNotAnalyzing);
        }

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            throw new NotImplementedException();
        }
    }
}
