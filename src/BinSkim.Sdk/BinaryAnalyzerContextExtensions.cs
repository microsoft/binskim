using System;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

namespace Microsoft.CodeAnalysis.IL.Sdk
{
    public static class BinaryAnalyzerContextExtensions
    {
        public static bool IsPE(this BinaryAnalyzerContext target)
        {
            return target.Binary is PEBinary;
        }

        public static PEBinary PEBinary(this BinaryAnalyzerContext target)
        {
            if (!(target.Binary is PEBinary ret))
            {
                // Attempted to cast a binary target to a '{0}', but was unable to.  This indicates a programmer error in rules evaluating that sort of target.
                throw new InvalidOperationException(string.Format(SdkResources.IllegalBinaryCast, "PEBinary"));
            }
            return ret;
        }

        public static bool IsELF(this BinaryAnalyzerContext target)
        {
            return target.Binary is ELFBinary;
        }

        public static ELFBinary ELFBinary(this BinaryAnalyzerContext target)
        {
            if (!(target.Binary is ELFBinary ret))
            {
                // Attempted to cast a binary target to a '{0}', but was unable to.  This indicates a programmer error in rules evaluating that sort of target.
                throw new InvalidOperationException(string.Format(SdkResources.IllegalBinaryCast, "ELFBinary"));
            }
            return ret;
        }

        public static bool IsMachO(this BinaryAnalyzerContext target)
        {
            return target.Binary is MachOBinary;
        }

        public static MachOBinary MachOBinary(this BinaryAnalyzerContext target)
        {
            if (!(target.Binary is MachOBinary ret))
            {
                // Attempted to cast a binary target to a '{0}', but was unable to.  This indicates a programmer error in rules evaluating that sort of target.
                throw new InvalidOperationException(string.Format(SdkResources.IllegalBinaryCast, "MachOBinary"));
            }
            return ret;
        }

        public static bool IsDwarf(this BinaryAnalyzerContext target)
        {
            return target.Binary is ELFBinary || target.Binary is MachOBinary;
        }

        public static IDwarfBinary DwarfBinary(this BinaryAnalyzerContext target)
        {
            if (target.Binary is MachOBinary machO)
            {
                return machO;
            }
            else if (target.Binary is ELFBinary elf)
            {
                return elf;
            }

            // Attempted to cast a binary target to a '{0}', but was unable to.  This indicates a programmer error in rules evaluating that sort of target.
            throw new InvalidOperationException(string.Format(SdkResources.IllegalBinaryCast, "MachOBinary"));
        }
    }
}
