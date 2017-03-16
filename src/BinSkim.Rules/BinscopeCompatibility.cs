// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    static class BinscopeCompatibility
    {
        // Mapping of BinSkim rule IDs to Binscope friendly names for backwards compatability.
        public const string EquivalentBinscopeRulePropertyName = "equivalentBinscopeReadableName";
        private static Dictionary<string, string> binscopeFriendlyNameMapping = new Dictionary<string, string>()
        {
            {RuleIds.LoadImageAboveFourGigabyteAddressId,  "FourGbCheck" },
            {RuleIds.DoNotIncorporateVulnerableDependenciesId , "ATLVersionCheck" },
            {RuleIds.DoNotShipVulnerableBinariesId, "BinaryVersionsCheck"},
            {RuleIds.BuildWithSecureToolsId, "CompilerVersionCheck" },
            {RuleIds.EnableCriticalCompilerWarningsId, "CompilerWarningsCheck" },
            {RuleIds.EnableControlFlowGuardId, "ControlFlowGuardCheck" },
            {RuleIds.EnableAddressSpaceLayoutRandomizationId, "DBCheck" },
            {RuleIds.DoNotMarkImportsSectionAsExecutableId, "ExecutableImportsCheck" },
            {RuleIds.EnableStackProtectionId, "GSCheck" },
            {RuleIds.DoNotModifyStackProtectionCookieId, "DefaultGSCookieCheck" },
            {RuleIds.InitializeStackProtectionId, "GSFriendlyInitCheck" },
            {RuleIds.DoNotDisableStackProtectionForFunctionsId, "GSFunctionSafeBuffersCheck" },
            {RuleIds.EnableHighEntropyVirtualAddressesId, "HighEntropyVACheck" },
            {RuleIds.MarkImageAsNXCompatibleId, "NXCheck" },
            {RuleIds.EnableSafeSEHId, "SafeSEHCheck" },
            {RuleIds.DoNotMarkWritableSectionsAsSharedId, "SharedSectionCheck" },
            {RuleIds.DoNotMarkWritableSectionsAsExecutableId, "WXCheck" }
        };

        /// <summary>
        /// Finds the 'alternative' rule id
        /// </summary>
        /// <param name="ruleId">The rule ID for the current check.</param>
        /// <returns>The alternative rule id if one exists, null otherwise.</returns>
        public static string GetBinscopeFriendlyName(string ruleId)
        {
            if (binscopeFriendlyNameMapping.ContainsKey(ruleId))
            {
                return binscopeFriendlyNameMapping[ruleId];
            }
            else
            {
                return null;
            }
        }
    }
}
