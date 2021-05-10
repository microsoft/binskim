// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal static class BinScopeCompatibility
    {
        // Mapping of BinSkim rule IDs to Binscope friendly names for backwards compatability.
        public const string EquivalentBinScopeRulePropertyName = "equivalentBinScopeRuleReadableName";
        private static readonly Dictionary<string, string> binScopeRuleReadableNameMapping = new Dictionary<string, string>()
        {
            {RuleIds.LoadImageAboveFourGigabyteAddress,  "FourGbCheck" }, //BA2001
            {RuleIds.DoNotIncorporateVulnerableDependencies , "ATLVersionCheck" }, //BA2002
            {RuleIds.DoNotShipVulnerableBinaries, "BinaryVersionsCheck"}, //BA2005
            {RuleIds.BuildWithSecureTools, "CompilerVersionCheck" }, //BA2006
            {RuleIds.EnableCriticalCompilerWarnings, "CompilerWarningsCheck" }, //BA2007
            {RuleIds.EnableControlFlowGuard, "ControlFlowGuardCheck" }, //BA2008
            {RuleIds.EnableAddressSpaceLayoutRandomization, "DBCheck" }, //BA2009
            {RuleIds.DoNotMarkImportsSectionAsExecutable, "ExecutableImportsCheck" }, //BA2010
            {RuleIds.EnableStackProtection, "GSCheck" }, //BA2011
            {RuleIds.DoNotModifyStackProtectionCookie, "DefaultGSCookieCheck" }, //BA2012
            {RuleIds.InitializeStackProtection, "GSFriendlyInitCheck" }, //BA2013
            {RuleIds.DoNotDisableStackProtectionForFunctions, "GSFunctionSafeBuffersCheck" }, //BA2014
            {RuleIds.EnableHighEntropyVirtualAddresses, "HighEntropyVACheck" }, //BA2015
            {RuleIds.MarkImageAsNXCompatible, "NXCheck" }, //BA2016
            {RuleIds.EnableSafeSEH, "SafeSEHCheck" }, //BA2018
            {RuleIds.DoNotMarkWritableSectionsAsShared, "SharedSectionCheck" }, //BA2019
            {RuleIds.DoNotMarkWritableSectionsAsExecutable, "WXCheck" } //BA2021
        };

        /// <summary>
        /// Gets the readable name of the equivalent BinScope check for a BinSkim Rule Id.
        /// Note that for checks which exist only in BinSkim, this will return null.
        /// </summary>
        /// <param name="ruleId">The rule ID for the BinSkim check.</param>
        /// <returns>The BinScope rule readable name if one exists, null otherwise.</returns>
        public static string GetBinScopeRuleReadableName(string ruleId)
        {
            binScopeRuleReadableNameMapping.TryGetValue(ruleId, out string result);
            return result;
        }
    }
}
