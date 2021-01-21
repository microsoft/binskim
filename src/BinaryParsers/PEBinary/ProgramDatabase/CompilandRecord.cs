// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    /// <summary>A record of identifying characteristics of an object file.</summary>
    public struct CompilandRecord : IEquatable<CompilandRecord>, IComparable<CompilandRecord>
    {
        /// <summary>The object file name, e.g. hello.o, or null if only the library name is known.</summary>
        public readonly string Object;
        /// <summary>The library the object came from, or null if it was supplied directly; e.g. libcmt.lib.</summary>
        public readonly string Library;
        /// <summary>A suffix attached to this record which is usually rule-dependant; e.g. "disabled warnings" or "effective warning level" or similar.</summary>
        public readonly string Suffix;

        /// <summary>Initializes a new instance of the <see cref="CompilandRecord"/> struct.</summary>
        /// <param name="obj">The object file name.</param>
        /// <param name="lib">The library file name.</param>
        public CompilandRecord(string obj, string lib)
            : this(obj, lib, null)
        { }

        /// <summary>Initializes a new instance of the <see cref="CompilandRecord"/> struct.</summary>
        /// <param name="obj">The object file name.</param>
        /// <param name="lib">The library file name.</param>
        /// <param name="suffix">A suffix attached to this record which is usually rule-dependant; e.g.
        /// "disabled warnings" or "effective warning level" or similar.</param>
        public CompilandRecord(string obj, string lib, string suffix)
        {
            this.Object = obj;
            this.Library = lib;
            this.Suffix = suffix;
        }

        /// <summary>Initializes a new instance of the <see cref="CompilandRecord"/> struct, sanitizing
        /// the inputs.</summary>
        /// <param name="obj">The object file name.</param>
        /// <param name="lib">The library file name.</param>
        /// <returns>The new <see cref="CompilandRecord"/> with file path parameters sanitized.</returns>
        public static CompilandRecord CreateSanitized(string obj, string lib)
        {
            return CreateSanitized(obj, lib, null);
        }

        /// <summary>Initializes a new instance of the <see cref="CompilandRecord"/> struct, sanitizing
        /// the inputs.</summary>
        /// <param name="obj">The object file name.</param>
        /// <param name="lib">The library file name.</param>
        /// <param name="suffix">A suffix attached to this record which is usually rule-dependant; e.g.
        /// "disabled warnings" or "effective warning level" or similar.</param>
        /// <returns>The new <see cref="CompilandRecord"/> with file path parameters sanitized.</returns>
        public static CompilandRecord CreateSanitized(string obj, string lib, string suffix)
        {
            if (obj == lib)
            {
                // This happens when the object file is a precompiled header. Since it's a precompiled header it's logically an object, so we'll ignore the library
                lib = null;
            }

            obj = SanitizeName(obj);
            lib = SanitizeName(lib);
            return new CompilandRecord(obj, lib, suffix);
        }

        /// <summary>Returns a string describing this record.</summary>
        /// <returns>A <see cref="T:System.String" /> containing a fully qualified type name.</returns>
        /// <seealso cref="M:System.ValueType.ToString()"/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            this.AppendString(sb);
            return sb.ToString();
        }

        /// <summary>Appends this record's string representation to a <see cref="StringBuilder"/> instance.</summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
        public void AppendString(StringBuilder sb)
        {
            if (this.Object == this.Library)
            {
                if (this.Object != null)
                {
                    sb.Append(this.Object);
                }
            }
            else if (this.Object == null)
            {
                // Library != null because Library != Object
                sb.Append(this.Library);
            }
            else if (this.Library == null)
            {
                // Object != null because Library != Object
                sb.Append(this.Object);
            }
            else
            {
                sb.Append(this.Object);
                sb.Append(" (");
                sb.Append(this.Library);
                sb.Append(')');
            }

            if (!string.IsNullOrWhiteSpace(this.Suffix))
            {
                sb.Append(' ');
                sb.Append(this.Suffix);
            }
        }

        /// <summary>Returns a hash code for this instance.</summary>
        /// <returns>A hash code for this instance.</returns>
        public override int GetHashCode()
        {
            var hash = new MultiplyByPrimesHash();
            if (this.Object == null)
            {
                hash.Add(0);
            }
            else
            {
                hash.Add(StringComparer.OrdinalIgnoreCase.GetHashCode(this.Object));
            }

            if (this.Library == null)
            {
                hash.Add(0);
            }
            else
            {
                hash.Add(StringComparer.OrdinalIgnoreCase.GetHashCode(this.Library));
            }

            if (this.Suffix == null)
            {
                hash.Add(0);
            }
            else
            {
                hash.Add(this.Suffix.GetHashCode());
            }

            return hash.GetHashCode();
        }

        /// <summary>Tests if this object is considered equal to another.</summary>
        /// <param name="obj">The object to compare to this instance.</param>
        /// <returns>true if the objects are considered equal, false if they are not.</returns>
        public override bool Equals(object obj)
        {
            var converted = obj as CompilandRecord?;
            return converted.HasValue && this.Equals(converted.Value);
        }

        /// <summary>Tests if this <see cref="CompilandRecord"/> is considered equal to another.</summary>
        /// <param name="other">The compiland record to compare to this instance.</param>
        /// <returns>true if the objects are considered equal, false if they are not.</returns>
        public bool Equals(CompilandRecord other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(this.Object, other.Object)
                && StringComparer.OrdinalIgnoreCase.Equals(this.Library, other.Library)
                && this.Suffix == other.Suffix;
        }

        /// <summary>
        /// Compares this <see cref="CompilandRecord"/> object to another to determine their relative ordering.
        /// </summary>
        /// <param name="other">Another instance to compare.</param>
        /// <returns>
        /// Negative if this instance is less than the other, 0 if they are equal, or positive if this is
        /// greater.
        /// </returns>
        public int CompareTo(CompilandRecord other)
        {
            int cmp = StringComparer.OrdinalIgnoreCase.Compare(this.Library, other.Library);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = StringComparer.OrdinalIgnoreCase.Compare(this.Object, other.Object);
            if (cmp != 0)
            {
                return cmp;
            }

            return StringComparer.Ordinal.Compare(this.Suffix, other.Suffix);
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                return Path.GetFileName(name);
            }

            // From http://referencesource.microsoft.com/#mscorlib/system/io/path.cs,1291
            catch (ArgumentException)
            {
                return name;
            }
        }
    }
}
