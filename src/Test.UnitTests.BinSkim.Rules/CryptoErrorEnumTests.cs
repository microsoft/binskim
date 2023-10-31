// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class CryptoErrorEnumTests
    {
        [Fact]
        public void CryptoErrorEnumTest_NoMissingValues()
        {
            IEnumerable<CryptoError> enumValues = Enum.GetValues(typeof(CryptoError)).Cast<CryptoError>();
            Dictionary<CryptoError, string> dictionaryValues = RulesExtensionMethods.BuildCryptoErrorDescriptions();
            var missingValues = enumValues.Where(value => !dictionaryValues.ContainsKey(value)).ToList();
            missingValues.Should().HaveCount(0, "BuildCryptoErrorDescriptions() should contain all cases from CryptoError.");
        }
    }
}
