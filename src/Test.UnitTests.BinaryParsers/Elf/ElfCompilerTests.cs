// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class ElfCompilerTestsdir
    {
        [Theory]
        [InlineData("not a match", ElfCompilerType.Unknown, "0.0.0.0")]
        [InlineData("Ubuntu clang version 3.7.1-2ubuntu2 (tags/RELEASE_371/final) (based on LLVM 3.7.1)", ElfCompilerType.Clang, "3.7.1")]
        [InlineData("GCC: (Ubuntu 5.4.0-6ubuntu1~16.04.4) 5.4.0 20160609", ElfCompilerType.GCC, "5.4.0")]
        [InlineData("GHC 7.10.3", ElfCompilerType.Unknown, "7.10.3")]
        [InlineData("GHC 7.10", ElfCompilerType.Unknown, "7.10")]
        [InlineData("GHC 7", ElfCompilerType.Unknown, "0.0.0.0")]
        [InlineData(null, ElfCompilerType.Unknown, "0.0.0.0")]
        public void ELFCompiler_ConstructorHandlesKnownInputs(string compilerString, ElfCompilerType expectedType, string expectedVersion)
        {
            var compiler = new ElfCompiler(compilerString);
            compiler.Compiler.Should().Be(expectedType);
            compiler.Version.Should().Be(new Version(expectedVersion));
        }
    }
}
