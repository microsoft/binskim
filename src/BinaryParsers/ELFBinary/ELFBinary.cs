// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using ELFSharp.ELF;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class ELFBinary : BinaryBase
    {
        public ELFBinary(Uri uri) : base(uri)
        {
            try
            {
                this.ELF = ELFReader.Load(Path.GetFullPath(uri.LocalPath));
                this.Compilers = ELFUtility.GetELFCompilers(this.ELF);
                this.Valid = true;
            }
            // At some point, we may want to better enumerate expected vs. unexpected exceptions.
            // For now, though, we'll generically catch any of them--ELFSharp can throw a number of different exceptions
            // if given an invalid ELF file.
            catch (Exception e)
            {
                this.LoadException = e;
                this.Valid = false;
            }
        }

        public static bool CanLoadBinary(Uri uri)
        {
            try
            {
                return ELFReader.CheckELFType(Path.GetFullPath(uri.LocalPath)) != Class.NotELF;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public IELF ELF { get; private set; }

        public ELFCompiler[] Compilers { get; private set; }
    }
}
