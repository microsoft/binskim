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
            return new BlockStatement(statements.ToImmutable(), locals);
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
            var body = new BlockStatement(statements.ToImmutable());
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
                var local = GenerateTemporaryAsSymbol(type);
                var handler = ImportHandler(r);
                catches[--n] = new Catch(type, local, null, handler);
            }

            return catches.MoveToImmutable();
        }

        private IBlockStatement ImportHandler(ExceptionRegion region)
        {
            return ImportBlockStatement(region.HandlerOffset, region.HandlerLength);
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
                    _basicBlocks[region.HandlerOffset].EntryStack = new[]
                    {
                        GenerateTemporaryAsStackValue(localType)
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

            var value = _stack[--_stackTop];
            _stack[_stackTop] = default(StackValue);
            return value;
        }

        private void Append(IStatement statement)
        {
            // If the stack is not empty when we append a new statement, we effectively
            // save all of its contents to locals and reload them. This ensures that
            // we do not misrepresent the order-of-evaluation.
            if (_stackTop != 0)
            {
                TransferStack(ref _stack, _statements);
            }

            _statements.Add(statement);
        }

        private StackValue GenerateTemporaryAsStackValue(ITypeSymbol type)
        {
            return new StackValue(GenerateTemporaryAsReference(type));
        }

        private ILocalReferenceExpression GenerateTemporaryAsReference(ITypeSymbol type)
        {
            return new LocalReferenceExpression(GenerateTemporaryAsSymbol(type));
        }

        private ILocalSymbol GenerateTemporaryAsSymbol(ITypeSymbol type)
        {
            var local = new LocalSymbol($"TMP_{_locals.Count}", _method, type);
            _locals.Add(local);
            return local;
        }

        private void AppendTemporaryAssignment(ImmutableArray<IStatement>.Builder statements, StackValue target, StackValue source)
        {
            statements.Add(
                new ExpressionStatement(
                    new AssignmentExpression(
                        (ILocalReferenceExpression)target.Expression,
                        source.Expression)));
        }

        private void TransferStack(ref StackValue[] target, ImmutableArray<IStatement>.Builder statements)
        {
            Debug.Assert(_stackTop != 0);
            Debug.Assert(target != _stack);

            if (target == null || target == _stack)
            {
                target = target ?? new StackValue[_stackTop];

                for (int i = 0; i < _stackTop; i++)
                {
                    var source = _stack[i];
                    target[i] = GenerateTemporaryAsStackValue(source.Expression.ResultType);
                    AppendTemporaryAssignment(statements, target[i], source);
                }
            }
            else
            {
                for (int i = 0; i < target.Length; i++)
                {
                    var source = _stack[i];
                    var destination = target[i];

                    if (source.Kind != destination.Kind)
                    {
                        // error case: illegal to hit same block with differently typed operands on stack
                        throw new NotImplementedException();
                    }

                    if (source.Expression.ResultType != destination.Expression.ResultType)
                    {
                        // TODO: This can be legal: e.g. Call(cond ? new Foo() : new Bar());
                        //       Need to adjust local type to compatible type.
                        throw new NotImplementedException();
                    }

                    AppendTemporaryAssignment(statements, destination, source);
                }
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
            Push(
                new AddressOfExpression(
                    _compilation, 
                    GetVariableReference(index, argument)));
        }

        private void ImportDup()
        {
            var value = Pop();
            var localReference = GenerateTemporaryAsReference(value.Expression.ResultType);
            Push(value.Kind, new AssignmentExpression(localReference, value.Expression));
            Push(value.Kind, localReference);
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
            if (_stackTop != 0)
            {
                TransferStack(ref next.EntryStack, _statements);
                _stackTop = 0;
            }

            MarkBasicBlock(next);
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            throw new NotImplementedException();
        }

        private void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
            IStatement gotoStatement = new GoToStatement(GetOrCreateLabel(target));
            StackValue left, right;

            switch (opcode)
            {
                case ILOpcode.br:
                    Debug.Assert(fallthrough == null);
                    ImportFallthrough(target);
                    Append(gotoStatement);
                    return;

                case ILOpcode.brtrue:
                case ILOpcode.brfalse:
                    opcode = opcode == ILOpcode.brtrue ? ILOpcode.beq : ILOpcode.bne_un;
                    left = Pop();
                    right = new StackValue(left.Kind, GetZeroLiteral(left.Kind));
                    break;

                default:
                    right = Pop();
                    left = Pop();
                    break;
            }

            MarkBasicBlock(fallthrough);
            MarkBasicBlock(target);

            if (_stackTop != 0)
            {
                TransferStack(ref fallthrough.EntryStack, _statements);

                var gotoBlock = ImmutableArray.CreateBuilder<IStatement>(_stackTop + 1);
                TransferStack(ref target.EntryStack, gotoBlock);
                gotoBlock.Add(gotoStatement);
                gotoStatement = new BlockStatement(gotoBlock.MoveToImmutable());

                _stackTop = 0;
            }

            Append(
                new IfStatement(
                    new BinaryOperatorExpression(
                        GetBranchKind(opcode, GetStackKind(left.Kind, right.Kind)),
                        left.Expression,
                        right.Expression,
                        BooleanType),
                    gotoStatement));
        }

        private void ImportBinaryOperation(ILOpcode opcode)
        {
            var right = Pop();
            var left = Pop();
            var stackKind = GetStackKind(left.Kind, right.Kind);

            Push(
                stackKind,
                new BinaryOperatorExpression(
                    GetBinaryOperationKind(opcode, stackKind),
                    left.Expression,
                    right.Expression,
                    left.Expression.ResultType)); // TODO: handle heterogeneous types here.
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
            Push(PopFieldReference(token, isStatic));
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
            Push(
                new AddressOfExpression(
                    _compilation,
                    PopFieldReference(token, isStatic)));
        }

        private void ImportStoreField(int token, bool isStatic)
        {
            var value = Pop().Expression;
            var target = PopFieldReference(token, isStatic);

            Append(
                new ExpressionStatement(
                    new AssignmentExpression(
                        target,
                        value)));
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
            UnaryOperationKind unaryKind;
            var operand = Pop();

            switch (opCode)
            {
                case ILOpcode.not:
                    unaryKind = UnaryOperationKind.IntegerBitwiseNegation;
                    break;
                case ILOpcode.neg:
                    unaryKind = GetUnaryMinusKind(operand);
                    break;
                default:
                    throw Unreachable();
            }

            Push(
                operand.Kind,
                new UnaryOperatorExpression(
                    unaryKind,
                    operand.Expression,
                    operand.Expression.ResultType));
                   
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
            Push(
                new SizeOfExpression(
                    _compilation,
                    GetTypeFromToken(token)));
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
            // TODO?
        }

        private void ImportVolatilePrefix()
        {
            // TODO?
        }

        private void ImportTailPrefix()
        {
            // TODO?
        }

        private void ImportConstrainedPrefix(int token)
        {
            // TODO?
        }

        private void ImportNoPrefix(byte mask)
        {
            // TODO?
        }

        private void ImportReadOnlyPrefix()
        {
            // TODO?
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

        private FieldReferenceExpression PopFieldReference(int token, bool isStatic)
        {
            return new FieldReferenceExpression(
                isStatic ? null : Pop().Expression,
                (IFieldSymbol)GetSymbolFromToken(token));
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

        private BinaryOperationKind GetBinaryOperationKind(ILOpcode opcode, StackValueKind stackKind)
        {
            switch (opcode)
            {
                case ILOpcode.add_ovf:
                case ILOpcode.add_ovf_un:
                case ILOpcode.add:
                    return GetAddKind(stackKind);
                case ILOpcode.sub_ovf:
                case ILOpcode.sub_ovf_un:
                case ILOpcode.sub:
                    return GetSubtractKind(stackKind);
                case ILOpcode.mul_ovf:
                case ILOpcode.mul_ovf_un:
                case ILOpcode.mul:
                    return GetMultiplyKind(stackKind);
                case ILOpcode.div:
                case ILOpcode.div_un:
                    return GetDivideKind(stackKind);
                case ILOpcode.rem:
                case ILOpcode.rem_un:
                    return GetRemainderKind(stackKind);
                case ILOpcode.and:
                    return BinaryOperationKind.IntegerAnd;
                case ILOpcode.or:
                    return BinaryOperationKind.IntegerOr;
                case ILOpcode.xor:
                    return BinaryOperationKind.IntegerExclusiveOr;
                default:
                    throw Unreachable();
            }
        }

        private BinaryOperationKind GetAddKind(StackValueKind stackKind)
        {
            switch (stackKind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return BinaryOperationKind.IntegerAdd;
                case StackValueKind.Float:
                    return BinaryOperationKind.FloatingAdd;
                default:
                    throw new NotImplementedException();
            }
        }

        private BinaryOperationKind GetSubtractKind(StackValueKind stackKind)
        {
            switch (stackKind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return BinaryOperationKind.IntegerSubtract;
                case StackValueKind.Float:
                    return BinaryOperationKind.IntegerSubtract;
                default:
                    throw new NotImplementedException();
            }
        }

        private BinaryOperationKind GetMultiplyKind(StackValueKind stackKind)
        {
            switch (stackKind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return BinaryOperationKind.IntegerAdd;
                case StackValueKind.Float:
                    return BinaryOperationKind.FloatingAdd;
                default:
                    throw new NotImplementedException();
            }
        }

        private BinaryOperationKind GetDivideKind(StackValueKind stackKind)
        {
            switch (stackKind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return BinaryOperationKind.IntegerDivide;
                case StackValueKind.Float:
                    return BinaryOperationKind.FloatingDivide;
                default:
                    throw new NotImplementedException();
            }
        }

        private BinaryOperationKind GetRemainderKind(StackValueKind stackKind)
        {
            switch (stackKind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return BinaryOperationKind.IntegerRemainder;
                case StackValueKind.Float:
                    return BinaryOperationKind.FloatingRemainder;
                default:
                    throw new NotImplementedException();
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

        private static UnaryOperationKind GetUnaryMinusKind(StackValue operand)
        {
            switch (operand.Kind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return UnaryOperationKind.IntegerMinus;
                case StackValueKind.Float:
                    return UnaryOperationKind.FloatingMinus;
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

        private static StackValueKind GetStackKind(ITypeSymbol type)
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
            }

            if (type.IsReferenceType)
            {
                return StackValueKind.ObjRef;
            }

            if (type.IsValueType)
            {
                switch (type.TypeKind)
                {
                    case TypeKind.Pointer:
                        return StackValueKind.NativeInt;
                    case TypeKind.Enum:
                        return GetStackKind(((INamedTypeSymbol)type).EnumUnderlyingType);
                }

                return StackValueKind.ValueType;
            }

            // error or unconstrained type parameter
            return StackValueKind.Unknown;
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
            var symbol =  _module.GetSymbolForMetadataHandle(handle);

            if (symbol == null)
            {
                throw new NotImplementedException(); // TODO: failed resolution can trigger this. need error placeholder.
            }

            return symbol;
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
            public StackValue(IExpression expression)
            {
                Kind = GetStackKind(expression.ResultType);
                Expression = expression;
            }

            public StackValue(StackValueKind kind, IExpression expression)
            {
                Kind = kind;
                Expression = expression;
            }

            public readonly IExpression Expression;
            public readonly StackValueKind Kind;

            public override string ToString()
            {
                return Expression?.ToString() ?? "(null)";
            }
        }

        private sealed class BasicBlock
        {
            // Required fields
            public BasicBlock Next;
            public int StartOffset;
            public int EndOffset;
            public StackValue[] EntryStack;
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
