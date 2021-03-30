// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Dwarf
{
    public class Attribute
    {
        public DW_AT Name { get; }
        public DW_FORM Form { get; }
        public byte[] Value { get; }

        public Attribute(ulong name, ulong form)
        {
            Name = (DW_AT)name;
            Form = (DW_FORM)form;
        }

        public Attribute(DW_AT name, DW_FORM form, byte[] value)
        {
            Name = name;
            Form = form;
            Value = value;
        }
    }
}
