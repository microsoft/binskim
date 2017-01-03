// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule)), Export(typeof(IOptionsProvider))]
    public class DoNotShipVulnerableBinaries : BinarySkimmerBase, IOptionsProvider
    {
        /// <summary>
        /// BA2005
        /// </summary>
        public override string Id { get { return RuleIds.DoNotShipVulnerableBinariesId; } }

        /// <summary>
        /// Do not ship obsolete libraries for which there are known security vulnerabilities.
        /// </summary>
        public override string FullDescription
        {
            get { return RuleResources.BA2005_DoNotShipVulnerableBinaries_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2005_Pass),
                    nameof(RuleResources.BA2005_Error),
                    nameof(RuleResources.BA2005_Error_CouldNotParseVersion),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public IEnumerable<IOption> GetOptions()
        {
            return new List<IOption>
            {
                VulnerableBinaries,
            }.ToImmutableArray();
        }

        private const string AnalyzerName = RuleIds.DoNotShipVulnerableBinariesId + "." + nameof(DoNotShipVulnerableBinaries);

        private static StringToVersionMap BuildDefaultVulnerableBinariesMap()
        {
            var result = new StringToVersionMap();
            result["msxml6.dll"] = new Version(6, 30);
            result["xmllite.dll"] = new Version(1, 3);
            result["msidcrl.dll"] = new Version(7, 0);
            return result;
        }

        public static PerLanguageOption<StringToVersionMap> VulnerableBinaries { get; } =
            new PerLanguageOption<StringToVersionMap>(
                AnalyzerName, nameof(VulnerableBinaries), defaultValue: () => { return BuildDefaultVulnerableBinariesMap(); });

        // \d+(\.\d+){0,3}
        // 
        // Match a single character that is a “digit” (0–9 in any Unicode script) «\d+»
        //    Between one and unlimited times, as many times as possible, giving back as needed (greedy) «+»
        // Match the regex below «(\.\d+){0,3}»
        //    Between zero and 3 times, as many times as possible, giving back as needed (greedy) «{0,3}»
        //    Match the character “.” literally «\.»
        //    Match a single character that is a “digit” (0–9 in any Unicode script) «\d+»
        //       Between one and unlimited times, as many times as possible, giving back as needed (greedy) «+»
        private static readonly Regex s_versionRegex = new Regex(@"\d+(\.\d+){0,3}", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

        public override void Analyze(BinaryAnalyzerContext context)
        {
            string fileName = Path.GetFileName(context.PE.FileName);

            Version minimumVersion;
            if (context.Policy.GetProperty(VulnerableBinaries).TryGetValue(fileName, out minimumVersion))
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(Path.GetFullPath(context.PE.FileName));
                string rawVersion = fvi.FileVersion;
                Match sanitizedVersion = s_versionRegex.Match(rawVersion);
                if (!sanitizedVersion.Success)
                {
                    // Version information for '{0}' could not be parsed. The binary therefore could not be verified not to be an obsolete binary that is known to be vulnerable to one or more security problems.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                            nameof(RuleResources.BA2005_Error_CouldNotParseVersion),
                            context.TargetUri.GetFileName()));
                    return;
                }

                var actualVersion = new Version(sanitizedVersion.Value);
                if (actualVersion < minimumVersion)
                {
                    // '{0}' appears to be an obsolete library (version {1}) for which there are one
                    // or more known security vulnerabilities. To resolve this issue, obtain a version 
                    //of {0} that is version {2} or greater. If this binary is not in fact {0}, 
                    // ignore this warning.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                            nameof(RuleResources.BA2005_Error),
                            context.TargetUri.GetFileName(),
                            sanitizedVersion.Value,
                            minimumVersion.ToString()));
                    return;
                }
            }

            // '{0}' is not known to be an obsolete binary that is 
            //vulnerable to one or more security problems.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                    nameof(RuleResources.BA2005_Pass),
                    context.TargetUri.GetFileName()));
        }
    }
}
