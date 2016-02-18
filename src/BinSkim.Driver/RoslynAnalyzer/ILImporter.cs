// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Decoding;
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
        private ImmutableArray<IOperation>.Builder _statements;
        private ImmutableArray<ILocalSymbol>.Builder _locals;
        private MetadataReader _reader;
        private ILSignatureProvider _signatureProvider;
        private StandaloneSignatureHandle _localSignatureHandle;

        public ILImporter(Compilation compilation, MetadataReader reader, IMethodSymbol method, MethodBodyBlock body)
        {
            _compilation = compilation;
            _reader = reader;
            _method = method;
            _ilBytes = body.GetILBytes();
            _signatureProvider = new ILSignatureProvider(compilation, method);
            _localSignatureHandle = body.LocalSignature;
            _exceptionRegions = GetExceptionRegions(body);
            _statements = ImmutableArray.CreateBuilder<IOperation>();
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

            var statements = ImmutableArray.CreateBuilder<IOperation>();
            ImportStatements(ilStartOffset, length, statements);
            return new BlockStatement(statements.ToImmutable(), locals);
        }

        private void ImportStatements(
            int ilStartOffset,
            int length,
            ImmutableArray<IOperation>.Builder statements)
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
            var statements = ImmutableArray.CreateBuilder<IOperation>();
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

            switch (tryRegion.Kind)
            {
                case ExceptionRegionKind.Catch:
                case ExceptionRegionKind.Filter:
                    return new TryCatchStatement(body, ImportCatches(tryRegion));

                case ExceptionRegionKind.Finally:
                    return new TryFinallyStatement(body, ImportFaultOrFinally(tryRegion));

                case ExceptionRegionKind.Fault:
                    return new TryFaultStatement(body, ImportFaultOrFinally(tryRegion));

                default:
                    throw Unreachable();
            }
        }

        private ImmutableArray<ICatchClause> ImportCatches(ExceptionRegion region)
        {
            Debug.Assert(region.IsCatchOrFilter);

            int n = region.CatchCount;
            var catches = ImmutableArray.CreateBuilder<ICatchClause>(n);
            catches.Count = n;

            for (var r = region; r != null; r = r.PreviousCatch)
            {
                var local = ((ILocalReferenceExpression)_basicBlocks[r.HandlerOffset].EntryStack[0].Expression).Local;
                var handler = ImportHandler(r);
                var filter = ImportFilter(r);

                catches[--n] = new CatchClause(local.Type, local, filter, handler);
            }

            return catches.MoveToImmutable();
        }

        private IBlockStatement ImportFaultOrFinally(ExceptionRegion region)
        {
            Debug.Assert(region.IsFaultOrFinally);

            var statements = ImmutableArray.CreateBuilder<IOperation>();
            ImportStatements(region.HandlerOffset, region.HandlerLength, statements);

            // lazily used for uncommon case of an endfinally elsewhere than the lexical end of finally block,
            // which will raise as a goto to the end of the block. The last one is just removed.
            ILabelSymbol endLabel = null;
            IOperation gotoEnd = null;

            int n = statements.Count;
            if (n == 0 || statements[n - 1] != EndFinally.Instance)
            {
                // todo: error case, can't end finally block with something other than endfinally.
                throw new NotImplementedException();
            }

            for (int i = 0; i < n - 1; i++)
            {
                if (statements[i] == EndFinally.Instance)
                {
                    if (gotoEnd == null)
                    {
                        // note that this label is not the same as GetOrCreateLabel(region.HandlerOffset + region.HandlerLength). The former
                        // is logically outside of the fault or finally block while this one is inside.
                        endLabel = new LabelSymbol(region.HandlerOffset + region.HandlerLength, _method, "IL_EH");
                        gotoEnd = new BranchStatement(endLabel);
                    }

                    statements[i] = gotoEnd;
                }
            }

            if (endLabel != null)
            {
                statements[n - 1] = new LabelStatement(endLabel);
            }
            else
            {
                statements.RemoveAt(n - 1);
            }

            return new BlockStatement(statements.ToImmutable());
        }

        private IOperation ImportFilter(ExceptionRegion region)
        {
            if (!region.IsFilter)
            {
                return null;
            }

            var statements = ImmutableArray.CreateBuilder<IOperation>();

            ImportStatements(region.FilterOffset, region.FilterLength, statements);

            int n = statements.Count;
            var endFilter = n > 0 ? statements[n - 1] as EndFilter : null;
            if (endFilter == null)
            {
                // todo: error case -- must end with endfilter (we should also flag improper use of endfilter elsewhere).
                throw new NotImplementedException(); 
            }

            if (n > 1)
            {
                statements[n - 1] = endFilter.Expression;

                // This case should be uncommon (filter that we couldn't represent as a single expression),
                // but unoptimized C# output has side-effects like stloc/ldloc. We'd have to optimize these
                // away to get back to a single expression.
                return new BlockStatement(statements.ToImmutable());
            }

            return endFilter.Expression;
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

                    if (region.TryLength == outerTry.TryLength && region.IsCatchOrFilter && outerTry.IsCatchOrFilter)
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

                switch (region.Kind)
                {
                    case ExceptionRegionKind.Catch:
                        var catchLocal = GenerateTemporaryAsStackValue(GetTypeFromHandle(region.CatchType));
                        _basicBlocks[region.HandlerOffset].EntryStack = new[] { catchLocal };
                        break;

                    case ExceptionRegionKind.Filter:
                        var filterLocal = GenerateTemporaryAsStackValue(ObjectType);
                        _basicBlocks[region.FilterOffset].EntryStack = new[] { filterLocal };
                        _basicBlocks[region.HandlerOffset].EntryStack = new[] { filterLocal };
                        break;
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

            var signature = _reader.GetStandaloneSignature(_localSignatureHandle);
            var localTypes = signature.DecodeLocalSignature(_signatureProvider);
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

        private void Push(StackValueKind kind, IOperation expression)
        {
            Push(new StackValue(kind, expression));
        }

        private void Push(IOperation expression)
        {
            Push(GetStackKind(expression.Type), expression);
        }

        private StackValue Pop()
        {
            if (_stackTop == 0)
            {
                // todo: error case: stack underflow
                throw new NotImplementedException();
            }

            var value = _stack[--_stackTop];
            _stack[_stackTop] = default(StackValue);
            return value;
        }

        // managed pointer types do not exist in the Roslyn type system. We therefore convert
        // to unmanaged pointers to perform comparison or arithmetic on byrefs. This 
        // unfortunately gives the wrong impression that GC tracking is abandoned, but it is
        // the closest thing we can represent. 
        private StackValue PopWithByRefAsPointer()
        {
            var value = Pop();
            if (value.Kind == StackValueKind.ByRef)
            {
                value = new StackValue(
                    StackValueKind.NativeInt,
                    new AddressOfExpression(_compilation, (IReferenceExpression)value.Expression));
            }

            return value;
        }

        private void Append(IOperation statement)
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


        private StackValue GenerateTemporaryAsStackValue(StackValue value)
        {
            return new StackValue(GenerateTemporaryAsReference(value));
        }

        private StackValue GenerateTemporaryAsStackValue(ITypeSymbol type)
        {
            return new StackValue(GenerateTemporaryAsReference(type));
        }

        private ILocalReferenceExpression GenerateTemporaryAsReference(StackValue value)
        {
            if (value.Kind == StackValueKind.ByRef)
            {
                // TODO: proper by ref local
                return GenerateTemporaryAsReference(_compilation.CreatePointerTypeSymbol(value.Expression.Type));
            }

            return GenerateTemporaryAsReference(value.Expression.Type);
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

        private void AppendTemporaryAssignment(ImmutableArray<IOperation>.Builder statements, StackValue target, StackValue source)
        {
            statements.Add(new AssignmentExpression((ILocalReferenceExpression)target.Expression, source.Expression));
        }

        private void TransferStack(ref StackValue[] target, ImmutableArray<IOperation>.Builder statements)
        {
            Debug.Assert(_stackTop != 0);

            if (target == null || target == _stack)
            {
                target = target ?? new StackValue[_stackTop];

                for (int i = 0; i < _stackTop; i++)
                {
                    var source = _stack[i];
                    target[i] = GenerateTemporaryAsStackValue(source);
                    AppendTemporaryAssignment(statements, target[i], source);
                }
            }
            else
            {
                for (int i = 0; i < target.Length; i++)
                {
                    var source = _stack[i];
                    var destination = target[i];

                    if (source.Kind == StackValueKind.ByRef && destination.Kind == StackValueKind.NativeInt)
                    {
                        // TODO: consequence of not having by-ref locals yet
                        source = new StackValue(StackValueKind.NativeInt, new AddressOfExpression(_compilation, (IReferenceExpression)source.Expression));
                    }
                    else if (source.Kind != destination.Kind)
                    {
                        // error case: illegal to hit same block with differently typed operands on stack
                        throw new NotImplementedException();
                    }

                    if (source.Expression.Type != destination.Expression.Type)
                    {
                        // TODO: This is legal: e.g. Call(cond ? new Foo() : new Bar());
                        //       Need to adjust local type to compatible type. For now, just leave local with bad type.
                    }

                    AppendTemporaryAssignment(statements, destination, source);
                }
            }
        }

        private void TransferStack(BasicBlock target, ref IOperation gotoStatement)
        {
            Debug.Assert(_stackTop != 0);

            var gotoBlock = ImmutableArray.CreateBuilder<IOperation>(_stackTop + 1);
            TransferStack(ref target.EntryStack, gotoBlock);
            gotoBlock.Add(gotoStatement);
            gotoStatement = new BlockStatement(gotoBlock.MoveToImmutable());
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
                    MarkHandlerBlocks(c);
                }
                MarkHandlerBlocks(t);
            }
        }

        private void MarkHandlerBlocks(ExceptionRegion region)
        {
            if (region.IsFilter)
            {
                MarkBasicBlock(_basicBlocks[region.FilterOffset]);
            }

            MarkBasicBlock(_basicBlocks[region.HandlerOffset]);
        }

        private void ImportNop()
        {
        }

        private void ImportBreak()
        {
            Append(new DebugBreakStatement());
        }

        private void ImportLoadVar(int index, bool argument)
        {
            Push(GetVariableReferenceAsStackValue(index, argument));
        }

        private void ImportStoreVar(int index, bool argument)
        {
            var target = GetVariableReference(index, argument);
            var value = Pop().Expression;

            Append(new AssignmentExpression(target, value));
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
            Push(
                StackValueKind.ByRef,
                GetVariableReference(index, argument));
        }

        private void ImportDup()
        {
            var value = Pop();

            if (value.Expression is IReferenceExpression)
            {
                Push(value);
                Push(value);
            }
            else
            {
                var localReference = GenerateTemporaryAsReference(value);

                Push(value.Kind, new AssignmentExpression(localReference, value.Expression));
                Push(value.Kind, localReference);
            }
        }

        private void ImportPop()
        {
            Append(Pop().Expression);
        }

        private void ImportJmp(int token)
        {
            if (_stackTop != 0)
            {
                throw new NotImplementedException(); // error case
            }

            Append(new JumpStatement((IMethodSymbol)GetSymbolFromToken(token)));
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            // unsigned argument deliberately unused: it is captured in wellKnownType.
            // FEEDBACK: How to represent checkOverflow = true?

            ImportCasting(ConversionKind.Cast, GetWellKnownType(wellKnownType));
        }

        private void ImportBox(int token)
        {
            ImportCasting(ConversionKind.Cast, ObjectType);
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
            if (opCode == ILOpcode.unbox)
            {
                Push(
                    StackValueKind.ByRef,
                    new UnboxExpression(Pop().Expression, GetTypeFromToken(token)));
            }
            else
            {
                Debug.Assert(opCode == ILOpcode.unbox_any);
                ImportCasting(ConversionKind.Cast, GetTypeFromToken(token));
            }
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
            Debug.Assert(opcode == ILOpcode.castclass || opcode == ILOpcode.isinst);

            ImportCasting(
                opcode == ILOpcode.castclass ? ConversionKind.Cast : ConversionKind.TryCast,
                GetTypeFromToken(token));
        }

        private void ImportCasting(ConversionKind conversionKind, ITypeSymbol type)
        {
            var operand = Pop().Expression;

            if (conversionKind == ConversionKind.TryCast && type.IsValueType)
            {
                Push(new ValueTypeAsExpression(type, operand));
            }
            else
            {
                Push(new ConversionExpression(conversionKind, operand, type));
            }
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
            if (opcode == ILOpcode.calli)
            {
                ImportCallIndirect(token);
            }
            else
            {
                ImportCallDirect(opcode, token);
            }
        }

        private void ImportCallIndirect(int token)
        {
            var signature = _reader.GetStandaloneSignature(MetadataTokens.StandaloneSignatureHandle(token));
            var sigReader = _reader.GetBlobReader(signature.Signature);
            var header = sigReader.ReadSignatureHeader();

            if (header.Kind != SignatureKind.Method || header.IsGeneric)
            {
                throw new NotImplementedException(); // error: bad calli signature
            }

            int argumentCount = sigReader.ReadCompressedInteger();
            if (header.IsInstance && !header.HasExplicitThis)
            {
                argumentCount++;
            }

            var decoder = new SignatureDecoder<ITypeSymbol>(_signatureProvider, _reader);
            var returnType = decoder.DecodeType(ref sigReader);
            var arguments = ImmutableArray.CreateBuilder<IOperation>(argumentCount);
            arguments.Count = argumentCount;

            var functionPointer = Pop().Expression;
            for (int i = argumentCount - 1; i >= 0; i--)
            {
                arguments[i] = Pop().Expression;
            }

            var invocation = new IndirectInvocationExpression(
                header.CallingConvention,
                functionPointer,
                returnType,
                arguments.MoveToImmutable());

            if (returnType.SpecialType == SpecialType.System_Void)
            {
                Append(invocation);
            }
            else
            {
                Push(invocation);
            }
        }

        private void ImportCallDirect(ILOpcode opcode, int token)
        {
            var callee = (IMethodSymbol)GetSymbolFromToken(token);
            var arguments = PopArguments(callee, token);

            switch (opcode)
            {
                case ILOpcode.call:
                case ILOpcode.callvirt:
                    var isVirtual = opcode == ILOpcode.callvirt;
                    var instance = callee.IsStatic ? null : Pop().Expression;
                    var invocation = new InvocationExpression(isVirtual, instance, callee, arguments);

                    if (callee.ReturnsVoid)
                    {
                        Append(invocation);
                    }
                    else
                    {
                        Push(invocation);
                    }
                    break;

                case ILOpcode.newobj:
                    Push(new ObjectCreationExpression(callee, arguments));
                    break;

                default:
                    throw Unreachable();
            }
        }

        private ImmutableArray<IArgument> PopArguments(IMethodSymbol callee, int token)
        {
            var parameters = callee.Parameters;

            int parameterCount = parameters.Length;
            int argumentCount = parameterCount;

            if (callee.IsVararg)
            {
                var memberRef = _reader.GetMemberReference(MetadataTokens.MemberReferenceHandle(token));
                var sigReader = _reader.GetBlobReader(memberRef.Signature);
                var header = sigReader.ReadSignatureHeader();
                Debug.Assert(header.CallingConvention == SignatureCallingConvention.VarArgs);

                if (header.IsGeneric)
                {
                    sigReader.ReadCompressedInteger(); // skip generic parameter count
                }

                argumentCount = sigReader.ReadCompressedInteger();
                Debug.Assert(argumentCount >= parameters.Length);
            }

            var args = ImmutableArray.CreateBuilder<IArgument>(argumentCount);
            args.Count = argumentCount;

            int i;
            for (i = argumentCount - 1; i >= parameterCount; i--)
            {
                // TODO/FEEDBACK: It's unclear how we're supposed to represent vararg arguments.
                //                __arglist calls don't seem to surface at all to IOperation analyzers.
                //                For now, just use a positional argument with a null parameter symbol.
                Debug.Assert(callee.IsVararg);
                args[i] = new Argument(null, Pop().Expression);
            }

            for (; i >= 0; i--)
            {
                args[i] = new Argument(parameters[i], Pop().Expression);
            }

            return args.MoveToImmutable();
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
            Push(
                new LoadFunctionExpression(
                    (IMethodSymbol)GetSymbolFromToken(token), 
                    opCode == ILOpcode.ldvirtftn, 
                    _compilation));
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
            var value = Pop().Expression;

            var cases = ImmutableArray.CreateBuilder<ISwitchCase>(jmpDelta.Length);

            if (_stackTop != 0)
            {
                TransferStack(ref _stack, _statements);
            }

            MarkBasicBlock(fallthrough);

            for (int i = 0; i < jmpDelta.Length; i++)
            {
                var target = _basicBlocks[jmpBase + jmpDelta[i]];
                IOperation gotoStatement = new BranchStatement(GetOrCreateLabel(target));

                if (_stackTop != 0)
                {
                    TransferStack(target, ref gotoStatement);
                }

                cases.Add(
                    new SwitchCase(
                        new LiteralExpression(i, Int32Type),
                        gotoStatement));

                MarkBasicBlock(target);
            }

            _stackTop = 0;

            Append(new SwitchStatement(value, cases.MoveToImmutable()));
        }

        private void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
            IOperation gotoStatement = new BranchStatement(GetOrCreateLabel(target));
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
                    left = PopWithByRefAsPointer();
                    right = new StackValue(left.Kind, GetZeroLiteral(left.Kind));
                    break;

                default:
                    right = PopWithByRefAsPointer();
                    left = PopWithByRefAsPointer();
                    break;
            }

            MarkBasicBlock(fallthrough);
            MarkBasicBlock(target);

            if (_stackTop != 0)
            {
                TransferStack(ref fallthrough.EntryStack, _statements);
                TransferStack(target, ref gotoStatement);
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
            var right = PopWithByRefAsPointer();
            var left = PopWithByRefAsPointer();

            var stackKind = GetStackKind(left.Kind, right.Kind);

            Push(
                stackKind,
                new BinaryOperatorExpression(
                    GetBinaryOperationKind(opcode, stackKind),
                    left.Expression,
                    right.Expression,
                    GetTypeForStackKind(stackKind))); // TODO: We should be more precise than stack kind -> type.
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
                    left.Expression.Type));
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
            var right = PopWithByRefAsPointer();
            var left = PopWithByRefAsPointer();

            var kind = GetCompareKind(opcode, GetStackKind(left.Kind, right.Kind));

            Push(
                new BinaryOperatorExpression(
                    kind,
                    left.Expression,
                    right.Expression,
                    BooleanType));
        }

        private void ImportLoadField(int token, bool isStatic)
        {
            Push(PopFieldReference(token, isStatic));
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
            Push(
                StackValueKind.ByRef,
                PopFieldReference(token, isStatic));
        }

        private void ImportStoreField(int token, bool isStatic)
        {
            var value = Pop().Expression;
            var target = PopFieldReference(token, isStatic);

            Append(new AssignmentExpression(target, value));
        }

        private void ImportLoadIndirect(int token)
        {
            ImportLoadIndirect(GetTypeFromToken(token));
        }

        private void ImportLoadIndirect(ITypeSymbol type)
        {
            Push(PopIndirectionReference(type));
        }

        private void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(GetTypeFromToken(token));
        }

        private void ImportStoreIndirect(ITypeSymbol type)
        {
            var value = Pop().Expression;
            var target = PopIndirectionReference(type);

            Append(new AssignmentExpression(target, value));
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
            var type = GetTypeFromToken(token);
            var value = new DefaultValueExpression(type);
            var target = PopIndirectionReference(type);
            Append(new AssignmentExpression(target, value));
        }

        private void ImportLeave(BasicBlock target)
        {
            MarkBasicBlock(target);
            Append(new BranchStatement(GetOrCreateLabel(target)));

            // Append will have flushed any extra values on stack to temporaries.
            // Leave semantics empties the evaluation stack, so we don't want to keep those temporaries reloaded here.
            _stackTop = 0; 
        }

        private void ImportNewArray(int token)
        {
            Push(
                new ArrayCreationExpression(
                    _compilation,
                    GetTypeFromToken(token),
                    Pop().Expression));
        }

        private void ImportLoadElement(int token)
        {
            ImportLoadElement(GetTypeFromToken(token));
        }

        private void ImportLoadElement(ITypeSymbol type)
        {
            Push(PopArrayReference(Pop().Expression, type));
        }

        private void ImportStoreElement(int token)
        {
            ImportStoreElement(GetTypeFromToken(token));
        }

        private void ImportStoreElement(ITypeSymbol type)
        {
            var value = Pop().Expression;
            var index = Pop().Expression;
            var target = PopArrayReference(index, type);

            Append(new AssignmentExpression(target, value));
        }

        private void ImportAddressOfElement(int token)
        {
            ImportAddressOfElement(GetTypeFromToken(token));
        }

        private void ImportAddressOfElement(ITypeSymbol type)
        {
            var index = Pop().Expression;
            var arrayReference = PopArrayReference(index, type);

            Push(StackValueKind.ByRef, arrayReference);
        }

        private void ImportLoadLength()
        {
            Push(new ArrayLengthExpression(Pop().Expression, _compilation));
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
                    operand.Expression.Type));
        }

        private void ImportCpOpj(int token)
        {
            var type = GetTypeFromToken(token);
            var src = PopIndirectionReference(type);
            var dst = PopIndirectionReference(type);

            Append(new AssignmentExpression(dst, src));
        }

        private void ImportRefAnyVal(int token)
        {
            var type = GetTypeFromToken(token);
            var typedRef = Pop().Expression;

            Push(
                StackValueKind.ByRef,
                new RefValueExpression(typedRef, type));
        }

        private void ImportCkFinite()
        {
            Push(new CheckFiniteExpression(Pop().Expression));
        }

        private void ImportMkRefAny(int token)
        {
            var type = GetTypeFromToken(token);
            var pointer = Pop().Expression;

            Push(new MakeRefExpression(type, pointer, _compilation));
        }

        private void ImportLdToken(int token)
        {
            Push(new LoadTokenExpression(GetSymbolFromToken(token), _compilation));
        }

        private void ImportLocalAlloc()
        {
            Push(new LocalAllocationExpression(Pop().Expression, _compilation));
        }

        private void ImportEndFinally()
        {
            Append(EndFinally.Instance);

            // Append will have flushed any extra values on stack to temporaries.
            // Endfinally semantics empties the evaluation stack, so we don't want to keep those temporaries reloaded here.
            _stackTop = 0;
        }

        private void ImportEndFilter()
        {
            Append(new EndFilter(Pop().Expression));

            if (_stackTop != 0)
            {
                // TODO: error case.
                throw new NotImplementedException();
            }
        }

        private void ImportCpBlk()
        {
            var byteCount = Pop().Expression;
            var srcPointer = Pop().Expression;
            var dstPointer = Pop().Expression;

            Append(new CopyBlockStatement(srcPointer, dstPointer, byteCount));
        }

        private void ImportInitBlk()
        {
            var byteCount = Pop().Expression;
            var value = Pop().Expression;
            var pointer = Pop().Expression;

            Append(new InitializeBlockStatement(pointer, value, byteCount));
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
            Push(new RefTypeExpression(Pop().Expression, _compilation));
        }

        private void ImportArgList()
        {
            Push(new ArgumentListExpression(_compilation));
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

        private StackValue GetVariableReferenceAsStackValue(int index, bool argument)
        {
            RefKind refKind;
            var reference = GetVariableReference(index, argument, out refKind);
            var stackKind = refKind == RefKind.None ? GetStackKind(reference.Type) : StackValueKind.ByRef;
            return new StackValue(stackKind, reference);
        }

        private IReferenceExpression GetVariableReference(int index, bool argument)
        {
            RefKind _;
            return GetVariableReference(index, argument, out _);
        }

        private IReferenceExpression GetVariableReference(int index, bool argument, out RefKind refKind)
        {
            refKind = RefKind.None;

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
                if (_method.ContainingType.IsValueType)
                {
                    refKind = RefKind.Ref;
                }
                return new InstanceReferenceExpression(_method);
            }

            var parameter = _method.Parameters[index];
            refKind = parameter.RefKind;
            return new ParameterReferenceExpression(parameter);
        }

        private FieldReferenceExpression PopFieldReference(int token, bool isStatic)
        {
            return new FieldReferenceExpression(
                isStatic ? null : Pop().Expression,
                (IFieldSymbol)GetSymbolFromToken(token));
        }

        private ArrayElementReferenceExpression PopArrayReference(IOperation index, ITypeSymbol type)
        {
            var arrayReference = Pop().Expression;

            if (type == null)
            {
                var arrayType = arrayReference.Type as IArrayTypeSymbol;
                type = arrayType != null ? arrayType.ElementType : ObjectType;
            }

            return new ArrayElementReferenceExpression(arrayReference, index, type);
        }

        private IReferenceExpression PopIndirectionReference(ITypeSymbol type)
        {
            var value = Pop();
            var expression = value.Expression;

            // TODO: Is it OK that we just adjust the result type here or should we have explicit
            // pointer conversions? Doing so would create another situation where we degrade managed
            // pointers to native pointers unnecessarily.

            switch (value.Kind)
            {
                case StackValueKind.ByRef:
                    return ((ReferenceExpression)expression).WithType(type);

                case StackValueKind.NativeInt:
                    return new PointerIndirectionReferenceExpression(expression, type ?? expression.Type);

                default:
                    throw new NotImplementedException(); // error.
            }
        }

        private static BinaryOperationKind GetCompareKind(ILOpcode opcode, StackValueKind kind)
        {
            Debug.Assert(kind != StackValueKind.ByRef); // we should have coerced to native int already

            switch (kind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return GetIntegerCompareKind(opcode);
                case StackValueKind.Float:
                    return GetFloatCompareKind(opcode);
                case StackValueKind.ObjRef:
                    return GetObjectCompareKind(opcode);
                default:
                    throw new NotImplementedException(); // should byref compares be integer compares?
            }
        }

        private static BinaryOperationKind GetIntegerCompareKind(ILOpcode opcode)
        {
            switch (opcode)
            {
                case ILOpcode.ceq:
                    return BinaryOperationKind.IntegerEquals;
                case ILOpcode.cgt:
                    return BinaryOperationKind.IntegerGreaterThan;
                case ILOpcode.cgt_un:
                    return BinaryOperationKind.UnsignedGreaterThan;
                case ILOpcode.clt:
                    return BinaryOperationKind.IntegerLessThan;
                case ILOpcode.clt_un:
                    return BinaryOperationKind.UnsignedLessThan;
                default:
                    throw Unreachable();
            }
        }

        private static BinaryOperationKind GetFloatCompareKind(ILOpcode opcode)
        {
            switch (opcode)
            {
                case ILOpcode.ceq:
                    return BinaryOperationKind.FloatingEquals;
                case ILOpcode.cgt:
                    return BinaryOperationKind.FloatingGreaterThan;
                case ILOpcode.clt:
                    return BinaryOperationKind.FloatingGreaterThan;

                // TODO/FEEDBACK: No "unordered" floating comparison in IOperation
                case ILOpcode.cgt_un:
                    goto case ILOpcode.cgt; // TODO
                case ILOpcode.clt_un:
                    goto case ILOpcode.clt; // TODO

                default:
                    throw Unreachable();
            }
        }

        private static BinaryOperationKind GetObjectCompareKind(ILOpcode opcode)
        {
            switch (opcode)
            {
                case ILOpcode.ceq:
                    return BinaryOperationKind.ObjectEquals;
                case ILOpcode.cgt_un:
                    // NOTE: cgt.un is allowed on objects for != null since there is no cne. No other comparisons
                    // are valid on obj refs. We therefore just assume rhs is null here and represent as
                    // ObjectNotEquals. The result is arbitrary in any other case anyhow because the GC
                    // can move object references at will.
                    return BinaryOperationKind.ObjectNotEquals;
                case ILOpcode.clt:
                case ILOpcode.clt_un:
                case ILOpcode.cgt:
                    throw new NotImplementedException(); //todo - error cases
                default:
                    throw Unreachable();
            }
        }

        private static BinaryOperationKind GetBranchKind(ILOpcode opcode, StackValueKind kind)
        {
            Debug.Assert(kind != StackValueKind.ByRef); // we should have coerced to native int already

            switch (kind)
            {
                case StackValueKind.Int32:
                case StackValueKind.Int64:
                case StackValueKind.NativeInt:
                    return GetIntegerBranchKind(opcode);
                case StackValueKind.Float:
                    return GetFloatBranchKind(opcode);
                case StackValueKind.Unknown: // type parameter
                case StackValueKind.ObjRef:
                    return GetObjectBranchKind(opcode);
                default:
                    throw new NotImplementedException(); // error
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

                // TODO/FEEDBACK: No "unordered" floating comparison in IOperation
                case ILOpcode.bge_un:
                    goto case ILOpcode.bge; // TODO
                case ILOpcode.bgt_un:
                    goto case ILOpcode.bgt; // TODO
                case ILOpcode.ble_un:
                    goto case ILOpcode.ble; // TODO
                case ILOpcode.blt_un:
                    goto case ILOpcode.blt; // TODO

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
                    return BinaryOperationKind.FloatingSubtract;
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
                case SpecialType.System_Boolean:
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
                case StackValueKind.Unknown: // Generic Type Parameter can be compared to null.
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
            return _method.ContainingModule.GetSymbolForMetadataHandle(handle, _method)
                ?? GetUnresolvedSymbolFromHandle(handle);
        }

        private ISymbol GetUnresolvedSymbolFromHandle(EntityHandle handle)
        {
            if (handle.Kind == HandleKind.MemberReference)
            {
                var memberRef = _reader.GetMemberReference((MemberReferenceHandle)handle);
                var arrayType = GetSymbolFromHandle(memberRef.Parent) as IArrayTypeSymbol;
                if (arrayType != null)
                {
                    return GetArrayMethod(memberRef.Name, arrayType);
                }
            }

            // TODO: Handle more than just special array methods. Unresolved references should not throw.
            throw new NotImplementedException();
        }

        private IMethodSymbol GetArrayMethod(StringHandle name, IArrayTypeSymbol arrayType)
        {
            // TODO: Decode parameter types to make sure they match.

            if (_reader.StringComparer.Equals(name, "Get"))
            {
                return ArrayMethodSymbol.Get(arrayType, _compilation);
            }
            else if (_reader.StringComparer.Equals(name, "Set"))
            {
                return ArrayMethodSymbol.Set(arrayType, _compilation);
            }
            else if (_reader.StringComparer.Equals(name, ".ctor"))
            {
                return ArrayMethodSymbol.Ctor(arrayType, _compilation);
            }
            else
            {
                // error
                throw new NotImplementedException();
            }
        }

        private ITypeSymbol GetTypeForStackKind(StackValueKind stackKind)
        {
            switch (stackKind)
            {
                case StackValueKind.Float:
                    return DoubleType;
                case StackValueKind.Int32:
                    return Int32Type;
                case StackValueKind.Int64:
                    return Int64Type;
                case StackValueKind.NativeInt:
                    return IntPtrType;
                case StackValueKind.ObjRef:
                    return ObjectType;
                case StackValueKind.Unknown:
                case StackValueKind.ByRef:
                case StackValueKind.ValueType:
                default:
                    // should not attempt to get a type symbol for these.
                    throw Unreachable();
            }
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

            // shorthand properties for readability
            public ExceptionRegionKind Kind => ILRegion.Kind;
            public int TryOffset => ILRegion.TryOffset;
            public int TryLength => ILRegion.TryLength;
            public int FilterOffset => ILRegion.FilterOffset;
            public int FilterLength => ILRegion.HandlerOffset - ILRegion.FilterOffset;
            public int HandlerOffset => ILRegion.HandlerOffset;
            public int HandlerLength => ILRegion.HandlerLength;
            public int HandlerEndOffset => HandlerOffset + HandlerLength - 1;
            public EntityHandle CatchType => ILRegion.CatchType;
            public bool IsCatchOrFilter => Kind <= ExceptionRegionKind.Filter;
            public bool IsFaultOrFinally => Kind >= ExceptionRegionKind.Finally;
            public bool IsFilter => Kind == ExceptionRegionKind.Filter;
        }

        // for source compatibility with driver
        private static class ILExceptionRegionKind
        {
            public const ExceptionRegionKind Filter = ExceptionRegionKind.Filter;
        }

        private struct StackValue
        {
            public StackValue(IOperation expression)
            {
                Kind = GetStackKind(expression.Type);
                Expression = expression;
            }

            public StackValue(StackValueKind kind, IOperation expression)
            {
                Kind = kind;
                Expression = expression;
            }

            public readonly IOperation Expression;
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
