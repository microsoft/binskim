// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Unit tests for <see cref="DwarfAttributeValue"/>.
    /// </summary>
    public class DwarfAttributeValueTests
    {
        [Fact]
        public void Address_ReturnsUnderlyingUlong()
        {
            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Address,
                Value = 0x12345678UL,
            };

            value.Address.Should().Be(0x12345678UL);
        }

        [Fact]
        public void Block_ReturnsUnderlyingByteArray()
        {
            byte[] bytes = { 0x01, 0x02, 0x03 };

            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Block,
                Value = bytes,
            };

            value.Block.Should().Equal(0x01, 0x02, 0x03);
        }

        [Fact]
        public void Constant_ReturnsUnderlyingUlong_WhenValueIsUlong()
        {
            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Constant,
                Value = 42UL,
            };

            value.Constant.Should().Be(42UL);
        }

        [Theory]
        [InlineData(new byte[] { 0x2A }, 0x2AUL)] // 1 byte
        [InlineData(new byte[] { 0x34, 0x12 }, 0x1234UL)] // 2 bytes (LE)
        [InlineData(new byte[] { 0x78, 0x56, 0x34, 0x12 }, 0x12345678UL)] // 4 bytes (LE)
        [InlineData(new byte[] { 0xEF, 0xCD, 0xAB, 0x90, 0x78, 0x56, 0x34, 0x12 }, 0x1234567890ABCDEFUL)] // 8 bytes (LE)
        public void Constant_DecodesSupportedByteArrayLengths(byte[] bytes, ulong expected)
        {
            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Constant,
                Value = bytes,
            };

            value.Constant.Should().Be(expected);
        }

        [Fact]
        public void Constant_ThrowsForUnsupportedByteArrayLength()
        {
            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Constant,
                Value = new byte[] { 0x01, 0x02, 0x03 }, // 3 bytes → not implemented
            };

            Action act = () => _ = value.Constant;

            act.Should().Throw<NotImplementedException>();
        }

        [Fact]
        public void String_ReturnsUnderlyingString()
        {
            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.String,
                Value = "hello",
            };

            value.String.Should().Be("hello");
        }

        [Fact]
        public void Flag_ReturnsUnderlyingBool()
        {
            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Flag,
                Value = true,
            };

            value.Flag.Should().BeTrue();
        }

        [Fact]
        public void Reference_ReturnsUnderlyingDwarfSymbol()
        {
            var symbol = new DwarfSymbol();

            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.ResolvedReference,
                Value = symbol,
            };

            value.Reference.Should().BeSameAs(symbol);
        }

        [Fact]
        public void ExpressionLocation_ReturnsUnderlyingByteArray()
        {
            byte[] bytes = { 0xAA, 0xBB };

            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.ExpressionLocation,
                Value = bytes,
            };

            value.ExpressionLocation.Should().Equal(0xAA, 0xBB);
        }

        [Fact]
        public void SecOffset_ReturnsUnderlyingUlong()
        {
            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.SecOffset,
                Value = 0xCAFEBABEUL,
            };

            value.SecOffset.Should().Be(0xCAFEBABEUL);
        }

        // ---- Equality and hash code semantics ----

        [Fact]
        public void Equals_ReturnsTrue_WhenBothNull()
        {
            DwarfAttributeValue left = null;
            DwarfAttributeValue right = null;

            (left == right).Should().BeTrue();
            (left != right).Should().BeFalse();
        }

        [Fact]
        public void Equals_ReturnsFalse_WhenOnlyOneNull()
        {
            var value = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Constant,
                Value = 1UL,
            };

            (value == null).Should().BeFalse();
            (null == value).Should().BeFalse();
            (value != null).Should().BeTrue();
            (null != value).Should().BeTrue();
        }

        [Fact]
        public void Equals_ReturnsFalse_WhenTypesDiffer()
        {
            var value1 = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Constant,
                Value = 1UL,
            };

            var value2 = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Address,
                Value = 1UL,
            };

            (value1 == value2).Should().BeFalse();
            (value1 != value2).Should().BeTrue();
        }

        [Fact]
        public void Equals_HandledScalarTypes_UseUnderlyingUlong()
        {
            var constant1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Constant, Value = 10UL };
            var constant2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Constant, Value = 10UL };
            var constant3 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Constant, Value = 11UL };

            (constant1 == constant2).Should().BeTrue();
            (constant1 == constant3).Should().BeFalse();

            var address1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Address, Value = 0x1000UL };
            var address2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Address, Value = 0x1000UL };
            var address3 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Address, Value = 0x2000UL };

            (address1 == address2).Should().BeTrue();
            (address1 == address3).Should().BeFalse();

            var secOffset1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.SecOffset, Value = 5UL };
            var secOffset2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.SecOffset, Value = 5UL };
            var secOffset3 = new DwarfAttributeValue { Type = DwarfAttributeValueType.SecOffset, Value = 6UL };

            (secOffset1 == secOffset2).Should().BeTrue();
            (secOffset1 == secOffset3).Should().BeFalse();

            var reference1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Reference, Value = 123UL };
            var reference2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Reference, Value = 123UL };
            var reference3 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Reference, Value = 124UL };

            (reference1 == reference2).Should().BeTrue();
            (reference1 == reference3).Should().BeFalse();
        }

        [Fact]
        public void Equals_HandledArrayAndStringTypes_UseSequenceAndStringComparison()
        {
            var block1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Block, Value = new byte[] { 0x01, 0x02 } };
            var block2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Block, Value = new byte[] { 0x01, 0x02 } };
            var block3 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Block, Value = new byte[] { 0x01, 0x03 } };

            (block1 == block2).Should().BeTrue();
            (block1 == block3).Should().BeFalse();

            var expr1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.ExpressionLocation, Value = new byte[] { 0xAA } };
            var expr2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.ExpressionLocation, Value = new byte[] { 0xAA } };
            var expr3 = new DwarfAttributeValue { Type = DwarfAttributeValueType.ExpressionLocation, Value = new byte[] { 0xBB } };

            (expr1 == expr2).Should().BeTrue();
            (expr1 == expr3).Should().BeFalse();

            var flag1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Flag, Value = true };
            var flag2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Flag, Value = true };
            var flag3 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Flag, Value = false };

            (flag1 == flag2).Should().BeTrue();
            (flag1 == flag3).Should().BeFalse();

            var string1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "name" };
            var string2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = new string("name".ToCharArray()) };
            var string3 = new DwarfAttributeValue { Type = DwarfAttributeValueType.String, Value = "other" };

            (string1 == string2).Should().BeTrue();
            (string1 == string3).Should().BeFalse();
        }

        [Fact]
        public void Equals_UnhandledTypes_IgnoreValueAndTreatSameTypeAsEqual()
        {
            var invalid1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Invalid, Value = 1UL };
            var invalid2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Invalid, Value = 2UL };

            (invalid1 == invalid2).Should().BeTrue();

            var resolved1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.ResolvedReference, Value = new DwarfSymbol() };
            var resolved2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.ResolvedReference, Value = new DwarfSymbol() };

            (resolved1 == resolved2).Should().BeTrue();

            var loclistx1 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Loclistx, Value = 10UL };
            var loclistx2 = new DwarfAttributeValue { Type = DwarfAttributeValueType.Loclistx, Value = 20UL };

            (loclistx1 == loclistx2).Should().BeTrue();
        }

        [Fact]
        public void GetHashCode_MatchesForEqualValues_OnHandledType()
        {
            var v1 = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Constant,
                Value = 123UL,
            };

            var v2 = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Constant,
                Value = 123UL,
            };

            v1.Equals(v2).Should().BeTrue();
            v1.GetHashCode().Should().Be(v2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_MayDifferForEqualValues_OnUnhandledType()
        {
            var v1 = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Loclistx,
                Value = 1UL,
            };

            var v2 = new DwarfAttributeValue
            {
                Type = DwarfAttributeValueType.Loclistx,
                Value = 2UL,
            };

            // Current implementation considers these equal but uses Value in the hash code.
            v1.Equals(v2).Should().BeTrue();
            v1.GetHashCode().Should().NotBe(v2.GetHashCode());
        }
    }
}
