// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;

using Microsoft.CodeAnalysis.Semantics;

namespace Microsoft.CodeAnalysis.IL
{
    // Nodes for operations that that aren't publicly supported by IOperation.
    //
    // Some of them may be raisable to higher level constructs that are modeled by IOperation (TODO), 
    // while others can happen in IL and have no IOperation equivalent.


    internal interface ICustomOperation : IOperation
    {
        void CustomWalk(OperationWalker walker);
    }

    internal abstract class CustomOperation : Operation, ICustomOperation
    {
        public override OperationKind Kind => OperationKind.None;

        public override void Accept(OperationVisitor visitor)
        {
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return default(TResult);
        }

        public abstract void CustomWalk(OperationWalker walker);
    }

    internal abstract class CustomExpression : CustomOperation, ICustomOperation
    {
        public abstract override ITypeSymbol Type { get; }
    }

    // temporary node to hold an endfilter operation. replaced appropriately when exception blocks are built.
    internal sealed class EndFilter : CustomOperation
    {
        public EndFilter(IOperation expression)
        {
            Expression = expression;
        }

        public IOperation Expression { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Expression);
        }
    }

    // temporary node to mark end of filter. replaced appropriately when exception blocks are built.
    internal sealed class EndFinally : CustomOperation
    {
        private EndFinally() { }

        public static readonly EndFinally Instance = new EndFinally();
        public override OperationKind Kind => OperationKind.None;

        public override void CustomWalk(OperationWalker walker)
        {
            // no children
        }
    }

    internal sealed class TryFaultStatement : TryStatement, ICustomOperation
    {
        public TryFaultStatement(IBlockStatement body, IBlockStatement faultHandler)
            : base(body)
        {
            FaultHandler = faultHandler;
        }

        // FEEDBACK: This is not exposed publicly
        public IBlockStatement FaultHandler { get; }

        public void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Body);
            walker.Visit(FaultHandler);
        }
    }

    // break
    //
    internal sealed class DebugBreakStatement : CustomOperation
    {
        public override void CustomWalk(OperationWalker walker)
        {
            // no children
        }
    }

    // jmp
    //
    internal sealed class JumpStatement : CustomOperation
    {
        public JumpStatement(IMethodSymbol targetMethod)
        {
            TargetMethod = targetMethod;
        }

        public IMethodSymbol TargetMethod { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            // no children
        }
    }

    // unbox
    // 
    // unlike unbox.any cannot be directly represented as a conversion
    // as it puts a byref to the value type on the heap on to the stack.
    internal sealed class UnboxExpression : ReferenceExpression, ICustomOperation
    {
        public UnboxExpression(IOperation operand, ITypeSymbol type)
            : base(type)
        {
            Operand = operand;
        }

        public IOperation Operand { get; }
        public override OperationKind Kind => OperationKind.None;

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new UnboxExpression(Operand, type);
        }

        public override void Accept(OperationVisitor visitor)
        {

        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return default(TResult);
        }

        public void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Operand);
        }
    }

    // ldftn/ldvirtftn. 
    //
    // TODO: raise use of this combined with delegate creation to IMethodBindingExpresion.
    internal sealed class LoadFunctionExpression : CustomExpression
    {
        public LoadFunctionExpression(IMethodSymbol method, bool isVirtual, Compilation compilation)
        {
            Method = method;
            IsVirtual = isVirtual;
            Type = compilation.GetSpecialType(SpecialType.System_IntPtr);
        }

        public bool IsVirtual { get; }
        public IMethodSymbol Method { get; }
        public override ITypeSymbol Type { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            // no children
        }
    }

    // ldlen
    //
    // TODO: raise to call to Length property
    internal sealed class ArrayLengthExpression : CustomExpression
    {
        public ArrayLengthExpression(IOperation array, Compilation compilation)
        {
            Array = array;
            Type = compilation.GetSpecialType(SpecialType.System_UIntPtr);
        }

        public IOperation Array { get; }
        public override ITypeSymbol Type { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Array);
        }
    }

    // ckfinite
    //
    internal sealed class CheckFiniteExpression : CustomExpression
    {
        public CheckFiniteExpression(IOperation operand)
        {
            Operand = operand;
        }

        public IOperation Operand { get; }
        public override ITypeSymbol Type => Operand.Type;

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Operand);
        }
    }

    // ldtoken
    //
    // TODO: raise Type.GetTypeFromHandle(ldtoken(X)) to typeof(X)
    //       can also raise arbitrary use of ldtoken(type) to typeof(type).TypeHandle,
    //       but still no way to represent ldtoken(field) or ldtoken(method).
    internal sealed class LoadTokenExpression : CustomExpression
    {
        public LoadTokenExpression(ISymbol symbol, Compilation compilation)
        {
            Symbol = symbol;

            if (symbol is ITypeSymbol)
            {
                Type = compilation.GetSpecialType(SpecialType.System_RuntimeTypeHandle);
            }
            else if (symbol is IMethodSymbol)
            {
                Type = compilation.GetSpecialType(SpecialType.System_RuntimeMethodHandle);
            }
            else if (symbol is IFieldSymbol)
            {
                Type = compilation.GetSpecialType(SpecialType.System_RuntimeFieldHandle);
            }
            else
            {
                throw new NotImplementedException(); // error case
            }
        }

        public override ITypeSymbol Type { get; }
        public ISymbol Symbol { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            // no children
        }
    }

    // localloc
    //
    // TODO/FEEDBACK: There does not seem to be a public IOperation node for stackalloc.
    internal sealed class LocalAllocationExpression : CustomExpression
    {
        public LocalAllocationExpression(IOperation size, Compilation compilation)
        {
            Size = size;
            Type = compilation.GetSpecialType(SpecialType.System_IntPtr);
        }

        public IOperation Size { get; }
        public override ITypeSymbol Type { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Size);
        }
    }

    // cpblk
    //
    internal sealed class CopyBlockStatement : CustomOperation
    {
        public CopyBlockStatement(IOperation sourcePointer, IOperation destinationPointer, IOperation byteCount)
        {
            SourcePointer = sourcePointer;
            DestinationPointer = destinationPointer;
            ByteCount = byteCount;
        }

        public IOperation SourcePointer { get; }
        public IOperation DestinationPointer { get; }
        public IOperation ByteCount { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(SourcePointer);
            walker.Visit(DestinationPointer);
            walker.Visit(ByteCount);
        }
    }

    // initblk
    //
    internal sealed class InitializeBlockStatement : CustomOperation
    {
        public InitializeBlockStatement(IOperation pointer, IOperation value, IOperation byteCount)
        {
            Pointer = pointer;
            Value = value;
            ByteCount = byteCount;
        }

        public IOperation Pointer { get; }
        public IOperation Value { get; }
        public IOperation ByteCount { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Pointer);
            walker.Visit(Value);
            walker.Visit(ByteCount);
        }
    }

    // arglist
    //
    // TODO/FEEDBACK: C# has __arglist syntax for this but no public IOperation.
    internal sealed class ArgumentListExpression : CustomExpression
    {
        public ArgumentListExpression(Compilation compilation)
        {
            Type = compilation.GetSpecialType(SpecialType.System_RuntimeArgumentHandle);
        }

        public override ITypeSymbol Type { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            // no children
        }
    }

    // refanyval
    //
    // TODO/FEEDBACK: C# has __refvalue syntax for this but no public IOperation.
    //
    internal sealed class RefValueExpression : ReferenceExpression, ICustomOperation
    {
        public RefValueExpression(IOperation typedReference, ITypeSymbol type)
            : base(type)
        {
            TypedReference = typedReference;
        }

        public IOperation TypedReference { get; }
        public override OperationKind Kind => OperationKind.None;

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new RefValueExpression(TypedReference, type);
        }

        public override void Accept(OperationVisitor visitor)
        {
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return default(TResult); 
        }

        public void CustomWalk(OperationWalker walker)
        {
            walker.Visit(TypedReference);
        }
    }

    // refanytype
    //
    // TODO/FEEDBACK: C# has __reftype syntax for this but no public IOperation. 
    // refanytype is slightly higher as it also calls GetTypeFromHandle, but we
    // can pattern match that in the common case and fallback to getting the handle
    // back via Type.TypeHandle. We can also just raise this node to a call to
    // TypedReference.TargetTypeToken.
    //
    internal sealed class RefTypeExpression : CustomExpression
    {
        public RefTypeExpression(IOperation typedReference, Compilation compilation)
        {
            TypedReference = typedReference;
            Type = compilation.GetSpecialType(SpecialType.System_RuntimeTypeHandle);
        }

        public IOperation TypedReference { get; }
        public override ITypeSymbol Type { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(TypedReference);
        }
    }

    // mkrefany
    //
    // TODO/FEEDBACK: C# has __makeref syntax for this, but no public IOperation.
    //
    internal sealed class MakeRefExpression : CustomExpression
    {
        public MakeRefExpression(ITypeSymbol type, IOperation pointer, Compilation compilation)
        {
            Pointer = pointer;
            Type = compilation.GetSpecialType(SpecialType.System_TypedReference);
        }

        public IOperation Pointer { get; }
        public override ITypeSymbol Type { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Pointer);
        }
    }

    // calli
    //
    internal sealed class IndirectInvocationExpression : CustomExpression
    {
        public IndirectInvocationExpression(SignatureCallingConvention callingConvention, IOperation functionPointer, ITypeSymbol resultType, ImmutableArray<IOperation> arguments)
        {
            FunctionPointer = functionPointer;
            Arguments = arguments;
            Type = resultType;
        }

        // TODO: Need full signature here, not just calling convention.
        public SignatureCallingConvention CallingConvention { get; }
        public IOperation FunctionPointer { get; }
        public ImmutableArray<IOperation> Arguments { get; }
        public override ITypeSymbol Type { get; }

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(FunctionPointer);

            foreach (var argument in Arguments)
            {
                walker.Visit(argument);
            }
        }
    }

    // isinst on value types: Like IsExpression, but result is non-boolean.
    // TODO: raise to IsExpression where possible, else ternary.

    internal sealed class ValueTypeAsExpression : CustomExpression
    {
        public ValueTypeAsExpression(ITypeSymbol asType, IOperation operand)
        {
            AsType = asType;
            Operand = operand;
        }

        public ITypeSymbol AsType { get; }
        public IOperation Operand { get; }
        public override ITypeSymbol Type => Operand.Type;

        public override void CustomWalk(OperationWalker walker)
        {
            walker.Visit(Operand);
        }
    }

}