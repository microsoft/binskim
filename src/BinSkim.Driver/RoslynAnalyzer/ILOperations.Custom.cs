// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;

using Microsoft.CodeAnalysis.Semantics;

namespace Microsoft.CodeAnalysis.IL
{
    // TODO: To ease first pass of Roslyn upgrade
    using IExpression = IOperation;
    using IStatement = IOperation;

    // Nodes for operations that that aren't publicly supported by IOperation.
    //
    // Some of them may be raisable to higher level constructs that are modeled by IOperation (TODO), 
    // while others can happen in IL and have no IOperation equivalent.

    internal interface ICustomExpression : IExpression { }
    internal interface ICustomStatement : IStatement { }

    internal abstract class CustomExpression : Expression, ICustomExpression
    {
        public override OperationKind Kind => OperationKind.None;
    }

    internal abstract class CustomStatement : Statement, ICustomStatement
    {
        public override OperationKind Kind => OperationKind.None;
    }

    internal sealed class TryFaultStatement : TryStatement, ICustomStatement
    {
        public TryFaultStatement(IBlockStatement body, IBlockStatement faultHandler)
            : base(body)
        {
            FaultHandler = faultHandler;
        }

        public IBlockStatement FaultHandler { get; }
        public override OperationKind Kind => OperationKind.None;
    }

    // temporary node to hold an endfilter operation. replaced appropriately when exception blocks are built.
    internal sealed class EndFilter : CustomStatement
    {
        public EndFilter(IExpression expression)
        {
            Expression = expression;
        }

        public IExpression Expression { get; }
    }

    // temporary node to mark end of filter. replaced appropriately when exception blocks are built.
    internal sealed class EndFinally : CustomStatement
    {
        private EndFinally() { }

        public static readonly EndFinally Instance = new EndFinally();
        public override OperationKind Kind => OperationKind.None;
    }

    // For arbitrary filters to hide as IExpression. No direct C#/VB equivalent. 
    //
    // Semantics:
    //
    //  1) execute the block
    //  2) evaluate the expression
    //
    internal sealed class BlockExpression : CustomExpression
    {
        public BlockExpression(IBlockStatement block, IExpression expression)
        {
            Block = block;
            Expression = expression;
        }

        public IBlockStatement Block { get; }
        public IExpression Expression { get; }

        public override ITypeSymbol Type => Expression.Type;
    }

    // break
    //
    internal sealed class DebugBreakStatement : CustomStatement
    {
    }

    // jmp
    //
    internal sealed class JumpStatement : CustomStatement
    {
        public JumpStatement(IMethodSymbol targetMethod)
        {
            TargetMethod = targetMethod;
        }

        public IMethodSymbol TargetMethod { get; }
    }

    // unbox
    // 
    // unlike unbox.any cannot be directly represented as a conversion
    // as it puts a byref to the value type on the heap on to the stack.
    internal sealed class UnboxExpression : ReferenceExpression, ICustomExpression
    {
        public UnboxExpression(IExpression operand, ITypeSymbol type)
            : base(type)
        {
            Operand = operand;
        }

        public IExpression Operand { get; }
        public override OperationKind Kind => OperationKind.None;

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new UnboxExpression(Operand, type);
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
    }

    // ldlen
    //
    // TODO: raise to call to Length property
    internal sealed class ArrayLengthExpression : CustomExpression
    {
        public ArrayLengthExpression(IExpression array, Compilation compilation)
        {
            Array = array;
            Type = compilation.GetSpecialType(SpecialType.System_UIntPtr);
        }

        public IExpression Array { get; }
        public override ITypeSymbol Type { get; }
    }

    // ckfinite
    //
    internal sealed class CheckFiniteExpression : CustomExpression
    {
        public CheckFiniteExpression(IExpression operand)
        {
            Operand = operand;
        }

        public IExpression Operand { get; }
        public override ITypeSymbol Type => Operand.Type;
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
    }

    // localloc
    //
    // TODO/FEEDBACK: There does not seem to be a public IOperation node for stackalloc.
    internal sealed class LocalAllocationExpression : CustomExpression
    {
        public LocalAllocationExpression(IExpression size, Compilation compilation)
        {
            Size = size;
            Type = compilation.GetSpecialType(SpecialType.System_IntPtr);
        }

        public IExpression Size { get; }
        public override ITypeSymbol Type { get; }
    }

    // cpblk
    //
    internal sealed class CopyBlockStatement : CustomStatement
    {
        public CopyBlockStatement(IExpression sourcePointer, IExpression destinationPointer, IExpression byteCount)
        {
            SourcePointer = sourcePointer;
            DestinationPointer = destinationPointer;
            ByteCount = byteCount;
        }

        public IExpression SourcePointer { get; }
        public IExpression DestinationPointer { get; }
        public IExpression ByteCount { get; }
    }

    // initblk
    //
    internal sealed class InitializeBlockStatement : CustomStatement
    {
        public InitializeBlockStatement(IExpression pointer, IExpression value, IExpression byteCount)
        {
            Pointer = pointer;
            Value = value;
            ByteCount = byteCount;
        }

        public IExpression Pointer { get; }
        public IExpression Value { get; }
        public IExpression ByteCount { get; }
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
    }

    // refanyval
    //
    // TODO/FEEDBACK: C# has __refvalue syntax for this but no public IOperation.
    //
    internal sealed class RefValueExpression : ReferenceExpression, ICustomExpression
    {
        public RefValueExpression(IExpression typedReference, ITypeSymbol type)
            : base(type)
        {
            TypedReference = typedReference;
        }

        public IExpression TypedReference { get; }
        public override OperationKind Kind => OperationKind.None;

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new RefValueExpression(TypedReference, type);
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
        public RefTypeExpression(IExpression typedReference, Compilation compilation)
        {
            TypedReference = typedReference;
            Type = compilation.GetSpecialType(SpecialType.System_RuntimeTypeHandle);
        }

        public IExpression TypedReference { get; }
        public override ITypeSymbol Type { get; }
    }

    // mkrefany
    //
    // TODO/FEEDBACK: C# has __makeref syntax for this, but no public IOperation.
    //
    internal sealed class MakeRefExpression : CustomExpression
    {
        public MakeRefExpression(ITypeSymbol type, IExpression pointer, Compilation compilation)
        {
            Pointer = pointer;
            Type = compilation.GetSpecialType(SpecialType.System_TypedReference);
        }

        public IExpression Pointer { get; }
        public override ITypeSymbol Type { get; }
    }

    // calli
    //
    internal sealed class IndirectInvocationExpression : CustomExpression
    {
        public IndirectInvocationExpression(SignatureCallingConvention callingConvention, IExpression functionPointer, ITypeSymbol resultType, ImmutableArray<IExpression> arguments)
        {
            FunctionPointer = functionPointer;
            Arguments = arguments;
            Type = resultType;
        }

        // TODO: Need full signature here, not just calling convention.
        public SignatureCallingConvention CallingConvention { get; }
        public IExpression FunctionPointer { get; }
        public ImmutableArray<IExpression> Arguments { get; }
        public override ITypeSymbol Type { get; }
    }

    // isinst on value types: Like IsExpression, but result is non-boolean.
    // TODO: raise to IsExpression where possible, else ternary.

    internal sealed class ValueTypeAsExpression : CustomExpression
    {
        public ValueTypeAsExpression(ITypeSymbol asType, IExpression operand)
        {
            AsType = asType;
            Operand = operand;
        }

        public ITypeSymbol AsType { get; }
        public IExpression Operand { get; }
        public override ITypeSymbol Type => Operand.Type;
    }

}