// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.PortableExecutable;

namespace Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable
{
    public struct ImageFieldData
    {
        public ImageFieldData(int offset, string name, Type type, int count)
        {
            this.Offset = offset;
            this.Name = name;
            this.Type = type;
            this.Count = count;
            this.Header = null;
            this.PadTo = 0;
            this.VarLen = false;
            this.TrailingByte = 0;
            this.ParentHeader = null;
            this.Is32BitOnly = false;
        }

        public ImageFieldData(int offset, string name, Type type, int count, ImageHeader h) : this(offset, name, type, count)
        {
            this.Header = h;
            this.ParentHeader = h.ParentHeader;
        }

        public ImageFieldData(int offset, string name, Type type, int count, bool b32BitOnly) : this(offset, name, type, count)
        {
            this.Is32BitOnly = b32BitOnly;
        }

        public int Offset;
        public string Name;
        public Type Type;
        public int Count;
        public ImageHeader Header;
        public int PadTo;
        public bool VarLen;
        public byte TrailingByte;
        public PEHeader ParentHeader;
        public bool Is32BitOnly;
    }
}
