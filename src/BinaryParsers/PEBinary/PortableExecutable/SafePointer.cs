// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;

namespace Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable
{
    public struct SafePointer
    {
        internal byte[] _array;
        internal int _index;
        internal Stream _stream;

        // constructors
        public SafePointer(byte[] byte_array)
        {
            this._array = byte_array;
            this._index = 0;
            this._stream = null;
        }

        public SafePointer(byte[] byte_array, int index)
        {
            this._array = byte_array;
            this._index = index;
            this._stream = null;
        }

        public SafePointer(Stream stream)
        {
            this._array = null;
            this._stream = stream;
            this._index = 0;
        }

        internal SafePointer(byte[] byte_array, Stream stream, int index)
        {
            this._array = byte_array;
            this._stream = stream;
            this._index = index;
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
            return (this._array != null)
                ? (this._array.GetHashCode() << 16) + this._index
                : this._stream.GetHashCode();
        }

        // conversion
        public static implicit operator byte(SafePointer pp)
        {
            pp.TestPointerAndThrow();

            if (pp._array != null)
            {
                return pp._array[pp._index];
            }

            if (pp._stream != null)
            {
                pp._stream.Seek(pp._index, SeekOrigin.Begin);
                return (byte)pp._stream.ReadByte();
            }

            throw new InvalidOperationException("Neither _array nor _stream exist");
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
            if (sp._array != null)
            {
                int nullterm = sp._index;
                while (sp._array[nullterm] != 0)
                {
                    nullterm++;
                }

                return System.Text.Encoding.ASCII.GetString(sp._array, sp._index, nullterm - sp._index);
            }

            if (sp._stream != null)
            {
                var alBytes = new ArrayList();
                byte b = 0;

                if (sp._stream.Seek(sp._index, SeekOrigin.Begin) != sp._index)
                {
                    throw new InvalidOperationException("Seeking outside stream boundaries");
                }

                while ((b = (byte)sp._stream.ReadByte()) != 0)
                {
                    alBytes.Add(b);
                }

                byte[] ab = (byte[])alBytes.ToArray(typeof(byte));

                return System.Text.Encoding.ASCII.GetString(ab);
            }

            throw new InvalidOperationException("Neither _array nor _stream exist");
        }

        // operators
        public static SafePointer operator +(SafePointer pp, int n)
        {
            return new SafePointer(pp._array, pp._stream, pp._index + n);
        }

        public static SafePointer operator -(SafePointer pp, int n)
        {
            return new SafePointer(pp._array, pp._stream, pp._index - n);
        }

        public static SafePointer operator +(SafePointer pp, uint n)
        {
            return new SafePointer(pp._array, pp._stream, (int)(pp._index + n));
        }

        public static SafePointer operator -(SafePointer pp, uint n)
        {
            return new SafePointer(pp._array, pp._stream, (int)(pp._index - n));
        }

        public static int operator -(SafePointer spl, SafePointer spr)
        {
            if (spl._array != spr._array)
            {
                throw new Exception("Incomparable pointers");
            }

            if (spl._stream != spr._stream)
            {
                throw new Exception("Incomparable pointers");
            }

            return spl._index - spr._index;
        }

        public static bool operator <(SafePointer spl, SafePointer spr)
        {
            if (spl._array != spr._array)
            {
                throw new Exception("Incomparable pointers");
            }

            if (spl._stream != spr._stream)
            {
                throw new Exception("Incomparable pointers");
            }

            return (spl._index < spr._index);
        }

        public static bool operator >(SafePointer spl, SafePointer spr)
        {
            if (spl._array != spr._array)
            {
                throw new Exception("Incomparable pointers");
            }

            if (spl._stream != spr._stream)
            {
                throw new Exception("Incomparable pointers");
            }

            return (spl._index > spr._index);
        }

        public static bool operator ==(SafePointer sp1, SafePointer sp2)
        {
            return ((sp1._array == sp2._array) && (sp1._stream == sp2._stream) && (sp1._index == sp2._index));
        }

        public static bool operator !=(SafePointer sp1, SafePointer sp2)
        {
            return ((sp1._array != sp2._array) || (sp1._stream != sp2._stream) || (sp1._index != sp2._index));
        }

        public static SafePointer operator ++(SafePointer sp)
        {
            sp._index++;
            return sp;
        }

        public static SafePointer operator --(SafePointer sp)
        {
            sp._index--;
            return sp;
        }

        public int Address
        {
            get => this._index;
            set => this._index = value;
        }

        public override string ToString()
        {
            return this._index.ToString("X");
        }

        public byte[] GetBytes(int len)
        {
            if (this._array != null)
            {
                if (this._index + len > this._array.Length)
                {
                    throw new ArgumentException("Out of bounds");
                }

                byte[] ret = new byte[len];
                Array.Copy(this._array, this._index, ret, 0, len);
                return ret;
            }

            if (this._stream != null)
            {
                if (this._stream.Seek(this._index, SeekOrigin.Begin) != this._index)
                {
                    throw new InvalidOperationException("Seeking outside stream boundaries");
                }

                byte[] ret = new byte[len];

                if (this._stream.Read(ret, 0, len) != len)
                {
                    throw new InvalidOperationException("Reading past stream boundaries");
                }

                return ret;
            }

            throw new InvalidOperationException("Neither _array nor _stream exist");
        }

        public Stream GetStream()
        {
            if (this._array != null)
            {
                return new MemoryStream(this._array, this._index, this._array.Length - this._index);
            }

            return this._stream;
        }

        public bool HasData(int cBytes)
        {
            if (this._array != null)
            {
                return (this._index <= this._array.Length - cBytes);
            }

            if (this._stream != null)
            {
                return (this._index <= this._stream.Length - cBytes);
            }

            return true;
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
                if ((this._array != null) && (this._index < this._array.Length) && (this._index >= 0))
                {
                    return true;
                }

                if ((this._stream != null) && (this._index >= 0) && (this._index < this._stream.Length))
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
                if (((this._array == null) || (this._array.Length == 0)) && ((this._stream == null) || (this._stream.Length == 0)))
                {
                    return true;
                }

                return false;
            }
        }

        public void Set(byte b)
        {
            if (this._array != null)
            {
                this._array[this._index] = b;
            }
            else
            {
                throw new NotSupportedException("Writing to stream-based sources is not supported");
            }
        }
    }
}
