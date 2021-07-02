// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class RuleTests
    {
        [Fact]
        public void BAXXXX_RULEFRIENDLYNAME_Fail()
        {
            // This example is for a PDB parsing rule, which cannot run on 
            // *nix due to lack of interoperability with msdia140.dll. This
            // conditional check (and the false branch) can be deleted 
            // entirely if this scenario isn't in play.
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                // Every PDB parsing rule should return an error if a PDB can't be located.
                // Be sure to delete this code (and remove passing the 'failureConditions`
                // arguments to 'VerifyFail' if not implementing a PDB crawling check.
                var failureConditions = new HashSet<string>
                {
                    MetadataConditions.CouldNotLoadPdb
                };
                this.VerifyFail(
                    new RULEFRIENDLYNAME(),
                    this.GetTestFilesMatchingConditions(failureConditions),
                    useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new RULEFRIENDLYNAME(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void BAXXXX_RULEFRIENDLYNAME_Pass()
        {
            // This conditional check (and the false branch) are only required for PDB reading rules.
            // Delete the conditional and unnecessary branch for checks that don't crawl PDBS.
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                this.VerifyPass(new RULEFRIENDLYNAME(), useDefaultPolicy: true);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new RULEFRIENDLYNAME(), useDefaultPolicy: true);
            }
        }

        [Fact]
        public void BAXXXX_NotApplicable()
        {
            // Be sure to add an exhaustive list of metadata conditions here
            // that apply to the check (i.e., all the conditions that are
            // referenced in the check CanAnalyze implementation).
            var notApplicableTo = new HashSet<string>
            {
                MetadataConditions.ImageIsResourceOnlyBinary,
                MetadataConditions.ImageIsILOnlyAssembly
            };

            this.VerifyNotApplicable(new RULEFRIENDLYNAME(), notApplicableTo);

            // This conditional check (and the false branch) are only required for PDB reading rules.
            // Delete the conditional and unnecessary branch for checks that don't crawl PDBS.
            // Note that this code is explicitly detecting that the rule positively identifies 
            // 64-bit binaries as scan target candidates.
            if (BinaryParsers.PlatformSpecificHelpers.RunningOnWindows())
            {
                var applicableTo = new HashSet<string> { MetadataConditions.ImageIs64BitBinary };
                this.VerifyNotApplicable(new RULEFRIENDLYNAME(), applicableTo, AnalysisApplicability.ApplicableToSpecifiedTarget);
            }
            else
            {
                this.VerifyThrows<PlatformNotSupportedException>(new RULEFRIENDLYNAME(), useDefaultPolicy: true);
            }
        }
    }
}