// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;

using Xunit;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class DriverUtilitiesTests
    {
        [Fact]
        public void GetInaccessibleSymbolPathsTests()
        {
            DriverUtilities.GetInaccessibleSymbolPaths(null)
                .Should().BeEquivalentTo(Array.Empty<string>());
            DriverUtilities.GetInaccessibleSymbolPaths("")
                .Should().BeEquivalentTo(Array.Empty<string>());
            DriverUtilities.GetInaccessibleSymbolPaths("SRV*http://msdl.microsoft.com/download/symbols")
                .Should().BeEquivalentTo(Array.Empty<string>());
            DriverUtilities.GetInaccessibleSymbolPaths("SRV*c:\\symbols*http://msdl.microsoft.com/download/symbols")
                .Should().BeEquivalentTo(Array.Empty<string>());
            DriverUtilities.GetInaccessibleSymbolPaths("CACHE*c:\\symbols;SRV*http://notexists/SymStore*http://notexists2/SymStore")
                .Should().BeEquivalentTo(new[] { "http://notexists/SymStore", "http://notexists2/SymStore" });
        }
    }
}
