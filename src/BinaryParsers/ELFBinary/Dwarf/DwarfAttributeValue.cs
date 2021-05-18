// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// Enumeration that represents attribute value type.
    /// </summary>
    public enum DwarfAttributeValueType
    {
        /// <summary>
        /// Attribute value is invalid.
        /// </summary>
        Invalid,

        /// <summary>
        /// Attribute value represents address.
        /// </summary>
        Address,

        /// <summary>
        /// Attribute value represents byte block.
        /// </summary>
        Block,

        /// <summary>
        /// Attribute value represents constant.
        /// </summary>
        Constant,

        /// <summary>
        /// Attribute value represents string.
        /// </summary>
        String,

        /// <summary>
        /// Attribute value represents flag.
        /// </summary>
        Flag,

        /// <summary>
        /// Attribute value represents reference.
        /// </summary>
        Reference,

        /// <summary>
        /// Attribute value represents resolved reference.
        /// </summary>
        ResolvedReference,

        /// <summary>
        /// Attribute value represents expression location.
        /// </summary>
        ExpressionLocation,

        /// <summary>
        /// Attribute value represents offset.
        /// </summary>
        SecOffset,
    }

    /// <summary>
    /// Structure representing attribute value.
    /// </summary>
    public class DwarfAttributeValue
    {
        /// <summary>
        /// Gets or sets the attribute value type.
        /// </summary>
        public DwarfAttributeValueType Type { get; set; }

        /// <summary>
        /// Gets or sets the value object.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets the address if type is <see cref="DwarfAttributeValueType.Address"/>.
        /// </summary>
        public ulong Address
        {
            get
            {
                return (ulong)Value;
            }
        }

        /// <summary>
        /// Gets the block if type is <see cref="DwarfAttributeValueType.Block"/>.
        /// </summary>
        public byte[] Block
        {
            get
            {
                return (byte[])Value;
            }
        }

        /// <summary>
        /// Gets the constant if type is <see cref="DwarfAttributeValueType.Constant"/>.
        /// </summary>
        public ulong Constant
        {
            get
            {
                if (Value is byte[] bytes)
                {
                    if (bytes.Length == 1)
                    {
                        return bytes[0];
                    }
                    else if (bytes.Length == 2)
                    {
                        return BitConverter.ToUInt16(bytes, 0);
                    }
                    else if (bytes.Length == 4)
                    {
                        return BitConverter.ToUInt32(bytes, 0);
                    }
                    else if (bytes.Length == 8)
                    {
                        return BitConverter.ToUInt64(bytes, 0);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    return (ulong)Value;
                }
            }
        }

        /// <summary>
        /// Gets the string if type is <see cref="DwarfAttributeValueType.String"/>.
        /// </summary>
        public string String
        {
            get
            {
                return (string)Value;
            }
        }

        /// <summary>
        /// Gets the flag if type is <see cref="DwarfAttributeValueType.Flag"/>.
        /// </summary>
        public bool Flag
        {
            get
            {
                return (bool)Value;
            }
        }

        /// <summary>
        /// Gets the reference if type is <see cref="DwarfAttributeValueType.ResolvedReference"/>.
        /// </summary>
        public DwarfSymbol Reference
        {
            get
            {
                return (DwarfSymbol)Value;
            }
        }

        /// <summary>
        /// Gets the expression location if type is <see cref="DwarfAttributeValueType.ExpressionLocation"/>.
        /// </summary>
        public byte[] ExpressionLocation
        {
            get
            {
                return (byte[])Value;
            }
        }

        /// <summary>
        /// Gets the offset if type is <see cref="DwarfAttributeValueType.SecOffset"/>.
        /// </summary>
        public ulong SecOffset
        {
            get
            {
                return (ulong)Value;
            }
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"{Type}: {Value}";
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            return Type.GetHashCode() ^ Value.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified <see cref="object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            DwarfAttributeValue other = obj as DwarfAttributeValue;

            if (other != null)
            {
                return this == other;
            }

            return false;
        }

        /// <summary>
        /// Compares two values for equality.
        /// </summary>
        /// <param name="value1">The first value.</param>
        /// <param name="value2">The second value.</param>
        /// <returns><c>true</c> if values are equal.</returns>
        public static bool operator ==(DwarfAttributeValue value1, DwarfAttributeValue value2)
        {
            if (value1?.Type != value2?.Type)
            {
                return false;
            }

            switch (value1.Type)
            {
                case DwarfAttributeValueType.Address:
                case DwarfAttributeValueType.Constant:
                case DwarfAttributeValueType.Reference:
                case DwarfAttributeValueType.SecOffset:
                    return (ulong)value1.Value == (ulong)value2.Value;

                case DwarfAttributeValueType.Block:
                case DwarfAttributeValueType.ExpressionLocation:
                    return Enumerable.SequenceEqual((byte[])value1.Value, (byte[])value2.Value);

                case DwarfAttributeValueType.Flag:
                    return (bool)value1.Value == (bool)value2.Value;

                case DwarfAttributeValueType.String:
                    return value1.Value.ToString() == value2.Value.ToString();
            }

            return true;
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="value1">The first value.</param>
        /// <param name="value2">The second value.</param>
        /// <returns><c>true</c> if values are not equal.</returns>
        public static bool operator !=(DwarfAttributeValue value1, DwarfAttributeValue value2)
        {
            return !(value1 == value2);
        }
    }
}
