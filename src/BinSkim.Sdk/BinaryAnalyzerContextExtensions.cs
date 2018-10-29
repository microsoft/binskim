using Microsoft.CodeAnalysis.BinaryParsers;
using System;

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
                // Attempted to cast a binary target to a '{0}', but was unable to.  This indicates a programmer error in rules evaluating that sort of target.
                throw new InvalidOperationException(string.Format(SdkResources.IllegalBinaryCast, "PEBinary"));
            }
            return ret;
        }

        public static bool IsELF(this BinaryAnalyzerContext target)
        {
            ELFBinary ret = target.Binary as ELFBinary;
            if (ret == null)
            {
                return false;
            }
            return true;
        }

        public static ELFBinary ELFBinary(this BinaryAnalyzerContext target)
        {
            ELFBinary ret = target.Binary as ELFBinary;
            if (ret == null)
            {
                // Attempted to cast a binary target to a '{0}', but was unable to.  This indicates a programmer error in rules evaluating that sort of target.
                throw new InvalidOperationException(string.Format(SdkResources.IllegalBinaryCast, "ELFBinary"));
            }
            return ret;
        }
    }
}
