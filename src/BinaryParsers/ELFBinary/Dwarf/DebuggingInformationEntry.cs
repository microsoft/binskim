// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public class DebuggingInformationEntry
    {
        public int Id { get; }
        public ulong Code { get; }
        public DW_TAG Tag { get; }
        public DW_CHILDREN HasChildren { get; }
        public List<Attribute> AttributeList { get; }
        public List<DebuggingInformationEntry> Children { get; }

        public DebuggingInformationEntry(int id, ulong code, DW_TAG tag, DW_CHILDREN hasChildren)
        {
            Id = id;
            Code = code;
            Tag = tag;
            HasChildren = hasChildren;
            AttributeList = new List<Attribute>();
            Children = new List<DebuggingInformationEntry>();
        }

        // Parse DIE from .debug_info
        public static DebuggingInformationEntry Parse(List<byte> infoData, ref int index, List<Abbreviation> abbrevList, int cuId)
        {
            int id = index;
            ulong code = LEB128Helper.ReadUnsigned(infoData, ref index);
            if (code == 0)
            {
                return null;
            }

            Abbreviation abbrev = abbrevList.Find(a => a.Code == code);
            if (abbrev == null)
            {
                return null;
            }

            var die = new DebuggingInformationEntry(id, code, abbrev.Tag, abbrev.HasChildren);
            foreach (Attribute abbrevAttr in abbrev.AttributeList)
            {
                List<byte> value = Read.AttributeValue(infoData, ref index, abbrevAttr, cuId);
                var attr = new Attribute(abbrevAttr.Name, abbrevAttr.Form, value.ToArray());
                die.AddAttribute(attr);
            }

            return die;
        }

        public void AddAttribute(Attribute attribute)
        {
            AttributeList.Add(attribute);
        }

        public void AddDieList(List<DebuggingInformationEntry> dieList)
        {
            Children.AddRange(dieList);
        }

        public string GetName(List<byte> strData)
        {
            string output = null;
            Attribute attr = AttributeList.Find(a => a.Name == DW_AT.Name);

            if (attr != null)
            {
                switch (attr.Form)
                {
                    case DW_FORM.String:
                        output = Encoding.ASCII.GetString(attr.Value);
                        break;

                    case DW_FORM.Strp:
                        int strp = BitConverter.ToInt32(attr.Value, 0);
                        output = Read.StringPtr(strData, strp);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            return output;
        }

        public string GetName(List<byte> strData, Attribute attr)
        {
            string output = null;
            switch (attr.Form)
            {
                case DW_FORM.String:
                    output = Encoding.ASCII.GetString(attr.Value);
                    break;

                case DW_FORM.Strp:
                    int strp = BitConverter.ToInt32(attr.Value, 0);
                    output = Read.StringPtr(strData, strp);
                    break;

                default:
                    throw new NotImplementedException();
            }

            return output;
        }

        public int GetTypeId()
        {
            int output = 0;
            Attribute typeId = AttributeList.Find(a => a.Name == DW_AT.Type);

            if (typeId != null)
            {
                output = BitConverter.ToInt32(typeId.Value, 0);
            }

            return output;
        }
    }
}
