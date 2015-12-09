// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection.Metadata;

using ILExceptionRegion = System.Reflection.Metadata.ExceptionRegion;

namespace Microsoft.CodeAnalysis.IL
{
    internal partial class ILImporter
    {
        private byte[] _ilBytes;
        private ExceptionRegion[] _exceptionRegions;

        public ILImporter()
        {
            // suppress warning in initial checkin.
            _ilBytes = new byte[0];
            _exceptionRegions = new ExceptionRegion[0];
        }

        private void Push(StackValue value)
        {
            // suppress warning in initial checkin
            _stackTop++; 
        }

        private ITypeSymbol GetWellKnownType(WellKnownType wellKnownType)
        {
            throw new NotImplementedException();
        }

        private void MarkInstructionBoundary()
        {
        }

        private void StartImportingInstruction()
        {
        }

        private void EndImportingInstruction()
        {
        }

        private void StartImportingBasicBlock(BasicBlock basicBlock)
        {
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
        }

        private void ImportNop()
        {
        }

        private void ImportBreak()
        {
        }

        private void ImportLoadVar(int index, bool argument)
        {
        }

        private void ImportStoreVar(int index, bool argument)
        {
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
        }

        private void ImportDup()
        {
        }

        private void ImportPop()
        {
        }

        private void ImportJmp(int token)
        {
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
        }

        private void ImportLoadInt(long value, StackValueKind kind)
        {
        }

        private void ImportLoadFloat(double value)
        {
        }

        private void ImportLoadNull()
        {
        }

        private void ImportReturn()
        {
        }

        private void ImportFallthrough(BasicBlock next)
        {
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
        }

        private void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
        }

        private void ImportBinaryOperation(ILOpcode opcode)
        {
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
        }

        private void ImportLoadField(int token, bool isStatic)
        {
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
        }

        private void ImportStoreField(int token, bool isStatic)
        {
        }

        private void ImportLoadIndirect(int token)
        {
        }

        private void ImportLoadIndirect(ITypeSymbol type)
        {
        }

        private void ImportStoreIndirect(int token)
        {
        }

        private void ImportStoreIndirect(ITypeSymbol type)
        {
        }

        private void ImportThrow()
        {
        }

        private void ImportLoadString(int token)
        {
        }

        private void ImportInitObj(int token)
        {
        }

        private void ImportBox(int token)
        {
        }

        private void ImportLeave(BasicBlock target)
        {
        }

        private void ImportEndFinally()
        {
        }

        private void ImportNewArray(int token)
        {
        }

        private void ImportLoadElement(int token)
        {
        }

        private void ImportLoadElement(ITypeSymbol type)
        {
        }

        private void ImportStoreElement(int token)
        {
        }

        private void ImportStoreElement(ITypeSymbol type)
        {
        }

        private void ImportAddressOfElement(int token)
        {
        }

        private void ImportLoadLength()
        {
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
        }

        private void ImportCpOpj(int token)
        {
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
        }

        private void ImportRefAnyVal(int token)
        {
        }

        private void ImportCkFinite()
        {
        }

        private void ImportMkRefAny(int token)
        {
        }

        private void ImportLdToken(int token)
        {
        }

        private void ImportLocalAlloc()
        {
        }

        private void ImportEndFilter()
        {
        }

        private void ImportCpBlk()
        {
        }

        private void ImportInitBlk()
        {
        }

        private void ImportRethrow()
        {
        }

        private void ImportSizeOf(int token)
        {
        }

        private void ImportRefAnyType()
        {
        }

        private void ImportArgList()
        {
        }

        private void ImportUnalignedPrefix(byte alignment)
        {
        }

        private void ImportVolatilePrefix()
        {
        }

        private void ImportTailPrefix()
        {
        }

        private void ImportConstrainedPrefix(int token)
        {
        }

        private void ImportNoPrefix(byte mask)
        {
        }

        private void ImportReadOnlyPrefix()
        {
        }

        private enum WellKnownType
        {
            Char,
            SByte,
            Byte,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,
            IntPtr,
            UIntPtr,
            Single,
            Double,
        }

        private static class ILExceptionRegionKind
        {
            public const ExceptionRegionKind Filter = ExceptionRegionKind.Filter;
        }

        private sealed class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        }

        private struct StackValue
        {
        }

        private sealed class BasicBlock
        {
            public BasicBlock Next;
            public int StartOffset;
            public int EndOffset;
            public StackValue[] EntryStack = null; // suppress warning in initial checkin
            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;
        }
    }
}
