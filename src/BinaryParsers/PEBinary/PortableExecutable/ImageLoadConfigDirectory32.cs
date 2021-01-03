// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.PortableExecutable;

namespace Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable
{
    public class ImageLoadConfigDirectory32 : ImageHeader
    {
        private static readonly ImageFieldData[] s_fields = null;

        public ImageLoadConfigDirectory32(PEHeader parentHeader, SafePointer sp) : base(parentHeader, sp)
        {
        }

        protected override ImageFieldData[] GetFields() { return s_fields; }

        public override ImageHeader Create(PEHeader parentHeader, SafePointer sp)
        {
            return new ImageLoadConfigDirectory32(parentHeader, sp);
        }

        static ImageLoadConfigDirectory32()
        {
            int n = 0;
            s_fields = new ImageFieldData[25];
            s_fields[0] = new ImageFieldData(0, "Size", Type.INT32, 1);
            s_fields[1] = new ImageFieldData(ShiftOffset(s_fields, n++), "TimeDateStamp", Type.UINT32, 1);
            s_fields[2] = new ImageFieldData(ShiftOffset(s_fields, n++), "MajorVersion", Type.UINT16, 1);
            s_fields[3] = new ImageFieldData(ShiftOffset(s_fields, n++), "MinorVersion", Type.UINT16, 1);
            s_fields[4] = new ImageFieldData(ShiftOffset(s_fields, n++), "GlobalFlagsClear", Type.UINT32, 1);
            s_fields[5] = new ImageFieldData(ShiftOffset(s_fields, n++), "GlobalFlagsSet", Type.UINT32, 1);
            s_fields[6] = new ImageFieldData(ShiftOffset(s_fields, n++), "CriticalSectionDefaultTimeout", Type.UINT32, 1);
            s_fields[7] = new ImageFieldData(ShiftOffset(s_fields, n++), "DeCommitFreeBlockThreshold", Type.UINT32, 1);
            s_fields[8] = new ImageFieldData(ShiftOffset(s_fields, n++), "DeCommitTotalFreeThreshold", Type.UINT32, 1);
            s_fields[9] = new ImageFieldData(ShiftOffset(s_fields, n++), "LockPrefixTable", Type.UINT32, 1);
            s_fields[10] = new ImageFieldData(ShiftOffset(s_fields, n++), "MaximumAllocationSize", Type.UINT32, 1);
            s_fields[11] = new ImageFieldData(ShiftOffset(s_fields, n++), "VirtualMemoryThreshold", Type.UINT32, 1);
            s_fields[12] = new ImageFieldData(ShiftOffset(s_fields, n++), "ProcessHeapFlags", Type.UINT32, 1);
            s_fields[13] = new ImageFieldData(ShiftOffset(s_fields, n++), "ProcessAffinityMask", Type.UINT32, 1);
            s_fields[14] = new ImageFieldData(ShiftOffset(s_fields, n++), "CSDVersion", Type.UINT16, 1);
            s_fields[15] = new ImageFieldData(ShiftOffset(s_fields, n++), "Reserved1", Type.UINT16, 1);
            s_fields[16] = new ImageFieldData(ShiftOffset(s_fields, n++), "EditList", Type.UINT32, 1);
            s_fields[17] = new ImageFieldData(ShiftOffset(s_fields, n++), "SecurityCookie", Type.UINT32, 1);
            s_fields[18] = new ImageFieldData(ShiftOffset(s_fields, n++), "SEHandlerTable", Type.UINT32, 1);
            s_fields[19] = new ImageFieldData(ShiftOffset(s_fields, n++), "SEHandlerCount", Type.UINT32, 1);
            s_fields[20] = new ImageFieldData(ShiftOffset(s_fields, n++), "GuardCFCheckFunctionPointer", Type.UINT32, 1);
            s_fields[21] = new ImageFieldData(ShiftOffset(s_fields, n++), "Reserved2", Type.UINT32, 1);
            s_fields[22] = new ImageFieldData(ShiftOffset(s_fields, n++), "GuardCFFunctionTable", Type.UINT32, 1);
            s_fields[23] = new ImageFieldData(ShiftOffset(s_fields, n++), "GuardCFFunctionCount", Type.UINT32, 1);
            s_fields[24] = new ImageFieldData(ShiftOffset(s_fields, n++), "GuardFlags", Type.UINT32, 1);
        }

        public enum Fields
        {
            Size = 0,
            TimeDateStamp,
            MajorVersion,
            MinorVersion,
            GlobalFlagsClear,
            GlobalFlagsSet,
            CriticalSectionDefaultTimeout,
            DeCommitFreeBlockThreshold,
            DeCommitTotalFreeThreshold,
            LockPrefixTable,
            MaximumAllocationSize,
            VirtualMemoryThreshold,
            ProcessHeapFlags,
            ProcessAffinityMask,
            CSDVersion,
            Reserved1,
            EditList,
            SecurityCookie,
            SEHandlerTable,
            SEHandlerCount,
            GuardCFCheckFunctionPointer,
            Reserved2,
            GuardCFFunctionTable,
            GuardCFFunctionCount,
            GuardFlags
        }
    }
}
