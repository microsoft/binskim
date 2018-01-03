// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.IL.Rules
{
    internal static class RuleIds
    {
        // Analysis check ids
        public const string LoadImageAboveFourGigabyteAddressId = "BA2001";
        public const string DoNotIncorporateVulnerableDependenciesId = "BA2002";

        // 2004 open. Previously for specific ATL implementation verification

        // Id gap relates to unported analysis 
        public const string DoNotShipVulnerableBinariesId = "BA2005";
        public const string BuildWithSecureToolsId = "BA2006";
        public const string EnableCriticalCompilerWarningsId = "BA2007";
        public const string EnableControlFlowGuardId = "BA2008";
        public const string EnableAddressSpaceLayoutRandomizationId = "BA2009";
        public const string DoNotMarkImportsSectionAsExecutableId = "BA2010";
        public const string EnableStackProtectionId = "BA2011";
        public const string DoNotModifyStackProtectionCookieId = "BA2012";
        public const string InitializeStackProtectionId = "BA2013";
        public const string DoNotDisableStackProtectionForFunctionsId = "BA2014";
        public const string EnableHighEntropyVirtualAddressesId = "BA2015";
        public const string MarkImageAsNXCompatibleId = "BA2016";

        // 2017 open. Previously for 'do not link static crypto' check

        public const string EnableSafeSEHId = "BA2018";
        public const string DoNotMarkWritableSectionsAsSharedId = "BA2019";

        // 2020 open. Previously for 'do not use vb6' check

        public const string DoNotMarkWritableSectionsAsExecutableId = "BA2021";
        public const string SignCorrectly = "BA2022";
    }
}
