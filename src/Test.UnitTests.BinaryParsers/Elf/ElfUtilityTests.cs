// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class ElfUtilityTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("test1")]
        [InlineData("test1", "test2")]
        [InlineData("test1", "test2", "test3")]
        [InlineData("", "", "test", "", "test2", "")]
        public void NullTermAsciiToStrings_WorksOnExpectedData(params string[] testStrings)
        {
            var testData = new List<byte>();
            foreach (string str in testStrings)
            {
                testData.AddRange(System.Text.Encoding.ASCII.GetBytes(str.ToCharArray()));
                testData.Add(0);
            }
            string[] output = ElfUtility.NullTermAsciiToStrings(testData.ToArray());
            output.Should().BeEquivalentTo(testStrings);
        }

        [Theory]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 01, 01, })]
        [InlineData(new byte[] { 01, 02, 00, 01, 01 })]
        public void NullTermAsciiToStrings_ThrowsArgumentExceptionForInvalidData(byte[] data)
        {
            Assert.Throws<ArgumentException>(() => ElfUtility.NullTermAsciiToStrings(data));
        }

        [Fact]
        public void NullTermAsciiToStrings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ElfUtility.NullTermAsciiToStrings(null));
        }

        [Fact]
        public void GetELFCompilers_IgnoresLeadingNullByte()
        {
            const string rustDescription = "rustc version 1.95.0 (ed80dadd6a 2026-06-18) (1.95.0-ms-20260618.5+ed80dadd6a)";
            const string linkerDescription = "Linker: LLD 22.1.2";
            var commentData = new List<byte> { 0 };
            commentData.AddRange(Encoding.ASCII.GetBytes(rustDescription));
            commentData.Add(0);
            commentData.AddRange(Encoding.ASCII.GetBytes(linkerDescription));
            commentData.Add(0);

            ElfCompiler[] compilers = ElfUtility.GetELFCompilers(commentData.ToArray());

            compilers.Should().HaveCount(2);
            compilers[0].Compiler.Should().Be(ElfCompilerType.Rust);
            compilers[0].Version.Should().Be(new Version(1, 95, 0));
            compilers[0].FullDescription.Should().Be(rustDescription);
            compilers[1].Compiler.Should().Be(ElfCompilerType.Unknown);
            compilers[1].FullDescription.Should().Be(linkerDescription);
        }

        [Theory]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 0, 0 })]
        public void GetELFCompilers_EmptyCommentsReturnUnknownCompiler(byte[] commentData)
        {
            ElfCompiler[] compilers = ElfUtility.GetELFCompilers(commentData);

            compilers.Should().ContainSingle();
            compilers[0].Compiler.Should().Be(ElfCompilerType.Unknown);
        }
    }
}
