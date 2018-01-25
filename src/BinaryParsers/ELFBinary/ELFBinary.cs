// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ELFSharp.ELF;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class ELFBinary : BinaryBase
    {
        public ELFBinary(Uri uri) : base(uri)
        {
            try
            {
                ELF = ELFReader.Load(Path.GetFullPath(uri.LocalPath));
                Valid = true;
            }
            // TODO--we may want to better enumerate the possible exceptions.
            // For now, though, we'll generically catch any of them.
            catch (Exception e)
            {
                LoadException = e;
                Valid = false;
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
    }
}
