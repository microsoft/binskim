// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Unit tests for <see cref="DwarfSymbol"/>.
    /// </summary>
    public class DwarfSymbolTests
    {
        [Fact]
        public void Name_ReturnsNull_WhenNameAttributeMissing()
        {
            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.Variable,
                Attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>()
            };

            symbol.Name.Should().BeNull();
        }

        [Fact]
        public void Name_ReturnsStringAttribute_WhenPresent()
        {
            var attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
            {
                {
                    DwarfAttribute.Name,
                    new DwarfAttributeValue
                    {
                        Type = DwarfAttributeValueType.String,
                        Value = "foo"
                    }
                }
            };

            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.Variable,
                Attributes = attributes
            };

            symbol.Name.Should().Be("foo");
        }

        [Fact]
        public void FullName_ReturnsSimpleName_WhenNoParentOrCompileUnitParent()
        {
            var attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
            {
                {
                    DwarfAttribute.Name,
                    new DwarfAttributeValue
                    {
                        Type = DwarfAttributeValueType.String,
                        Value = "Top"
                    }
                }
            };

            var compileUnit = new DwarfSymbol
            {
                Tag = DwarfTag.CompileUnit,
                Attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>()
            };

            var child = new DwarfSymbol
            {
                Tag = DwarfTag.Subprogram,
                Attributes = attributes,
                Parent = compileUnit
            };

            child.FullName.Should().Be("Top");
        }

        [Fact]
        public void FullName_BuildsQualifiedName_ThroughNonCompileUnitParents()
        {
            // Build hierarchy: ns::Class::Method
            var ns = new DwarfSymbol
            {
                Tag = DwarfTag.Namespace,
                Attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
                {
                    {
                        DwarfAttribute.Name,
                        new DwarfAttributeValue
                        {
                            Type = DwarfAttributeValueType.String,
                            Value = "ns"
                        }
                    }
                }
            };

            var cls = new DwarfSymbol
            {
                Tag = DwarfTag.ClassType,
                Attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
                {
                    {
                        DwarfAttribute.Name,
                        new DwarfAttributeValue
                        {
                            Type = DwarfAttributeValueType.String,
                            Value = "Class"
                        }
                    }
                },
                Parent = ns
            };

            var method = new DwarfSymbol
            {
                Tag = DwarfTag.Subprogram,
                Attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
                {
                    {
                        DwarfAttribute.Name,
                        new DwarfAttributeValue
                        {
                            Type = DwarfAttributeValueType.String,
                            Value = "Method"
                        }
                    }
                },
                Parent = cls
            };

            method.FullName.Should().Be("ns::Class::Method");
        }

        [Fact]
        public void GetConstantAttribute_ReturnsDefault_WhenAttributeMissing()
        {
            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.Variable,
                Attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>()
            };

            symbol.GetConstantAttribute(DwarfAttribute.ByteSize, defaultValue: 42).Should().Be(42UL);
        }

        [Fact]
        public void GetConstantAttribute_ReturnsUnderlyingConstant_WhenPresent()
        {
            var attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>
            {
                {
                    DwarfAttribute.ByteSize,
                    new DwarfAttributeValue
                    {
                        Type = DwarfAttributeValueType.Constant,
                        Value = 8UL
                    }
                }
            };

            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.Variable,
                Attributes = attributes
            };

            symbol.GetConstantAttribute(DwarfAttribute.ByteSize, defaultValue: 0).Should().Be(8UL);
        }

        [Fact]
        public void ToString_IncludesTagOffsetAttributeAndChildCounts()
        {
            var symbol = new DwarfSymbol
            {
                Tag = DwarfTag.Variable,
                Attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>(),
                Children = new List<DwarfSymbol>(),
            };

            // Offset is internal but defaults to 0.
            string text = symbol.ToString();

            text.Should().Contain("Variable");
            text.Should().Contain("Offset = 0");
            text.Should().Contain("Attributes = 0");
            text.Should().Contain("Children = 0");
        }
    }
}
