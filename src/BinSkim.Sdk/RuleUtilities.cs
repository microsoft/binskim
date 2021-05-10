//// Copyright (c) Microsoft. All rights reserved.
//// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//using System;
//using System.Diagnostics;
//using System.Globalization;
//using System.IO;

//namespace Microsoft.CodeAnalysis.IL.Sdk
//{
//    public static class RuleUtilities
//    {
//        public static string BuildObsoleteToolsMessage(Version actual, Version expected)
//        {
//            return string.Format(MetadataConditions.ImageCompiledWithOutdatedTools,
//                actual.ToString(),
//                expected.ToString());
//        }

//        public static string BuildTargetNotAnalyzedMessage(string targetPath, string ruleName, string reason)
//        {
//            targetPath = Path.GetFileName(targetPath);

//            // Image '{0}' was not evaluated for check '{1}' as the analysis
//            // is not relevant based on observed metadata: {2}
//            return String.Format(
//                CultureInfo.InvariantCulture,
//                SdkResources.TargetNotAnalyzed_NotApplicable,
//                targetPath,
//                ruleName,
//                reason);
//        }

//        public static string BuildRuleDisabledDueToMissingPolicyMessage(string ruleName, string reason)
//        {
//            // BinSkim command-line using the --policy argument (recommended), or 
//            // pass --defaultPolicy to invoke built-in settings. Invoke the 
//            // BinSkim.exe 'export' command to produce an initial policy file 
//            // that can be edited if required and passed back into the tool.
//            return String.Format(
//                CultureInfo.InvariantCulture,
//                SdkResources.RuleWasDisabledDueToMissingPolicy,
//                ruleName,
//                reason);
//        }
//    }
//}
