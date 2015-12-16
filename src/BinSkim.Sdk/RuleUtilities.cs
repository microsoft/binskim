// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public static class RuleUtilities
    {
        public static string BuildObsoleteToolsMessage(Version actual, Version expected)
        {
            return string.Format(MetadataConditions.ImageCompiledWithOutdatedTools,
                actual.ToString(),
                expected.ToString());
        }

        public static string BuildMessage(BinaryAnalyzerContext context, string messageFormatString, params string[] arguments)
        {
            // By convention, the first argument is always the target name, 
            // which we retrieve from the context
            Debug.Assert(File.Exists(context.PE.FileName));
            string targetName = Path.GetFileName(context.PE.FileName);

            string[] fullArguments = new string[arguments != null ? arguments.Length + 1 : 1];
            fullArguments[0] = targetName;

            if (fullArguments.Length > 1)
            {
                arguments.CopyTo(fullArguments, 1);
            }

            return String.Format(CultureInfo.InvariantCulture,
                messageFormatString, fullArguments);
        }

        public static string BuildCouldNotLoadPdbMessage(BinaryAnalyzerContext context)
        {
            Debug.Assert(context.Pdb == null);
            Debug.Assert(context.PdbParseException != null);

            string ruleName = context.Rule.Name;
            string targetPath = Path.GetFileName(context.PE.FileName);
            string exceptionMessage = context.PdbParseException.Message;

            // Image '{0}' was not evaluated for check '{1}' as  
            // an exception occurred loading its pdb: '{3}'
            return String.Format(
                CultureInfo.InvariantCulture,
                SdkResources.TargetNotAnalyzed_MissingPdb,
                targetPath,
                ruleName,
                SdkResources.MetadataCondition_CouldNotLoadPdb,
                exceptionMessage);
        }

        public static string BuildTargetNotAnalyzedMessage(string targetPath, string ruleName, string reason)
        {
            targetPath = Path.GetFileName(targetPath);

            // Image '{0}' was not evaluated for check '{1}' as the analysis
            // is not relevant based on observed metadata: {2}
            return String.Format(
                CultureInfo.InvariantCulture,
                SdkResources.TargetNotAnalyzed_NotApplicable,
                targetPath,
                ruleName,
                reason);
        }

        public static string BuildRuleDisabledDueToMissingPolicyMessage(string ruleName, string reason)
        {
            // BinSkim command-line using the --policy argument (recommended), or 
            // pass --defaultPolicy to invoke built-in settings. Invoke the 
            // BinSkim.exe 'export' command to produce an initial policy file 
            // that can be edited if required and passed back into the tool.
            return String.Format(
                CultureInfo.InvariantCulture,
                SdkResources.RuleWasDisabledDueToMissingPolicy,
                ruleName,
                reason);
        }
    }
}
