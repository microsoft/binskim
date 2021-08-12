// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    /// <summary>
    /// DWARF compilation unit instance.
    /// </summary>
    public class DwarfCompilationUnit
    {
        /// <summary>
        /// The dictionary of symbols located by offset in the debug data stream.
        /// </summary>
        private readonly Dictionary<int, DwarfSymbol> symbolsByOffset = new Dictionary<int, DwarfSymbol>();

        /// <summary>
        /// The offset of next Compilation Unit
        /// </summary>
        public int NextOffset { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DwarfCompilationUnit"/> class.
        /// </summary>
        /// <param name="debugData">The debug data stream.</param>
        /// <param name="debugDataDescription">The debug data description stream.</param>
        /// <param name="debugStrings">The debug strings.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        public DwarfCompilationUnit(IDwarfBinary dwarfBinary, DwarfMemoryReader debugData, DwarfMemoryReader debugDataDescription, DwarfMemoryReader debugStrings, NormalizeAddressDelegate addressNormalizer)
        {
            ReadData(dwarfBinary, debugData, debugDataDescription, debugStrings, addressNormalizer);
        }

        /// <summary>
        /// Gets the symbols tree of all top level symbols defined in this compilation unit.
        /// </summary>
        public DwarfSymbol[] SymbolsTree { get; private set; }

        /// <summary>
        /// Gets all symbols defined in this compilation unit.
        /// </summary>
        public IEnumerable<DwarfSymbol> Symbols
        {
            get
            {
                return symbolsByOffset.Values;
            }
        }

        /// <summary>
        /// Reads the data for this instance.
        /// </summary>
        /// <param name="debugData">The debug data.</param>
        /// <param name="debugDataDescription">The debug data description.</param>
        /// <param name="debugStrings">The debug strings.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        private void ReadData(IDwarfBinary dwarfBinary, DwarfMemoryReader debugData, DwarfMemoryReader debugDataDescription, DwarfMemoryReader debugStrings, NormalizeAddressDelegate addressNormalizer)
        {
            // Read header
            int beginPosition = debugData.Position;
            ulong length = debugData.ReadLength(out bool is64bit);
            int endPosition = debugData.Position + (int)length;
            NextOffset = endPosition;
            ushort version = debugData.ReadUshort();

            byte addressSize;
            int debugDataDescriptionOffset;

            dwarfBinary.DwarfVersion = version;

            if (version == 5)
            {
                dwarfBinary.DwarfUnitType = (DwarfUnitType)(debugData.ReadByte());
                addressSize = debugData.ReadByte();
                debugDataDescriptionOffset = debugData.ReadOffset(is64bit);
                if (dwarfBinary.DwarfUnitType == DwarfUnitType.Skeleton || dwarfBinary.DwarfUnitType == DwarfUnitType.SplitCompile)
                {
                    debugData.ReadUlong();
                }
            }
            else if (version > 0 && version < 5)
            {
                debugDataDescriptionOffset = debugData.ReadOffset(is64bit);
                addressSize = debugData.ReadByte();
            }
            else
            {
                return;
            }

            DataDescriptionReader dataDescriptionReader = new DataDescriptionReader(debugDataDescription, debugDataDescriptionOffset);

            // Read data
            List<DwarfSymbol> symbols = new List<DwarfSymbol>();
            Stack<DwarfSymbol> parents = new Stack<DwarfSymbol>();

            while (debugData.Position < endPosition)
            {
                int dataPosition = debugData.Position;
                uint code = debugData.LEB128();

                if (code == 0)
                {
                    if (parents.Count > 0)
                    {
                        parents.Pop();
                    }

                    continue;
                }

                DataDescription description = dataDescriptionReader.GetDebugDataDescription(code);
                Dictionary<DwarfAttribute, DwarfAttributeValue> attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>();

                if (description.Attributes.Any(a => a.Attribute == DwarfAttribute.LinkageName && a.Format == DwarfFormat.Strp))
                {
                    description.Attributes.RemoveAll(a => a.Attribute == DwarfAttribute.Name);
                }

                foreach (DataDescriptionAttribute descriptionAttribute in description.Attributes)
                {
                    DwarfAttribute attribute = descriptionAttribute.Attribute;
                    DwarfFormat format = descriptionAttribute.Format;
                    DwarfAttributeValue attributeValue = new DwarfAttributeValue();

                    switch (format)
                    {
                        case DwarfFormat.Address:
                            attributeValue.Type = DwarfAttributeValueType.Address;
                            attributeValue.Value = debugData.ReadUlong(addressSize);
                            break;

                        case DwarfFormat.Block:
                            attributeValue.Type = DwarfAttributeValueType.Block;
                            attributeValue.Value = debugData.ReadBlock(debugData.LEB128());
                            break;

                        case DwarfFormat.Block1:
                            attributeValue.Type = DwarfAttributeValueType.Block;
                            attributeValue.Value = debugData.ReadBlock(debugData.ReadByte());
                            break;

                        case DwarfFormat.Block2:
                            attributeValue.Type = DwarfAttributeValueType.Block;
                            attributeValue.Value = debugData.ReadBlock(debugData.ReadUshort());
                            break;

                        case DwarfFormat.Block4:
                            attributeValue.Type = DwarfAttributeValueType.Block;
                            attributeValue.Value = debugData.ReadBlock(debugData.ReadUint());
                            break;

                        case DwarfFormat.Data1:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = (ulong)debugData.ReadByte();
                            break;

                        case DwarfFormat.Data2:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = (ulong)debugData.ReadUshort();
                            break;

                        case DwarfFormat.Data4:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = (ulong)debugData.ReadUint();
                            break;

                        case DwarfFormat.Data8:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = (ulong)debugData.ReadUlong();
                            break;

                        case DwarfFormat.SData:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = (ulong)debugData.SLEB128();
                            break;

                        case DwarfFormat.UData:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = (ulong)debugData.LEB128();
                            break;

                        case DwarfFormat.String:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Value = debugData.ReadString();
                            break;

                        case DwarfFormat.Strp:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            int offsetStrp = debugData.ReadOffset(is64bit);
                            attributeValue.Value = debugStrings.ReadString(offsetStrp);
                            break;

                        case DwarfFormat.Flag:
                            attributeValue.Type = DwarfAttributeValueType.Flag;
                            attributeValue.Value = debugData.ReadByte() != 0;
                            break;

                        case DwarfFormat.FlagPresent:
                            attributeValue.Type = DwarfAttributeValueType.Flag;
                            attributeValue.Value = true;
                            break;

                        case DwarfFormat.Ref1:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = (ulong)debugData.ReadByte() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.Ref2:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = (ulong)debugData.ReadUshort() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.Ref4:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = (ulong)debugData.ReadUint() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.Ref8:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = (ulong)debugData.ReadUlong() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.RefUData:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = (ulong)debugData.LEB128() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.RefAddr:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = (ulong)debugData.ReadOffset(is64bit);
                            break;

                        case DwarfFormat.RefSig8:
                            attributeValue.Type = DwarfAttributeValueType.Invalid;
                            debugData.Position += 8;
                            break;

                        case DwarfFormat.ExpressionLocation:
                            attributeValue.Type = DwarfAttributeValueType.ExpressionLocation;
                            attributeValue.Value = debugData.ReadBlock(debugData.LEB128());
                            break;

                        case DwarfFormat.SecOffset:
                            attributeValue.Type = DwarfAttributeValueType.SecOffset;
                            attributeValue.Value = (ulong)debugData.ReadOffset(is64bit);
                            break;

                        case DwarfFormat.ImplicitConst:
                            break;

                        case DwarfFormat.Strx:
                        case DwarfFormat.GNUStrIndex:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Value = (ulong)debugData.LEB128() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.Addrx:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Value = (ulong)debugData.LEB128() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.Indirect:
                            break;

                        case DwarfFormat.GNUAddrIndex:
                            break;

                        default:
                            break;
                    }

                    if (attributes.ContainsKey(attribute))
                    {
                        if (attributes[attribute] != attributeValue)
                        {
                            attributes[attribute] = attributeValue;
                        }
                    }
                    else
                    {
                        attributes.Add(attribute, attributeValue);
                    }
                }

                DwarfSymbol symbol = new DwarfSymbol()
                {
                    Tag = description.Tag,
                    Attributes = attributes,
                    Offset = dataPosition,
                };

                symbolsByOffset.Add(symbol.Offset, symbol);

                if (parents.Count > 0)
                {
                    parents.Peek().Children.Add(symbol);
                    symbol.Parent = parents.Peek();
                }
                else
                {
                    symbols.Add(symbol);
                }

                if (description.HasChildren)
                {
                    symbol.Children = new List<DwarfSymbol>();
                    parents.Push(symbol);
                }

                break;
            }

            SymbolsTree = symbols.ToArray();

            if (SymbolsTree.Length > 0)
            {
                // Add void type symbol
                DwarfSymbol voidSymbol = new DwarfSymbol()
                {
                    Tag = DwarfTag.BaseType,
                    Offset = -1,
                    Parent = SymbolsTree[0],
                    Attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>()
                    {
                        { DwarfAttribute.Name, new DwarfAttributeValue() { Type = DwarfAttributeValueType.String, Value = "void" } },
                        { DwarfAttribute.ByteSize, new DwarfAttributeValue() { Type = DwarfAttributeValueType.Constant, Value = (ulong)0 } },
                    },
                };
                if (SymbolsTree[0].Children == null)
                {
                    SymbolsTree[0].Children = new List<DwarfSymbol>();
                }
                SymbolsTree[0].Children.Insert(0, voidSymbol);
                symbolsByOffset.Add(voidSymbol.Offset, voidSymbol);

                // Post process all symbols
                foreach (DwarfSymbol symbol in Symbols)
                {
                    Dictionary<DwarfAttribute, DwarfAttributeValue> attributes = symbol.Attributes as Dictionary<DwarfAttribute, DwarfAttributeValue>;

                    if (attributes != null)
                    {
                        foreach (DwarfAttributeValue value in attributes.Values)
                        {
                            if (value.Type == DwarfAttributeValueType.Reference)
                            {
                                if (symbolsByOffset.TryGetValue((int)value.Address, out DwarfSymbol reference))
                                {
                                    value.Type = DwarfAttributeValueType.ResolvedReference;
                                    value.Value = reference;
                                }
                            }
                            else if (value.Type == DwarfAttributeValueType.Address)
                            {
                                value.Value = addressNormalizer(value.Address);
                            }
                        }

                        if ((symbol.Tag == DwarfTag.PointerType && !attributes.ContainsKey(DwarfAttribute.Type))
                            || (symbol.Tag == DwarfTag.Typedef && !attributes.ContainsKey(DwarfAttribute.Type)))
                        {
                            attributes.Add(DwarfAttribute.Type, new DwarfAttributeValue()
                            {
                                Type = DwarfAttributeValueType.ResolvedReference,
                                Value = voidSymbol,
                            });
                        }
                    }
                }

                // Merge specifications
                foreach (DwarfSymbol symbol in Symbols)
                {
                    Dictionary<DwarfAttribute, DwarfAttributeValue> attributes = symbol.Attributes as Dictionary<DwarfAttribute, DwarfAttributeValue>;

                    if (attributes != null)
                    {
                        if (attributes.TryGetValue(DwarfAttribute.Specification, out DwarfAttributeValue specificationValue) && specificationValue.Type == DwarfAttributeValueType.ResolvedReference)
                        {
                            DwarfSymbol reference = specificationValue.Reference;
                            Dictionary<DwarfAttribute, DwarfAttributeValue> referenceAttributes = reference.Attributes as Dictionary<DwarfAttribute, DwarfAttributeValue>;

                            if (referenceAttributes != null)
                            {
                                foreach (KeyValuePair<DwarfAttribute, DwarfAttributeValue> kvp in attributes)
                                {
                                    if (kvp.Key != DwarfAttribute.Specification)
                                    {
                                        referenceAttributes[kvp.Key] = kvp.Value;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Symbol data description
        /// </summary>
        private struct DataDescription
        {
            /// <summary>
            /// Gets or sets the symbol tag.
            /// </summary>
            public DwarfTag Tag { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether symbol has children.
            /// </summary>
            /// <value>
            ///   <c>true</c> if symbol has children; otherwise, <c>false</c>.
            /// </value>
            public bool HasChildren { get; set; }

            /// <summary>
            /// Gets or sets the symbol data description attributes list.
            /// </summary>
            public List<DataDescriptionAttribute> Attributes { get; set; }
        }

        /// <summary>
        /// Symbol data description attribute.
        /// </summary>
        private struct DataDescriptionAttribute
        {
            /// <summary>
            /// Gets or sets the attribute.
            /// </summary>
            public DwarfAttribute Attribute { get; set; }

            /// <summary>
            /// Gets or sets the format.
            /// </summary>
            public DwarfFormat Format { get; set; }
        }

        /// <summary>
        /// Data description reader helper
        /// </summary>
        private class DataDescriptionReader
        {
            /// <summary>
            /// The debug data description stream
            /// </summary>
            private readonly DwarfMemoryReader debugDataDescription;

            /// <summary>
            /// The dictionary of already read symbol data descriptions located by code.
            /// </summary>
            private readonly Dictionary<uint, DataDescription> readDescriptions;

            /// <summary>
            /// The last read position.
            /// </summary>
            private int lastReadPosition;

            /// <summary>
            /// Initializes a new instance of the <see cref="DataDescriptionReader"/> class.
            /// </summary>
            /// <param name="debugDataDescription">The debug data description.</param>
            /// <param name="startingPosition">The starting position.</param>
            public DataDescriptionReader(DwarfMemoryReader debugDataDescription, int startingPosition)
            {
                readDescriptions = new Dictionary<uint, DataDescription>();
                lastReadPosition = startingPosition;
                this.debugDataDescription = debugDataDescription;
            }

            /// <summary>
            /// Gets the debug data description for the specified code.
            /// </summary>
            /// <param name="findCode">The code to be found.</param>
            public DataDescription GetDebugDataDescription(uint findCode)
            {
                if (readDescriptions.TryGetValue(findCode, out DataDescription result))
                {
                    return result;
                }

                debugDataDescription.Position = lastReadPosition;
                while (!debugDataDescription.IsEnd)
                {
                    uint code = debugDataDescription.LEB128();

                    if (debugDataDescription.IsEnd)
                    {
                        return result;
                    }

                    DwarfTag tag = (DwarfTag)debugDataDescription.LEB128();
                    bool hasChildren = debugDataDescription.ReadByte() != 0;
                    List<DataDescriptionAttribute> attributes = new List<DataDescriptionAttribute>();

                    while (!debugDataDescription.IsEnd)
                    {
                        DwarfAttribute attribute = (DwarfAttribute)debugDataDescription.LEB128();
                        DwarfFormat format = (DwarfFormat)debugDataDescription.LEB128();

                        while (format == DwarfFormat.Indirect)
                        {
                            format = (DwarfFormat)debugDataDescription.LEB128();
                        }

                        if (attribute == DwarfAttribute.None && format == DwarfFormat.None)
                        {
                            break;
                        }

                        attributes.Add(new DataDescriptionAttribute()
                        {
                            Attribute = attribute,
                            Format = format,
                        });
                    }

                    result = new DataDescription()
                    {
                        Tag = tag,
                        HasChildren = hasChildren,
                        Attributes = attributes,
                    };

                    if (!readDescriptions.ContainsKey(code))
                    {
                        readDescriptions.Add(code, result);
                    }

                    if (code == findCode)
                    {
                        lastReadPosition = debugDataDescription.Position;
                        return result;
                    }
                }

                throw new NotImplementedException();
            }
        }
    }
}
