using Microsoft.CodeAnalysis.BinaryParsers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    class BinaryTargetManager
    {
        public static IBinary GetBinaryFromFile(Uri uri)
        {
            if(PEBinary.CanLoadBinary(uri))
            {
                return new PEBinary(uri);
            }
            else
            {
                return null;
            }
        }
    }
}
