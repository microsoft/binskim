using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public static class BinaryAnalyzerContextExtensions
    {
        public static bool IsPE(this BinaryAnalyzerContext target)
        {
            PEBinary ret = target.Binary as PEBinary;
            if (ret == null)
            {
                return false;
            }
            return true;
        }

        public static PEBinary PEBinary(this BinaryAnalyzerContext target)
        {
            PEBinary ret = target.Binary as PEBinary;
            if (ret == null)
            {
                // Attempted to access PEBinary representation of a non-PE binary target.  This indicates a programmer error.
                throw new InvalidOperationException(SdkResources.IllegalPEBinaryAccess);
            }
            return ret;
        }
    }
}
