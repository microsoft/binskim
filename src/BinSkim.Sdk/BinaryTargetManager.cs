using Microsoft.CodeAnalysis.BinaryParsers;
using System;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    class BinaryTargetManager
    {
        // We may want to consider changing this to an extension/plugin model rather than a hardcoded list of supported binary parsers.
        // However, for now this will do.
        public static IBinary GetBinaryFromFile(Uri uri, string symbolPath = null, string localSymbolDirectories = null)
        {
            if (PEBinary.CanLoadBinary(uri))
            {
                return new PEBinary(uri, symbolPath, localSymbolDirectories);
            }
            else if (ELFBinary.CanLoadBinary(uri))
            {
                return new ELFBinary(uri);
            }
            else
            {
                return null;
            }
        }
    }
}
