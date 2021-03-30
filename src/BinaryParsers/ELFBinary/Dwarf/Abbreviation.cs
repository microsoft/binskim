// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using ELFSharp.ELF;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public class Abbreviation
    {
        public int Offset { get; }
        public ulong Code { get; }
        public DW_TAG Tag { get; }
        public DW_CHILDREN HasChildren { get; }
        public List<Attribute> AttributeList { get; }

        public Abbreviation(int start, ulong code, DW_TAG tag, DW_CHILDREN hasChildren)
        {
            Offset = start;
            Code = code;
            Tag = tag;
            HasChildren = hasChildren;
            AttributeList = new List<Attribute>();
        }

        // Parse abbreviation from .debug_abbrev
        public static Abbreviation Parse(List<byte> abbrevData, ref int index, int startIndex)
        {
            ulong code = LEB128Helper.ReadUnsigned(abbrevData, ref index);
            if (code == 0)
            {
                return null;
            }

            ulong tag = LEB128Helper.ReadUnsigned(abbrevData, ref index);
            byte hasChildren = abbrevData[index];
            index++;
            var abbreviation = new Abbreviation(startIndex, code, (DW_TAG)tag, (DW_CHILDREN)hasChildren);

            while (index < abbrevData.Count)
            {
                ulong name = LEB128Helper.ReadUnsigned(abbrevData, ref index);
                ulong form = LEB128Helper.ReadUnsigned(abbrevData, ref index);
                if (name == 0 && form == 0)
                {
                    break;
                }

                abbreviation.AddAttribute(new Attribute(name, form));
            }

            return abbreviation;
        }

        public static List<Abbreviation> Extract(IELF elfFile)
        {
            var abbrevList = new List<Abbreviation>();
            ELFSharp.ELF.Sections.ISection section = elfFile.Sections.FirstOrDefault(s => s.Name == ".debug_abbrev");

            if (section == null)
            {
                return null;
            }

            var abbrevData = section.GetContents().ToList();

            int index = 0;
            while (index < abbrevData.Count)
            {
                int startIndex = index;
                Abbreviation abbrev;
                while ((abbrev = Parse(abbrevData, ref index, startIndex)) != null)
                {
                    abbrevList.Add(abbrev);
                }
            }
            return abbrevList;
        }

        public void AddAttribute(Attribute attribute)
        {
            AttributeList.Add(attribute);
        }
    }
}
