// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    internal static class Read
    {
        // Read attribute value from .debug_info
        public static List<byte> AttributeValue(List<byte> infoData, ref int index, Attribute attribute, int cuId)
        {
            var output = new List<byte>();
            switch (attribute.Form)
            {
                case DW_FORM.Addr:
                    output.AddRange(infoData.GetRange(index, 4));
                    index += 4;
                    break;

                case DW_FORM.Block2:
                {
                    int numBytes = BitConverter.ToInt32(infoData.GetRange(index, 2).ToArray(), 0);
                    index += 2;
                    output.AddRange(infoData.GetRange(index, numBytes));
                    index += numBytes;
                    break;
                }
                case DW_FORM.Block4:
                {
                    try
                    {
                        int numBytes = BitConverter.ToInt32(infoData.GetRange(index, 4).ToArray(), 0);
                        index += 4;
                        output.AddRange(infoData.GetRange(index, numBytes));
                        index += numBytes;
                    }
                    catch (Exception)
                    {
                        // TODO: understand why does this happen for dwarf5
                    }
                    break;
                }
                case DW_FORM.Data2:
                    output.AddRange(infoData.GetRange(index, 2));
                    index += 2;
                    break;

                case DW_FORM.Data4:
                    output.AddRange(infoData.GetRange(index, 4));
                    index += 4;
                    break;

                case DW_FORM.Data8:
                    output.AddRange(infoData.GetRange(index, 8));
                    index += 8;
                    break;

                case DW_FORM.String:
                {
                    var str = new List<byte>();
                    while (index < infoData.Count)
                    {
                        byte data = infoData[index];
                        index++;
                        if (data == 0)
                        {
                            break;
                        }

                        str.Add(data);
                    }
                    output = str;
                    break;
                }
                case DW_FORM.Block:
                {
                    int numBytes = (int)LEB128Helper.ReadUnsigned(infoData, ref index);
                    output.AddRange(infoData.GetRange(index, numBytes));
                    index += numBytes;
                    break;
                }
                case DW_FORM.Block1:
                {
                    int numBytes = (int)infoData[index];
                    index++;
                    output.AddRange(infoData.GetRange(index, numBytes));
                    index += numBytes;
                    break;
                }
                case DW_FORM.Data1:
                    output.AddRange(infoData.GetRange(index, 1));
                    index++;
                    break;

                case DW_FORM.Flag:
                    output.AddRange(infoData.GetRange(index, 1));
                    index++;
                    break;

                case DW_FORM.Sdata:
                    output = BitConverter.GetBytes(LEB128Helper.ReadSigned(infoData, ref index)).ToList<byte>();
                    break;

                case DW_FORM.Strp:
                    output.AddRange(infoData.GetRange(index, 4));
                    index += 4;
                    break;

                case DW_FORM.Udata:
                    output = BitConverter.GetBytes(LEB128Helper.ReadUnsigned(infoData, ref index)).ToList<byte>();
                    break;

                case DW_FORM.RefAddr:
                    output.AddRange(infoData.GetRange(index, 4));
                    index += 4;
                    break;

                case DW_FORM.Ref1:
                {
                    int reference = BitConverter.ToInt32(infoData.GetRange(index, 1).ToArray(), 0);
                    index++;
                    output = BitConverter.GetBytes(cuId + reference).ToList<byte>();
                    break;
                }
                case DW_FORM.Ref2:
                {
                    int reference = BitConverter.ToInt32(infoData.GetRange(index, 2).ToArray(), 0);
                    index += 2;
                    output = BitConverter.GetBytes(cuId + reference).ToList<byte>();
                    break;
                }
                case DW_FORM.Ref4:
                {
                    int reference = BitConverter.ToInt32(infoData.GetRange(index, 4).ToArray(), 0);
                    index += 4;
                    output = BitConverter.GetBytes(cuId + reference).ToList<byte>();
                    break;
                }
                case DW_FORM.Ref8:
                {
                    int reference = BitConverter.ToInt32(infoData.GetRange(index, 8).ToArray(), 0);
                    index += 8;
                    output = BitConverter.GetBytes(cuId + reference).ToList<byte>();
                    break;
                }
                case DW_FORM.RefUdata:
                {
                    int reference = (int)LEB128Helper.ReadUnsigned(infoData, ref index);
                    output = BitConverter.GetBytes(cuId + reference).ToList<byte>();
                    break;
                }
                case DW_FORM.Indirect:
                    throw new NotImplementedException("DW_FORM_indirect not yet implemented.");
                default:
                    break;
                    // TODO: review
                    //throw new NotImplementedException("Unknown DW_FORM.");
            }
            return output;
        }

        // Read string from .debug_str
        public static string StringPtr(List<byte> strData, int index)
        {
            var output = new List<byte>();
            byte character;
            while ((character = strData.ElementAt(index)) > 0)
            {
                output.Add(character);
                index++;
            }
            return Encoding.ASCII.GetString(output.ToArray());
        }
    }
}
