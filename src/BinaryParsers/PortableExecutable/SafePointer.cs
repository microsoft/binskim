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
            _array = byte_array;
            _index = 0;
            _stream = null;
        }

        public SafePointer(byte[] byte_array, int index)
        {
            _array = byte_array;
            _index = index;
            _stream = null;
        }

        public SafePointer(Stream stream)
        {
            _array = null;
            _stream = stream;
            _index = 0;
        }

        internal SafePointer(byte[] byte_array, Stream stream, int index)
        {
            _array = byte_array;
            _stream = stream;
            _index = index;
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
            return (_array != null)
                ? (_array.GetHashCode() << 16) + _index
                : _stream.GetHashCode();
        }

        // conversion
        public static implicit operator byte (SafePointer pp)
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

        public static explicit operator UInt32(SafePointer sp)
        {
            return (((uint)(byte)(sp + 3)) << 24) | (((uint)(byte)(sp + 2)) << 16) | (((uint)(byte)(sp + 1)) << 8) | ((uint)(byte)(sp));
        }

        public static explicit operator UInt16(SafePointer sp)
        {
            return (ushort)(((byte)(sp + 1) << 8) | (byte)(sp));
        }

        public static explicit operator UInt64(SafePointer sp)
        {
            return ((UInt64)(UInt32)(sp + 4) << 32) | ((UInt64)(UInt32)(sp));
        }

        public static explicit operator string (SafePointer sp)
        {
            if (sp._array != null)
            {
                int nullterm = sp._index;
                while (sp._array[nullterm] != 0) nullterm++;
                return System.Text.Encoding.ASCII.GetString(sp._array, sp._index, nullterm - sp._index);
            }

            if (sp._stream != null)
            {
                ArrayList alBytes = new ArrayList();
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
            if (spl._array != spr._array) throw new Exception("Incomparable pointers");
            if (spl._stream != spr._stream) throw new Exception("Incomparable pointers");
            return spl._index - spr._index;
        }

        public static bool operator <(SafePointer spl, SafePointer spr)
        {
            if (spl._array != spr._array) throw new Exception("Incomparable pointers");
            if (spl._stream != spr._stream) throw new Exception("Incomparable pointers");
            return (spl._index < spr._index);
        }

        public static bool operator >(SafePointer spl, SafePointer spr)
        {
            if (spl._array != spr._array) throw new Exception("Incomparable pointers");
            if (spl._stream != spr._stream) throw new Exception("Incomparable pointers");
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
            get { return _index; }
            set { _index = value; }
        }

        public override string ToString()
        {
            return _index.ToString("X");
        }

        public byte[] GetBytes(int len)
        {
            if (_array != null)
            {
                if (_index + len > _array.Length)
                    throw new ArgumentException("Out of bounds");
                byte[] ret = new byte[len];
                Array.Copy(_array, _index, ret, 0, len);
                return ret;
            }

            if (_stream != null)
            {
                if (_stream.Seek(_index, SeekOrigin.Begin) != _index)
                {
                    throw new InvalidOperationException("Seeking outside stream boundaries");
                }

                byte[] ret = new byte[len];

                if (_stream.Read(ret, 0, len) != len)
                {
                    throw new InvalidOperationException("Reading past stream boundaries");
                }

                return ret;
            }

            throw new InvalidOperationException("Neither _array nor _stream exist");
        }

        public Stream GetStream()
        {
            if (_array != null)
            {
                return new MemoryStream(_array, _index, _array.Length - _index);
            }

            return _stream;
        }

        public bool HasData(int cBytes)
        {
            if (_array != null)
            {
                return (_index <= _array.Length - cBytes);
            }

            if (_stream != null)
            {
                return (_index <= _stream.Length - cBytes);
            }

            return true;
        }

        private void TestPointerAndThrow()
        {
            if (!IsValid)
                throw new InvalidOperationException("Pointer is beyond the safe range.");
        }

        public bool IsValid
        {
            get
            {
                if ((_array != null) && (_index < _array.Length) && (_index >= 0)) return true;
                if ((_stream != null) && (_index >= 0) && (_index < _stream.Length)) return true;
                return false;
            }
        }

        public bool IsNull
        {
            get
            {
                if (((_array == null) || (_array.Length == 0)) && ((_stream == null) || (_stream.Length == 0))) return true;
                return false;
            }
        }

        public void Set(byte b)
        {
            if (_array != null)
            {
                _array[_index] = b;
            }
            else
            {
                throw new NotSupportedException("Writing to stream-based sources is not supported");
            }
        }
    }
}
