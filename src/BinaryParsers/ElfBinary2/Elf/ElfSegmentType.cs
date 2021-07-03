// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.Elf
{
    public enum ElfSegmentType : uint
    {
        PT_NULL = 0,            // Unused segment.
        PT_LOAD = 1,            // Loadable segment.
        PT_DYNAMIC = 2,         // Dynamic linking information.
        PT_INTERP = 3,          // Interpreter pathname.
        PT_NOTE = 4,            // Auxiliary information.
        PT_SHLIB = 5,           // Reserved.
        PT_PHDR = 6,            // The program header table itself.
        PT_TLS = 7,             // The thread-local storage template.
        PT_LOOS = 0x60000000,   // Lowest operating system-specific pt entry type.
        PT_HIOS = 0x6fffffff,   // Highest operating system-specific pt entry type.
        PT_LOPROC = 0x70000000, // Lowest processor-specific program hdr entry type.
        PT_HIPROC = 0x7fffffff, // Highest processor-specific program hdr entry type.

        // x86-64 program header types.
        // These all contain stack unwind tables.
        PT_GNU_EH_FRAME = 0x6474e550,

        PT_SUNW_EH_FRAME = 0x6474e550,
        PT_SUNW_UNWIND = 0x6464e550,

        PT_GNU_STACK = 0x6474e551,    // Indicates stack executability.
        PT_GNU_RELRO = 0x6474e552,    // Read-only after relocation.
        PT_GNU_PROPERTY = 0x6474e553, // .note.gnu.property notes sections.

        PT_OPENBSD_RANDOMIZE = 0x65a3dbe6, // Fill with random data.
        PT_OPENBSD_WXNEEDED = 0x65a3dbe7,  // Program does W^X violations.
        PT_OPENBSD_BOOTDATA = 0x65a41be6,  // Section for boot arguments.

        // ARM program header types.
        PT_ARM_ARCHEXT = 0x70000000, // Platform architecture compatibility info

        // These all contain stack unwind tables.
        PT_ARM_EXIDX = 0x70000001,

        PT_ARM_UNWIND = 0x70000001,

        // MIPS program header types.
        PT_MIPS_REGINFO = 0x70000000,  // Register usage information.

        PT_MIPS_RTPROC = 0x70000001,   // Runtime procedure table.
        PT_MIPS_OPTIONS = 0x70000002,  // Options segment.
        PT_MIPS_ABIFLAGS = 0x70000003, // Abiflags segment.
    }
}
