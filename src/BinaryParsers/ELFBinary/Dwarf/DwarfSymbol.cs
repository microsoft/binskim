// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// DWARF symbol instance.
    /// </summary>
    public class DwarfSymbol
    {
        /// <summary>
        /// Gets or sets the symbol tag.
        /// </summary>
        public DwarfTag Tag { get; internal set; }

        /// <summary>
        /// Gets or sets the attributes.
        /// </summary>
        public IReadOnlyDictionary<DwarfAttribute, DwarfAttributeValue> Attributes { get; internal set; }

        /// <summary>
        /// Gets or sets the children.
        /// </summary>
        public List<DwarfSymbol> Children { get; internal set; }

        /// <summary>
        /// Gets or sets the parent.
        /// </summary>
        public DwarfSymbol Parent { get; internal set; }

        /// <summary>
        /// Gets or sets the offset.
        /// </summary>
        internal int Offset { get; set; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name
        {
            get
            {

                if (Attributes.TryGetValue(DwarfAttribute.Name, out DwarfAttributeValue nameValue))
                {
                    return nameValue.String;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the full name.
        /// </summary>
        public string FullName
        {
            get
            {
                string name = Name;

                if (Parent != null && Parent.Tag != DwarfTag.CompileUnit && name != null)
                {
                    return Parent.FullName + "::" + name;
                }

                return name;
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
            return $"{Tag} (Offset = {Offset}, Attributes = {Attributes.Count}, Children = {Children?.Count}";
        }

        /// <summary>
        /// Gets the constant attribute value if available.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <param name="defaultValue">The default value if attribute is not available.</param>
        /// <returns>Attribute value if available; default value otherwise</returns>
        public ulong GetConstantAttribute(DwarfAttribute attribute, ulong defaultValue = 0)
        {

            if (Attributes.TryGetValue(attribute, out DwarfAttributeValue value))
            {
                return value.Constant;
            }

            return defaultValue;
        }
    }
}
