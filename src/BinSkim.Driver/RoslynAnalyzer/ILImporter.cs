// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Microsoft.CodeAnalysis.Semantics;

namespace Microsoft.CodeAnalysis.IL
{
    internal partial class ILImporter
    {
        private Compilation _compilation;
        private IMethodSymbol _method;
        private byte[] _ilBytes;
        private ExceptionRegion[] _exceptionRegions;
        private ImmutableArray<IStatement>.Builder _statements;
        private ImmutableArray<ILocalSymbol>.Builder _locals;
        private IMetadataModuleSymbol _module;
        private MetadataReader _reader;
        private StandaloneSignatureHandle _localSignatureHandle;

        public ILImporter(Compilation compilation, MetadataReader reader, IMethodSymbol method, MethodBodyBlock body)
        {
            _compilation = compilation;
            _reader = reader;
            _method = method;
            _module = (IMetadataModuleSymbol)method.ContainingModule;
            _ilBytes = body.GetILBytes();
            _localSignatureHandle = body.LocalSignature;
            _exceptionRegions = GetExceptionRegions(body);
            _statements = ImmutableArray.CreateBuilder<IStatement>();
        }

        public IBlockStatement Import()
        {
            DecodeLocals();
            FindBasicBlocks();
            FindExceptionRegions();
            ImportBasicBlocks();
            return ImportBlockStatement(0, _ilBytes.Length, locals: _locals.ToImmutable());
        }

        private IBlockStatement ImportBlockStatement(
            int ilStartOffset, 
            int length,
            ImmutableArray<ILocalSymbol> locals = default(ImmutableArray<ILocalSymbol>))
        {
            if (locals.IsDefault)
            {
                locals = ImmutableArray<ILocalSymbol>.Empty;
            }

            var statements = ImmutableArray.CreateBuilder<IStatement>();
            ImportStatements(ilStartOffset, length, statements);
            return new BlockStatement(locals, statements.ToImmutable());
        }

        private void ImportStatements(
            int ilStartOffset,
            int length,
            ImmutableArray<IStatement>.Builder statements)
        {
            for (int i = ilStartOffset, endOffset = ilStartOffset + length; i < endOffset; )
            {
                var block = _basicBlocks[i];

                if (block == null || block.EndOffset < 0)
                {
                    i++;
                    continue;
                }

                if (block.Label != null)
                {
                    statements.Add(new LabelStatement(block.Label));
                }

                var tryRegion = block.OuterTry;
                if (tryRegion != null)
                {
                    block.OuterTry = null; // next time we want to  get the statements
                    Debug.Assert(block.TryStart);
                    statements.Add(ImportTryStatement(tryRegion));
                    i += tryRegion.TryLength;
                    continue;
                }

                for (int j = block.StatementStartIndex; j <= block.StatementEndIndex; j++)
                {
                    statements.Add(_statements[j]);
                }

                i = block.EndOffset + 1;
                block.EndOffset = -1;
            }
        }

        private ITryStatement ImportTryStatement(ExceptionRegion tryRegion)
        {
            var statements = ImmutableArray.CreateBuilder<IStatement>();
            int startOffset = tryRegion.TryOffset;
            int length = tryRegion.TryLength;

            var innerTry = tryRegion.InnerTry;
            if (innerTry != null)
            {
                statements.Add(ImportTryStatement(innerTry));
                startOffset += innerTry.TryLength;
                length -= innerTry.TryLength;
            }

            ImportStatements(startOffset, length, statements);
            var body = new BlockStatement(ImmutableArray<ILocalSymbol>.Empty, statements.ToImmutable());
            var catches = ImmutableArray<ICatch>.Empty;
            var finallyHandler = default(IBlockStatement);

            switch (tryRegion.Kind)
            {
                case ExceptionRegionKind.Catch:
                    catches = ImportCatches(tryRegion);
                    break;

                case ExceptionRegionKind.Finally:
                    finallyHandler = ImportHandler(tryRegion);
                    break;

                case ExceptionRegionKind.Fault:
                    // TODO: fault is not supported by C# / VB / IOperation, but semantics can be emulated.
                    throw new NotImplementedException();

                case ExceptionRegionKind.Filter:
                    // TODO: no representation issue, just not done yet.
                    throw new NotImplementedException();
            }

            return new TryStatement(body, catches, finallyHandler);
        }

        private ImmutableArray<ICatch> ImportCatches(ExceptionRegion region)
        {
            int n = region.CatchCount;
            var catches = ImmutableArray.CreateBuilder<ICatch>(n);
            catches.Count = n;

            for (var r = region; r != null; r = r.PreviousCatch)
            {
                Debug.Assert(r.Kind == ExceptionRegionKind.Catch);
                var type = GetTypeFromHandle(r.CatchType);
                var local = GenerateLocal(type);
                var handler = ImportHandler(r);
                catches[--n] = new Catch(type, local, null, handler);
            }

            return catches.MoveToImmutable();
        }

        private IBlockStatement ImportHandler(ExceptionRegion region)
        {
            return ImportBlockStatement(region.HandlerOffset, region.HandlerLength);
        }

        private ILocalSymbol GenerateLocal(ITypeSymbol type)
        {
            var local = new LocalSymbol($"TMP_{_locals.Count}", _method, type);
            _locals.Add(local);
            return local;
        }

        //
        // Associate exception regions with the first basic block they protect.
        //
        private void FindExceptionRegions()
        {
            //
            // Note that there are several rules to a valid set of exception regions
            // with respect to how they are ordered.
            //
            // See: 
            //
            //   * ECMA-335 CLI Specification: 
            //     * I.12.4.2.5 - Overview of exception handling
            //     * I.12.4.2.7 - Lexical nesting of protected blocks 
            //
            //   * "Expert .NET 2.0 IL Assembly" by Serge Lidin 
            //     * Chapter 14 - Summary of EH Clause Structuring Rules
            //
            //  TODO:  At present, we just assume that they hold, but we'll need 
            //  approriate error handling for when they don't.

            foreach (var region in _exceptionRegions)
            {
                var block = _basicBlocks[region.TryOffset];

                Debug.Assert(region.CatchCount == 0);
                Debug.Assert(region.InnerTry == null);
                Debug.Assert(region.PreviousCatch == null);

                if (region.Kind == ExceptionRegionKind.Catch)
                {
                    region.CatchCount = 1;
                }

                var outerTry = block.OuterTry;
                if (outerTry != null)
                {
                    Debug.Assert(region.TryOffset == outerTry.TryOffset);

                    if (region.TryLength < outerTry.TryLength)
                    {
                        //
                        // Error case - CLI spec I.12.4.2.5: 
                        //
                        //   "The ordering of the exception clauses  in the Exception Handler 
                        //    Table is important. If handlers are nested, the most deeply nested 
                        //    try blocks shall come before the try blocks that enclose them."
                        //
                        throw new NotImplementedException();
                    }

                    if (region.TryLength == outerTry.TryLength &&
                        region.Kind == ExceptionRegionKind.Catch && 
                        outerTry.Kind == ExceptionRegionKind.Catch)
                    {
                        region.PreviousCatch = outerTry;
                        region.CatchCount = outerTry.CatchCount + 1;
                    }
                    else
                    {
                        region.InnerTry = outerTry;
                    }
                }
                block.OuterTry = region;

                ITypeSymbol localType = null;
                switch (region.Kind)
                {
                    case ExceptionRegionKind.Filter:
                        localType = ObjectType;
                        break;
                    case ExceptionRegionKind.Catch:
                        localType = GetTypeFromHandle(region.CatchType);
                        break;
                }

                if (localType != null)
                {
                    var local = GenerateLocal(localType);
                    _basicBlocks[region.HandlerOffset].EntryStack = new[]
                    {
                        new StackValue(
                            StackValueKind.ObjRef,
                            new LocalReferenceExpression(local))
                    };
                }
            }
        }

        private void DecodeLocals()
        {
            if (_localSignatureHandle.IsNil)
            {
                _locals = ImmutableArray<ILocalSymbol>.Empty.ToBuilder();
                return;
            }

            var provider = new ILSignatureProvider(_compilation, _method);
            var signature = _reader.GetStandaloneSignature(_localSignatureHandle);
            var localTypes = signature.DecodeLocalSignature(provider);
            var locals = ImmutableArray.CreateBuilder<ILocalSymbol>(localTypes.Length);

            int i = 0;
            foreach (var type in localTypes)
            {
                locals.Add(new LocalSymbol($"V_{i++}", _method, type));
            }

            _locals = locals;
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

        private void Push(IExpression expression)
        {
            Push(GetStackKind(expression.ResultType), expression);
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
            RequireEmptyStackForNow();
            _statements.Add(statement);
        }

        private void RequireEmptyStackForNow()
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
            basicBlock.StatementStartIndex = _statements.Count;
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
            basicBlock.StatementEndIndex = _statements.Count - 1;
            basicBlock.EndOffset = _currentOffset - 1;

            for (var t = basicBlock.OuterTry; t != null; t = t.InnerTry)
            {
                for (var c = t.PreviousCatch; c != null; c = c.PreviousCatch)
                {
                    MarkBasicBlock(_basicBlocks[c.HandlerOffset]);
                }
                MarkBasicBlock(_basicBlocks[t.HandlerOffset]);
            }
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
            Push(GetVariableReference(index, argument));
        }

        private void ImportStoreVar(int index, bool argument)
        {
            Append(
                new ExpressionStatement(
                    new AssignmentExpression(
                        GetVariableReference(index, argument),
                        Pop().Expression)));
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
            var callee = (IMethodSymbol)GetSymbolFromToken(token);

            if (callee.IsVararg)
            {
                throw new NotImplementedException();
            }

            var arguments = PopArguments(callee.Parameters);

            switch (opcode)
            {
                case ILOpcode.call:
                case ILOpcode.callvirt:
                    var isVirtual = opcode == ILOpcode.callvirt;
                    var instance = callee.IsStatic ? null : Pop().Expression;
                    var invocation = new InvocationExpression(isVirtual, instance, callee, arguments);

                    if (callee.ReturnsVoid)
                    {
                        Append(new ExpressionStatement(invocation));
                    }
                    else
                    {
                        Push(invocation);
                    }
                    break;

                case ILOpcode.newobj:
                    Push(new ObjectCreationExpression(callee, arguments));
                    break;

                case ILOpcode.calli:
                    throw new NotImplementedException();

                default:
                    throw Unreachable();
            }
        }

        private ImmutableArray<IArgument> PopArguments(ImmutableArray<IParameterSymbol> parameters)
        {
            int count = parameters.Length;

            var args = ImmutableArray.CreateBuilder<IArgument>(count);
            args.Count = count;

            for (int i = count - 1; i >= 0; i--)
            {
                args[i] = new Argument(parameters[i], Pop().Expression);
            }

            return args.MoveToImmutable();
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
            RequireEmptyStackForNow();
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
            // unsigned argument deliberately unused: it is captured in wellKnownType.
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
            Push(
                StackValueKind.ObjRef,
                new LiteralExpression(_reader.GetUserString(MetadataTokens.UserStringHandle(token)), StringType));
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
            MarkBasicBlock(target);
            Append(new GoToStatement(GetOrCreateLabel(target)));
        }

        private void ImportEndFinally()
        {
            // TODO: We need to branch if we're not at the lexical end of finally block.
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

        private IReferenceExpression GetVariableReference(int index, bool argument)
        {
            if (!argument)
            {
                return new LocalReferenceExpression(_locals[index]);
            }

            if (!_method.IsStatic)
            {
                index--;
            }

            if (index == -1)
            {
                return new InstanceReferenceExpression(_method);
            }

            return new ParameterReferenceExpression(_method.Parameters[index]);
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
                default:
                    throw new NotImplementedException(); // should byref compares be integer compares?
            }
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
                    return BinaryOperationKind.FloatingGreaterThanOrEqual;
                case ILOpcode.bgt:
                    return BinaryOperationKind.FloatingGreaterThan;
                case ILOpcode.ble:
                    return BinaryOperationKind.FloatingLessThanOrEqual;
                case ILOpcode.blt:
                    return BinaryOperationKind.FloatingLessThan;
                case ILOpcode.bne_un:
                    return BinaryOperationKind.FloatingNotEquals;
                default:
                    throw new NotImplementedException();
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

        private StackValueKind GetStackKind(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Char:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                    return StackValueKind.Int32;
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                    return StackValueKind.Float;
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return StackValueKind.Int64;
                case SpecialType.System_IntPtr:
                case SpecialType.System_UIntPtr:
                    return StackValueKind.NativeInt;

                default:
                    // TODO: ByRef
                    return type.IsValueType ? StackValueKind.ValueType : StackValueKind.ObjRef;
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
                case StackValueKind.ObjRef:
                    return new LiteralExpression(null, ObjectType);
                case StackValueKind.NativeInt:
                    return new LiteralExpression(IntPtr.Zero, IntPtrType);
                default:
                    throw new NotImplementedException(); // Only use of float here so far, via brfalse/brtrue would be invalid IL.
            }
        }

        private static Exception Unreachable()
        {
            Debug.Fail("Code path expected to be unreachable.");
            return null;
        }

        private ITypeSymbol GetTypeFromToken(int token)
        {
            return (ITypeSymbol)GetSymbolFromToken(token);
        }

        private ITypeSymbol GetTypeFromHandle(EntityHandle handle)
        {
            return (ITypeSymbol)GetSymbolFromHandle(handle);
        }

        private ISymbol GetSymbolFromToken(int token)
        {
            return GetSymbolFromHandle(MetadataTokens.EntityHandle(token));
        }

        private ISymbol GetSymbolFromHandle(EntityHandle handle)
        {
            return _module.GetSymbolForMetadataHandle(handle);
        }

        private ILabelSymbol GetOrCreateLabel(BasicBlock block)
        {
            if (block.Label == null)
            {
                block.Label = new LabelSymbol(block.StartOffset, _method);
            }

            return block.Label;
        }

        private static ExceptionRegion[] GetExceptionRegions(MethodBodyBlock block)
        {
            var ilRegions = block.ExceptionRegions;
            var regions = new ExceptionRegion[ilRegions.Length];

            for (int i = 0; i < ilRegions.Length; i++)
            {
                regions[i] = new ExceptionRegion(ilRegions[i]);
            }

            return regions;
        }

        private ITypeSymbol BooleanType => _compilation.GetSpecialType(SpecialType.System_Boolean);
        private ITypeSymbol DoubleType => _compilation.GetSpecialType(SpecialType.System_Double);
        private ITypeSymbol Int32Type => _compilation.GetSpecialType(SpecialType.System_Int32);
        private ITypeSymbol Int64Type => _compilation.GetSpecialType(SpecialType.System_Int64);
        private ITypeSymbol IntPtrType => _compilation.GetSpecialType(SpecialType.System_IntPtr);
        private ITypeSymbol ObjectType => _compilation.GetSpecialType(SpecialType.System_Object);
        private ITypeSymbol StringType => _compilation.GetSpecialType(SpecialType.System_String);

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

        private sealed class ExceptionRegion
        {
            public ExceptionRegion(System.Reflection.Metadata.ExceptionRegion ilRegion)
            {
                ILRegion = ilRegion;
            }

            // required field
            public readonly System.Reflection.Metadata.ExceptionRegion ILRegion;

            // custom fields
            public ExceptionRegion InnerTry;
            public ExceptionRegion PreviousCatch;
            public int CatchCount;

            // shorthand for ILRegion.* for readability.
            public ExceptionRegionKind Kind => ILRegion.Kind;
            public int TryOffset => ILRegion.TryOffset;
            public int TryLength => ILRegion.TryLength;
            public int HandlerOffset => ILRegion.HandlerOffset;
            public int HandlerLength => ILRegion.HandlerLength;
            public EntityHandle CatchType => ILRegion.CatchType;

        }

        // for source compatibility with driver
        private static class ILExceptionRegionKind
        {
            public const ExceptionRegionKind Filter = ExceptionRegionKind.Filter;
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
            public int StatementStartIndex;
            public int StatementEndIndex;
            public ExceptionRegion OuterTry;
        }
    }
}
