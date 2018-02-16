// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class ELFUtilityTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("test1")]
        [InlineData("test1", "test2")]
        [InlineData("test1", "test2", "test3")]
        [InlineData("", "", "test", "", "test2", "")]
        public void NullTermAsciiToStrings_WorksOnExpectedData(params string[] testStrings)
        {
            List<byte> testData = new List<byte>();
            foreach(var str in testStrings)
            {
                testData.AddRange(System.Text.Encoding.ASCII.GetBytes(str.ToCharArray()));
                testData.Add(0);
            }
            string[] output = ELFUtility.NullTermAsciiToStrings(testData.ToArray());
            output.Should().BeEquivalentTo(testStrings);
        }

        [Theory]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 01, 01, })]
        [InlineData(new byte[] { 01, 02, 00, 01, 01})]
        public void NullTermAsciiToStrings_ThrowsArgumentExceptionForInvalidData(byte[] data)
        {
            Assert.Throws<ArgumentException>(() => ELFUtility.NullTermAsciiToStrings(data));
        }

        [Fact]
        public void NullTermAsciiToStrings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ELFUtility.NullTermAsciiToStrings(null));
        }
    }
}
