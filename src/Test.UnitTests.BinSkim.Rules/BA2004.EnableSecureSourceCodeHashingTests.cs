// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class EnableSecureSourceCodeHashingTests
    {
        [Fact]
        public void IsLikelyUwpDummyObjTests()
        {
            EnableSecureSourceCodeHashing.IsLikelyUwpDummyObj(Language.MASM, @"c:\dummy.obj", @"c:\dummy.obj").Should().BeTrue();
            EnableSecureSourceCodeHashing.IsLikelyUwpDummyObj(Language.MASM, @"d:\dummy.obj", @"d:\dummy.obj").Should().BeFalse();
            EnableSecureSourceCodeHashing.IsLikelyUwpDummyObj(Language.C, @"c:\dummy.obj", @"c:\dummy.obj").Should().BeFalse();
            EnableSecureSourceCodeHashing.IsLikelyUwpDummyObj(Language.MASM, @"c:\Dummy.obj", @"c:\Dummy.obj").Should().BeFalse();
            EnableSecureSourceCodeHashing.IsLikelyUwpDummyObj(Language.MASM, "AnyLib", @"c:\dummy.obj").Should().BeFalse();
        }
    }
}
