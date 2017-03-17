// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    static class BinScopeCompatibility
    {
        // Mapping of BinSkim rule IDs to Binscope friendly names for backwards compatability.
        public const string EquivalentBinScopeRulePropertyName = "equivalentBinScopeRuleReadableName";
        private static Dictionary<string, string> binScopeRuleReadableNameMapping = new Dictionary<string, string>()
        {
            {RuleIds.LoadImageAboveFourGigabyteAddressId,  "FourGbCheck" }, //BA2001
            {RuleIds.DoNotIncorporateVulnerableDependenciesId , "ATLVersionCheck" }, //BA2002
            {RuleIds.DoNotShipVulnerableBinariesId, "BinaryVersionsCheck"}, //BA2005
            {RuleIds.BuildWithSecureToolsId, "CompilerVersionCheck" }, //BA2006
            {RuleIds.EnableCriticalCompilerWarningsId, "CompilerWarningsCheck" }, //BA2007
            {RuleIds.EnableControlFlowGuardId, "ControlFlowGuardCheck" }, //BA2008
            {RuleIds.EnableAddressSpaceLayoutRandomizationId, "DBCheck" }, //BA2009
            {RuleIds.DoNotMarkImportsSectionAsExecutableId, "ExecutableImportsCheck" }, //BA2010
            {RuleIds.EnableStackProtectionId, "GSCheck" }, //BA2011
            {RuleIds.DoNotModifyStackProtectionCookieId, "DefaultGSCookieCheck" }, //BA2012
            {RuleIds.InitializeStackProtectionId, "GSFriendlyInitCheck" }, //BA2013
            {RuleIds.DoNotDisableStackProtectionForFunctionsId, "GSFunctionSafeBuffersCheck" }, //BA2014
            {RuleIds.EnableHighEntropyVirtualAddressesId, "HighEntropyVACheck" }, //BA2015
            {RuleIds.MarkImageAsNXCompatibleId, "NXCheck" }, //BA2016
            {RuleIds.EnableSafeSEHId, "SafeSEHCheck" }, //BA2018
            {RuleIds.DoNotMarkWritableSectionsAsSharedId, "SharedSectionCheck" }, //BA2019
            {RuleIds.DoNotMarkWritableSectionsAsExecutableId, "WXCheck" } //BA2021
        };

        /// <summary>
        /// Gets the readable name of the equivalent BinScope check for a BinSkim Rule Id.
        /// Note that for checks which exist only in BinSkim, this will return null.
        /// </summary>
        /// <param name="ruleId">The rule ID for the BinSkim check.</param>
        /// <returns>The BinScope rule readable name if one exists, null otherwise.</returns>
        public static string GetBinScopeRuleReadableName(string ruleId)
        {
            string result;
            binScopeRuleReadableNameMapping.TryGetValue(ruleId, out result);
            return result;
        }
    }
}
