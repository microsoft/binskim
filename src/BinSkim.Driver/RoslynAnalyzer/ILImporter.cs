// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;

using Microsoft.CodeAnalysis.Semantics;

using ILExceptionRegion = System.Reflection.Metadata.ExceptionRegion;

namespace Microsoft.CodeAnalysis.IL
{
    internal partial class ILImporter
    {
        private Compilation _compilation;
        private IMethodSymbol _method;
        private byte[] _ilBytes;
        private ExceptionRegion[] _exceptionRegions;
        private List<IStatement> _statements;
        private int _labelCount;

        public ILImporter(Compilation compilation, IMethodSymbol method, MethodBodyBlock body)
        {
            if (body.ExceptionRegions.Length != 0)
            {
                throw new NotImplementedException();
            }

            _compilation = compilation;
            _method = method;
            _ilBytes = body.GetILBytes();
            _exceptionRegions = new ExceptionRegion[0];
            _statements = new List<IStatement>();
        }

        public IBlockStatement Import()
        {
            FindBasicBlocks();
            ImportBasicBlocks();

            var statements = ImmutableArray.CreateBuilder<IStatement>(_statements.Count + _labelCount);

            foreach (var block in _basicBlocks)
            {
                if (block != null)
                {
                    if (block.Label != null)
                    {
                        statements.Add(new LabelStatement(block.Label));
                    }

                    for (int i = block.FirstStatementOffset; i <= block.LastStatementOffset; i++)
                    {
                        statements.Add(_statements[i]);
                    }
                }
            }

            return new BlockStatement(ImmutableArray<ILocalSymbol>.Empty, statements.MoveToImmutable());
        }

        private void Push(StackValue value)
        {
            if (_stackTop >= _stack.Length)
            {
                Array.Resize(ref _stack, 2 * _stackTop + 3);
            }

            _stack[_stackTop++] = value;
        }

        private void Push(StackValueKind kind, IExpression expression)
        {
            Push(new StackValue(kind, expression));
        }

        private StackValue Pop()
        {
            if (_stackTop == 0)
            {
                throw new BadImageFormatException();
            }

            return _stack[--_stackTop];
        }

        private void Append(IStatement statement)
        {
            ReqireEmptyStackForNow();
            _statements.Add(statement);
        }

        private void ReqireEmptyStackForNow()
        {
            if (_stackTop != 0)
            {
                // TODO: We don't yet have the machinery to (1) move between basic
                // blocks or (2) append statements when the operand stack isn't empty. 
                // We'll have to create temporaries to deal with those cases.
                //
                // (2) might not be as obviously problematic, but if we leave 
                // expressions on the stack and emit a statement, we'd create
                // a tree that does not preserve execution order semantics.
                throw new NotImplementedException();
            }
        }

        private ITypeSymbol GetWellKnownType(WellKnownType wellKnownType)
        {
            return _compilation.GetSpecialType((SpecialType)wellKnownType);
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
            basicBlock.FirstStatementOffset = _statements.Count;
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
            basicBlock.LastStatementOffset = _statements.Count - 1;
        }

        private void ImportNop()
        {
        }

        private void ImportBreak()
        {
            throw new NotImplementedException();
        }

        private void ImportLoadVar(int index, bool argument)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreVar(int index, bool argument)
        {
            throw new NotImplementedException();
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
            throw new NotImplementedException();
        }

        private void ImportDup()
        {
            throw new NotImplementedException();
        }

        private void ImportPop()
        {
            Append(new ExpressionStatement(Pop().Expression));
        }

        private void ImportJmp(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
            throw new NotImplementedException();
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadInt(long value, StackValueKind kind)
        {
            object boxedValue;
            ITypeSymbol type;

            if (kind == StackValueKind.Int32)
            {
                boxedValue = (int)value;
                type = Int32Type;
            }
            else
            {
                Debug.Assert(kind == StackValueKind.Int64);
                boxedValue = value;
                type = Int64Type;
            }

            Push(kind, new LiteralExpression(boxedValue, type));
        }

        private void ImportLoadFloat(double value)
        {
            Push(StackValueKind.Float, new LiteralExpression(value, DoubleType));
        }

        private void ImportLoadNull()
        {
            Push(StackValueKind.ObjRef, new LiteralExpression(null, ObjectType));
        }

        private void ImportReturn()
        {
            Append(new ReturnStatement(_method.ReturnsVoid ? null : Pop().Expression));
        }

        private void ImportFallthrough(BasicBlock next)
        {
            ReqireEmptyStackForNow();
            MarkBasicBlock(next);
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            throw new NotImplementedException();
        }

        private void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
            StackValue left, right;

            if (opcode == ILOpcode.brtrue || opcode == ILOpcode.brfalse)
            {
                left = Pop();
                right = new StackValue(left.Kind, GetZeroLiteral(left.Kind));
                opcode = opcode == ILOpcode.brtrue ? ILOpcode.bne_un : ILOpcode.beq;
            }
            else
            {
                right = Pop();
                left = Pop();
            }

            Append(
                new IfStatement(
                    new BinaryOperatorExpression(
                        GetBranchKind(opcode, GetStackKind(left.Kind, right.Kind)),
                        left.Expression,
                        right.Expression,
                        BooleanType),
                    new GoToStatement(
                        GetOrCreateLabel(target))));

            ImportFallthrough(fallthrough);
            MarkBasicBlock(target);
        }

        private void ImportBinaryOperation(ILOpcode opcode)
        {
            throw new NotImplementedException();
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
            var right = Pop();
            var left = Pop();

            Push(
                left.Kind,
                new BinaryOperatorExpression(
                    GetShiftKind(opcode),
                    left.Expression,
                    right.Expression,
                    left.Expression.ResultType));
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
            throw new NotImplementedException();
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            // unsigned argument deliberately unusued: it is captured in wellKnownType.
            // FEEDBACK: How to represent checkOverflow = true?

            Push(
                GetStackKind(wellKnownType),
                new ConversionExpression(
                    ConversionKind.Cast,
                    Pop().Expression,
                    GetWellKnownType(wellKnownType)));
        }

        private void ImportLoadField(int token, bool isStatic)
        {
            throw new NotImplementedException();
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreField(int token, bool isStatic)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadIndirect(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadIndirect(ITypeSymbol type)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(GetTypeFromToken(token));
        }

        private void ImportStoreIndirect(ITypeSymbol type)
        {
            throw new NotImplementedException();
        }

        private void ImportThrow()
        {
            Append(new ThrowStatement(Pop().Expression));
        }

        private void ImportLoadString(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportInitObj(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportBox(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLeave(BasicBlock target)
        {
            throw new NotImplementedException();
        }

        private void ImportEndFinally()
        {
            throw new NotImplementedException();
        }

        private void ImportNewArray(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadElement(int token)
        {
            ImportLoadElement(GetTypeFromToken(token));
        }

        private void ImportLoadElement(ITypeSymbol type)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreElement(int token)
        {
            ImportStoreElement(GetTypeFromToken(token));
        }

        private void ImportStoreElement(ITypeSymbol type)
        {
            throw new NotImplementedException();
        }

        private void ImportAddressOfElement(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadLength()
        {
            throw new NotImplementedException();
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportCpOpj(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportRefAnyVal(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportCkFinite()
        {
            throw new NotImplementedException();
        }

        private void ImportMkRefAny(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLdToken(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLocalAlloc()
        {
            throw new NotImplementedException();
        }

        private void ImportEndFilter()
        {
            throw new NotImplementedException();
        }

        private void ImportCpBlk()
        {
            throw new NotImplementedException();
        }

        private void ImportInitBlk()
        {
            throw new NotImplementedException();
        }

        private void ImportRethrow()
        {
            Append(new ThrowStatement(null));
        }

        private void ImportSizeOf(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportRefAnyType()
        {
            throw new NotImplementedException();
        }

        private void ImportArgList()
        {
            throw new NotImplementedException();
        }

        private void ImportUnalignedPrefix(byte alignment)
        {
            throw new NotImplementedException();
        }

        private void ImportVolatilePrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportTailPrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportConstrainedPrefix(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportNoPrefix(byte mask)
        {
            throw new NotImplementedException();
        }

        private void ImportReadOnlyPrefix()
        {
            throw new NotImplementedException();
        }

        private static BinaryOperationKind GetBranchKind(ILOpcode opcode, StackValueKind kind)
        {
            switch (kind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return GetIntegerBranchKind(opcode);
                case StackValueKind.Float:
                    return GetFloatBranchKind(opcode);
                case StackValueKind.ObjRef:
                    return GetObjectBranchKind(opcode);
            }

            throw new BadImageFormatException();
        }

        private static BinaryOperationKind GetIntegerBranchKind(ILOpcode opcode)
        {
            switch (opcode)
            {
                case ILOpcode.beq:
                    return BinaryOperationKind.IntegerEquals;
                case ILOpcode.bge:
                    return BinaryOperationKind.IntegerGreaterThanOrEqual;
                case ILOpcode.bgt:
                    return BinaryOperationKind.IntegerGreaterThan;
                case ILOpcode.ble:
                    return BinaryOperationKind.IntegerLessThanOrEqual;
                case ILOpcode.blt:
                    return BinaryOperationKind.IntegerLessThan;
                case ILOpcode.bne_un:
                    return BinaryOperationKind.IntegerNotEquals;
                case ILOpcode.bge_un:
                    return BinaryOperationKind.UnsignedGreaterThan;
                case ILOpcode.bgt_un:
                    return BinaryOperationKind.UnsignedGreaterThan;
                case ILOpcode.ble_un:
                    return BinaryOperationKind.UnsignedLessThanOrEqual;
                case ILOpcode.blt_un:
                    return BinaryOperationKind.UnsignedLessThan;
                default:
                    throw Unreachable();
            }
        }

        private static BinaryOperationKind GetFloatBranchKind(ILOpcode opcode)
        {
            switch (opcode)
            {
                case ILOpcode.beq:
                    return BinaryOperationKind.FloatingEquals;
                case ILOpcode.bge:
                    return BinaryOperationKind.FloatingGreaterThan;
                case ILOpcode.bgt:
                    return BinaryOperationKind.FloatingGreaterThanOrEqual;
                case ILOpcode.ble:
                    return BinaryOperationKind.FloatingLessThanOrEqual;
                case ILOpcode.blt:
                    return BinaryOperationKind.FloatingLessThan;
                case ILOpcode.bne_un:
                    return BinaryOperationKind.FloatingNotEquals;
                default:
                    throw Unreachable();
            }
        }

        private static BinaryOperationKind GetShiftKind(ILOpcode opcode)
        {
            switch (opcode)
            {
                case ILOpcode.shl:
                    return BinaryOperationKind.IntegerLeftShift;
                case ILOpcode.shr:
                    return BinaryOperationKind.IntegerRightShift;
                case ILOpcode.shr_un:
                    return BinaryOperationKind.UnsignedRightShift;
                default:
                    throw Unreachable();
            }
        }

        private static BinaryOperationKind GetObjectBranchKind(ILOpcode opcode)
        {
            switch (opcode)
            {
                case ILOpcode.beq:
                    return BinaryOperationKind.ObjectEquals;
                case ILOpcode.bne_un:
                    return BinaryOperationKind.ObjectNotEquals;
                default:
                    throw new NotImplementedException();
            }
        }

        private StackValueKind GetStackKind(WellKnownType wellKnownType)
        {
            switch (wellKnownType)
            {
                case WellKnownType.Byte:
                case WellKnownType.SByte:
                case WellKnownType.Char:
                case WellKnownType.Int16:
                case WellKnownType.UInt16:
                case WellKnownType.Int32:
                case WellKnownType.UInt32:
                    return StackValueKind.Int32;
                case WellKnownType.Single:
                case WellKnownType.Double:
                    return StackValueKind.Float;
                case WellKnownType.Int64:
                case WellKnownType.UInt64:
                    return StackValueKind.Int64;
                case WellKnownType.IntPtr:
                case WellKnownType.UIntPtr:
                    return StackValueKind.NativeInt;
                default:
                    throw Unreachable();
            }
        }

        private static StackValueKind GetStackKind(StackValueKind lhsKind, StackValueKind rhsKind)
        {
            // the ordering of StackValueKind is chosen to make this work (assuming valid IL)
            return lhsKind > rhsKind ? lhsKind : rhsKind;
        }

        private LiteralExpression GetZeroLiteral(StackValueKind kind)
        {
            switch (kind)
            {
                case StackValueKind.Int32:
                    return new LiteralExpression(0, Int32Type);
                case StackValueKind.Int64:
                    return new LiteralExpression(0L, Int64Type);
                case StackValueKind.Float:
                    return new LiteralExpression(0.0, DoubleType);
                case StackValueKind.ObjRef:
                    return new LiteralExpression(null, ObjectType);
                case StackValueKind.NativeInt:
                    return new LiteralExpression(IntPtr.Zero, IntPtrType);
                default:
                    throw new NotImplementedException();
            }
        }

        private static Exception Unreachable()
        {
            Debug.Fail("Code path expected to be unreachable.");
            return null;
        }

        private ITypeSymbol GetTypeFromToken(int token)
        {
            throw new NotImplementedException();
        }

        private ILabelSymbol GetOrCreateLabel(BasicBlock block)
        {
            if (block.Label == null)
            {
                block.Label = new LabelSymbol(block.StartOffset, _method);
                _labelCount++;
            }

            return block.Label;
        }

        private ITypeSymbol BooleanType => _compilation.GetSpecialType(SpecialType.System_Boolean);
        private ITypeSymbol DoubleType => _compilation.GetSpecialType(SpecialType.System_Double);
        private ITypeSymbol Int32Type => _compilation.GetSpecialType(SpecialType.System_Int32);
        private ITypeSymbol Int64Type => _compilation.GetSpecialType(SpecialType.System_Int64);
        private ITypeSymbol IntPtrType => _compilation.GetSpecialType(SpecialType.System_IntPtr);
        private ITypeSymbol ObjectType => _compilation.GetSpecialType(SpecialType.System_Object);

        private enum WellKnownType
        {
            Char = SpecialType.System_Char,
            SByte = SpecialType.System_SByte,
            Byte = SpecialType.System_Byte,
            Int16 = SpecialType.System_Int16,
            UInt16 = SpecialType.System_UInt16,
            Int32 = SpecialType.System_Int32,
            UInt32 = SpecialType.System_UInt32,
            Int64 = SpecialType.System_Int64,
            UInt64 = SpecialType.System_UInt64,
            IntPtr = SpecialType.System_IntPtr,
            UIntPtr = SpecialType.System_UIntPtr,
            Single = SpecialType.System_Single,
            Double = SpecialType.System_Double,
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
            public StackValue(StackValueKind kind, IExpression expression)
            {
                Kind = kind;
                Expression = expression;
            }

            public readonly IExpression Expression;
            public readonly StackValueKind Kind;
        }

        private sealed class BasicBlock
        {
            // Required fields
            public BasicBlock Next;
            public int StartOffset;
            public int EndOffset;
            public StackValue[] EntryStack = null; // suppress warning in initial checkin
            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;

            // Custom fields
            public ILabelSymbol Label;
            public int FirstStatementOffset;
            public int LastStatementOffset;
        }
    }
}
