// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;

namespace Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable
{
    public struct SafePointer
    {
        internal byte[] array;
        internal int index;
        internal Stream stream;

        // constructors
        public SafePointer(byte[] bytearray)
        {
            this.array = bytearray;
            this.index = 0;
            this.stream = null;
        }

        public SafePointer(byte[] bytearray, int index)
        {
            this.array = bytearray;
            this.index = index;
            this.stream = null;
        }

        public SafePointer(Stream stream)
        {
            this.array = null;
            this.stream = stream;
            this.index = 0;
        }

        internal SafePointer(byte[] bytearray, Stream stream, int index)
        {
            this.array = bytearray;
            this.stream = stream;
            this.index = index;
        }

        // required overrides
        public override bool Equals(object o)
        {
            if (!(o is SafePointer))
            {
                return false;
            }

            return ((SafePointer)o == this);
        }

        public override int GetHashCode()
        {
            return (this.array != null)
                ? (this.array.GetHashCode() << 16) + this.index
                : this.stream.GetHashCode();
        }

        // conversion
        public static implicit operator byte(SafePointer pp)
        {
            pp.TestPointerAndThrow();

            if (pp.array != null)
            {
                return pp.array[pp.index];
            }

            if (pp.stream != null)
            {
                pp.stream.Seek(pp.index, SeekOrigin.Begin);
                return (byte)pp.stream.ReadByte();
            }

            throw new InvalidOperationException("Neither array nor stream exist");
        }

        public static explicit operator uint(SafePointer sp)
        {
            return (((uint)(byte)(sp + 3)) << 24) | (((uint)(byte)(sp + 2)) << 16) | (((uint)(byte)(sp + 1)) << 8) | ((uint)(byte)(sp));
        }

        public static explicit operator ushort(SafePointer sp)
        {
            return (ushort)(((byte)(sp + 1) << 8) | (byte)(sp));
        }

        public static explicit operator ulong(SafePointer sp)
        {
            return ((ulong)(uint)(sp + 4) << 32) | ((ulong)(uint)(sp));
        }

        public static explicit operator string(SafePointer sp)
        {
            if (sp.array != null)
            {
                int nullterm = sp.index;
                while (sp.array[nullterm] != 0)
                {
                    nullterm++;
                }

                return System.Text.Encoding.ASCII.GetString(sp.array, sp.index, nullterm - sp.index);
            }

            if (sp.stream != null)
            {
                var alBytes = new ArrayList();
                byte b = 0;

                if (sp.stream.Seek(sp.index, SeekOrigin.Begin) != sp.index)
                {
                    throw new InvalidOperationException("Seeking outside stream boundaries");
                }

                while ((b = (byte)sp.stream.ReadByte()) != 0)
                {
                    alBytes.Add(b);
                }

                byte[] ab = (byte[])alBytes.ToArray(typeof(byte));

                return System.Text.Encoding.ASCII.GetString(ab);
            }

            throw new InvalidOperationException("Neither array nor stream exist");
        }

        // operators
        public static SafePointer operator +(SafePointer pp, int n)
        {
            return new SafePointer(pp.array, pp.stream, pp.index + n);
        }

        public static SafePointer operator -(SafePointer pp, int n)
        {
            return new SafePointer(pp.array, pp.stream, pp.index - n);
        }

        public static SafePointer operator +(SafePointer pp, uint n)
        {
            return new SafePointer(pp.array, pp.stream, (int)(pp.index + n));
        }

        public static SafePointer operator -(SafePointer pp, uint n)
        {
            return new SafePointer(pp.array, pp.stream, (int)(pp.index - n));
        }

        public static int operator -(SafePointer spl, SafePointer spr)
        {
            if (spl.array != spr.array)
            {
                throw new Exception("Incomparable pointers");
            }

            if (spl.stream != spr.stream)
            {
                throw new Exception("Incomparable pointers");
            }

            return spl.index - spr.index;
        }

        public static bool operator <(SafePointer spl, SafePointer spr)
        {
            if (spl.array != spr.array)
            {
                throw new Exception("Incomparable pointers");
            }

            if (spl.stream != spr.stream)
            {
                throw new Exception("Incomparable pointers");
            }

            return (spl.index < spr.index);
        }

        public static bool operator >(SafePointer spl, SafePointer spr)
        {
            if (spl.array != spr.array)
            {
                throw new Exception("Incomparable pointers");
            }

            if (spl.stream != spr.stream)
            {
                throw new Exception("Incomparable pointers");
            }

            return (spl.index > spr.index);
        }

        public static bool operator ==(SafePointer sp1, SafePointer sp2)
        {
            return ((sp1.array == sp2.array) && (sp1.stream == sp2.stream) && (sp1.index == sp2.index));
        }

        public static bool operator !=(SafePointer sp1, SafePointer sp2)
        {
            return ((sp1.array != sp2.array) || (sp1.stream != sp2.stream) || (sp1.index != sp2.index));
        }

        public static SafePointer operator ++(SafePointer sp)
        {
            sp.index++;
            return sp;
        }

        public static SafePointer operator --(SafePointer sp)
        {
            sp.index--;
            return sp;
        }

        public int Address
        {
            get => this.index;
            set => this.index = value;
        }

        public override string ToString()
        {
            return this.index.ToString("X");
        }

        public byte[] GetBytes(int len)
        {
            if (this.array != null)
            {
                if (this.index + len > this.array.Length)
                {
                    throw new ArgumentException("Out of bounds");
                }

                byte[] ret = new byte[len];
                Array.Copy(this.array, this.index, ret, 0, len);
                return ret;
            }

            if (this.stream != null)
            {
                if (this.stream.Seek(this.index, SeekOrigin.Begin) != this.index)
                {
                    throw new InvalidOperationException("Seeking outside stream boundaries");
                }

                byte[] ret = new byte[len];

                if (this.stream.Read(ret, 0, len) != len)
                {
                    throw new InvalidOperationException("Reading past stream boundaries");
                }

                return ret;
            }

            throw new InvalidOperationException("Neither array nor stream exist");
        }

        public Stream GetStream()
        {
            return this.array != null
                ? new MemoryStream(this.array, this.index, this.array.Length - this.index)
                : this.stream;
        }

        private void TestPointerAndThrow()
        {
            if (!this.IsValid)
            {
                throw new InvalidOperationException("Pointer is beyond the safe range.");
            }
        }

        public bool IsValid
        {
            get
            {
                if ((this.array != null) && (this.index < this.array.Length) && (this.index >= 0))
                {
                    return true;
                }

                if ((this.stream != null) && (this.index >= 0) && (this.index < this.stream.Length))
                {
                    return true;
                }

                return false;
            }
        }

        public bool IsNull
        {
            get
            {
                return
                    ((this.array == null) || (this.array.Length == 0)) &&
                    ((this.stream == null) || (this.stream.Length == 0));
            }
        }

        public void Set(byte b)
        {
            if (this.array != null)
            {
                this.array[this.index] = b;
            }
            else
            {
                throw new NotSupportedException("Writing to stream-based sources is not supported");
            }
        }
    }
}
