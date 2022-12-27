// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

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
        /// <param name="debugStrings">The debug string offsets.</param>
        /// <param name="addressNormalizer">Normalize address delegate (<see cref="NormalizeAddressDelegate"/>)</param>
        public DwarfCompilationUnit(IDwarfBinary dwarfBinary,
                                    DwarfMemoryReader debugData,
                                    DwarfMemoryReader debugDataDescription,
                                    DwarfMemoryReader debugStrings,
                                    DwarfMemoryReader debugLineStrings,
                                    IList<int> debugStringOffsets,
                                    NormalizeAddressDelegate addressNormalizer)
        {
            ReadData(dwarfBinary,
                     debugData,
                     debugDataDescription,
                     debugStrings,
                     debugLineStrings,
                     debugStringOffsets,
                     addressNormalizer);
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
        private void ReadData(IDwarfBinary dwarfBinary,
                              DwarfMemoryReader debugData,
                              DwarfMemoryReader debugDataDescription,
                              DwarfMemoryReader debugStrings,
                              DwarfMemoryReader debugLineStrings,
                              IList<int> debugStringOffsets,
                              NormalizeAddressDelegate addressNormalizer)
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
                ulong code = debugData.ULEB128();

                if (code == 0)
                {
                    if (parents.Count > 0)
                    {
                        parents.Pop();
                    }

                    continue;
                }

                DataDescription description = dataDescriptionReader.GetDebugDataDescription(code);
                var attributes = new Dictionary<DwarfAttribute, DwarfAttributeValue>();

                if (description.Attributes.Any(a => a.Attribute == DwarfAttribute.LinkageName && a.Format == DwarfFormat.Strp))
                {
                    description.Attributes.RemoveAll(a => a.Attribute == DwarfAttribute.Name);
                }

                int indexOffset = -1;

                foreach (DataDescriptionAttribute descriptionAttribute in description.Attributes)
                {
                    DwarfAttributeValue attributeValue = new DwarfAttributeValue();
                    DwarfAttribute attribute = descriptionAttribute.Attribute;
                    DwarfFormat format = descriptionAttribute.Format;

                    switch (format)
                    {
                        case DwarfFormat.Address:
                            attributeValue.Type = DwarfAttributeValueType.Address;
                            attributeValue.Value = debugData.ReadUlong(addressSize);
                            break;

                        case DwarfFormat.Block:
                            attributeValue.Type = DwarfAttributeValueType.Block;
                            attributeValue.Value = debugData.ReadBlock(debugData.ULEB128());
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
                            attributeValue.Value = debugData.ReadUlong();
                            break;

                        case DwarfFormat.Data16:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = debugData.ReadBlock(16);
                            break;

                        case DwarfFormat.SData:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = (ulong)debugData.SLEB128();
                            break;

                        case DwarfFormat.UData:
                            attributeValue.Type = DwarfAttributeValueType.Constant;
                            attributeValue.Value = (ulong)debugData.ULEB128();
                            break;

                        case DwarfFormat.String:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Value = debugData.ReadString();
                            break;

                        case DwarfFormat.LineStrp:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            int offsetStrp = debugData.ReadOffset(is64bit);
                            attributeValue.Value = debugLineStrings.ReadString(offsetStrp);
                            break;

                        case DwarfFormat.Strp:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            offsetStrp = debugData.ReadOffset(is64bit);
                            attributeValue.Value = debugStrings.ReadString(offsetStrp);
                            break;

                        case DwarfFormat.StrpSup:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            // NOTE: we don't support locating this value currently.
                            attributeValue.Offset = (ulong)debugData.ReadOffset(is64bit);
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
                            attributeValue.Value = debugData.ReadByte() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.Ref2:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = debugData.ReadUshort() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.Ref4:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = debugData.ReadUint() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.Ref8:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = debugData.ReadUlong() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.RefUData:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = debugData.ULEB128() + (ulong)beginPosition;
                            break;

                        case DwarfFormat.RefAddr:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Value = (ulong)debugData.ReadOffset(is64bit);
                            break;

                        case DwarfFormat.RefSig8:
                            attributeValue.Type = DwarfAttributeValueType.Invalid;
                            debugData.Position += 8;
                            break;

                        case DwarfFormat.RefSup4:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Offset = debugData.ReadUint();
                            // We don't resolve this reference from supplemental data yet.
                            break;

                        case DwarfFormat.RefSup8:
                            attributeValue.Type = DwarfAttributeValueType.Reference;
                            attributeValue.Offset = debugData.ReadUlong();
                            // We don't resolve this reference from supplemental data yet.
                            break;

                        case DwarfFormat.ExpressionLocation:
                            attributeValue.Type = DwarfAttributeValueType.ExpressionLocation;
                            attributeValue.Value = debugData.ReadBlock(debugData.ULEB128());
                            break;

                        case DwarfFormat.SecOffset:
                            attributeValue.Type = DwarfAttributeValueType.SecOffset;
                            attributeValue.Value = (ulong)debugData.ReadOffset(is64bit);
                            if (attribute == DwarfAttribute.RankStrOffsetsBase)
                            {
                                indexOffset = (int)((ulong)attributeValue.Value / (ulong)(is64bit ? 8 : 4));
                            }
                            break;

                        case DwarfFormat.ImplicitConst:
                            attributeValue.Value = descriptionAttribute.Value;
                            break;

                        case DwarfFormat.Strx:
                        case DwarfFormat.GNUStrIndex:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Offset = debugData.ULEB128();
                            break;

                        case DwarfFormat.Strx1:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Offset = debugData.ReadByte();
                            break;

                        case DwarfFormat.Strx2:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Offset = debugData.ReadUshort();
                            break;

                        case DwarfFormat.Strx3:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Offset = debugData.ReadThreeBytes();
                            break;

                        case DwarfFormat.Strx4:
                            attributeValue.Type = DwarfAttributeValueType.String;
                            attributeValue.Offset = debugData.ReadUint();
                            break;

                        case DwarfFormat.Addrx:
                            attributeValue.Type = DwarfAttributeValueType.Address;
                            attributeValue.Value = debugData.ULEB128() + (ulong)beginPosition;
                            break;

                        // NOTE: we don't resolve any of these new DWARF5 address values yet.
                        case DwarfFormat.Addrx1:
                            attributeValue.Type = DwarfAttributeValueType.Address;
                            attributeValue.Offset = debugData.ReadByte();
                            break;

                        case DwarfFormat.Addrx2:
                            attributeValue.Type = DwarfAttributeValueType.Address;
                            attributeValue.Offset = debugData.ReadUshort();
                            break;

                        case DwarfFormat.Addrx3:
                            attributeValue.Type = DwarfAttributeValueType.Address;
                            attributeValue.Offset = debugData.ReadThreeBytes();
                            break;

                        case DwarfFormat.Addrx4:
                            attributeValue.Type = DwarfAttributeValueType.Address;
                            attributeValue.Offset = debugData.ReadUlong();
                            break;

                        case DwarfFormat.Rnglistx:
                            attributeValue.Type = DwarfAttributeValueType.SecOffset;
                            attributeValue.Value = debugData.ULEB128();
                            break;

                        case DwarfFormat.GNUAddrIndex:
                            break;

                        default:
                            throw new InvalidOperationException($"{attribute} : {format}");
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

                if (indexOffset > -1)
                {
                    foreach (DwarfAttributeValue dwarfAttributeValue in attributes.Values)
                    {
                        if (dwarfAttributeValue.Offset == null ||
                            dwarfAttributeValue.Type != DwarfAttributeValueType.String)
                        {
                            // We current do not post-process all address types.
                            continue;
                        }

                        int debugStringOffsetIndex = Convert.ToInt32(dwarfAttributeValue.Value) + indexOffset;
                        int offset = debugStringOffsets[debugStringOffsetIndex];
                        dwarfAttributeValue.Value = debugStrings.ReadString(offset);
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

        private List<int> BuildStringOffsets(DwarfMemoryReader reader, bool is64bit)
        {
            var stringOffsets = new List<int>();

            while (!reader.IsEnd)
            {
                int offset = reader.ReadOffset(is64bit);
                stringOffsets.Add(offset);
            }

            return stringOffsets;
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

            /// <summary>
            /// Gets or sets the attribute value, if specified in the abbreviations table.
            /// </summary>
            public object Value { get; set; }
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
            private readonly Dictionary<ulong, DataDescription> readDescriptions;

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
                readDescriptions = new Dictionary<ulong, DataDescription>();
                lastReadPosition = startingPosition;
                this.debugDataDescription = debugDataDescription;
            }

            /// <summary>
            /// Gets the debug data description for the specified code.
            /// </summary>
            /// <param name="findCode">The code to be found.</param>
            public DataDescription GetDebugDataDescription(ulong findCode)
            {
                // See section 7.5.3 Abbreviations Tables of DWARF5
                // spec for information on this parsing implementation.

                if (readDescriptions.TryGetValue(findCode, out DataDescription result))
                {
                    return result;
                }

                debugDataDescription.Position = lastReadPosition;
                while (!debugDataDescription.IsEnd)
                {
                    ulong code = debugDataDescription.ULEB128();

                    if (debugDataDescription.IsEnd)
                    {
                        return result;
                    }

                    DwarfTag tag = (DwarfTag)debugDataDescription.ULEB128();
                    bool hasChildren = debugDataDescription.ReadByte() != 0;
                    List<DataDescriptionAttribute> attributes = new List<DataDescriptionAttribute>();

                    while (!debugDataDescription.IsEnd)
                    {
                        DwarfAttribute attribute = (DwarfAttribute)debugDataDescription.ULEB128();
                        DwarfFormat format = (DwarfFormat)debugDataDescription.ULEB128();
                        object value = null;

                        while (format == DwarfFormat.Indirect)
                        {
                            format = (DwarfFormat)debugDataDescription.ULEB128();
                        }

                        if (attribute == DwarfAttribute.None && format == DwarfFormat.None)
                        {
                            break;
                        }

                        if (format == DwarfFormat.ImplicitConst)
                        {
                            value = debugDataDescription.ULEB128();
                        }

                        attributes.Add(new DataDescriptionAttribute()
                        {
                            Attribute = attribute,
                            Format = format,
                            Value = value
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
