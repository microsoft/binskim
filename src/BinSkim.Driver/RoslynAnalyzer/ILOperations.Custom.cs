// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.Semantics;

namespace Microsoft.CodeAnalysis.IL
{
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

    // TODO -- Raise to: 
    //
    //    bool succeeded = false;
    //    try { 
    //       ...;
    //       succeeded = true 
    //   } finally { 
    //       if (!succeeded) { ... } 
    //   }

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

        public override ITypeSymbol ResultType => Expression.ResultType;
    }

    // break opcode
    internal sealed class BreakStatement : CustomStatement
    {
    }
    
    // jmp opcode
    //
    // TODO: raise to tail call
    internal sealed class JumpStatement : CustomStatement
    {
        public JumpStatement(IMethodSymbol targetMethod)
        {
            TargetMethod = targetMethod;
        }

        public IMethodSymbol TargetMethod { get; }
    }

    // unbox opcode: unlike unbox.any cannot be directly represented as a conversion
    // as it puts a byref to the value type on the heap on to the stack.
    internal sealed class UnboxExpression : CustomExpression
    {
        public UnboxExpression(IExpression operand, ITypeSymbol valueType, Compilation compilation)
        {
            Operand = operand;
            ResultType = compilation.CreatePointerTypeSymbol(valueType); // TODO: Need to handle managed pointer (by-ref) somehow.
        }

        public IExpression Operand { get; private set; }
        public override ITypeSymbol ResultType { get; }
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
            ResultType = compilation.GetSpecialType(SpecialType.System_IntPtr);
        }

        public bool IsVirtual { get; }
        public IMethodSymbol Method { get; }
        public override ITypeSymbol ResultType { get; }
    }

    // ldlen
    //
    // TODO: raise to call to Length property
    internal sealed class ArrayLengthExpression : CustomExpression
    {
        public ArrayLengthExpression(IExpression array, Compilation compilation)
        {
            Array = array;
            ResultType = compilation.GetSpecialType(SpecialType.System_UIntPtr);
        }

        public IExpression Array { get; }
        public override ITypeSymbol ResultType { get; }
    }

    // ckfinite
    //
    // TODO: raise X(ckfinite(Y)) to
    //
    //      double tmp = Y;
    //      if (dobule.IsNaN(tmp) || double.IsInfinity(tmp)) {
    //          throw new ArithmeticException();
    //      }
    //      X(tmp);
    //
    internal sealed class CheckFiniteExpression : CustomExpression
    {
        public CheckFiniteExpression(IExpression operand)
        {
            Operand = operand;
        }

        public IExpression Operand { get; }
        public override ITypeSymbol ResultType => Operand.ResultType;
    }

    // ldtoken
    //
    // TODO: raise Type.GetTypeFromHandle(ldtoken(X)) to typeof(X)
    internal sealed class LoadTokenExpression : CustomExpression
    {
        public LoadTokenExpression(ISymbol symbol, Compilation compilation)
        {
            Symbol = symbol;

            if (symbol is ITypeSymbol)
            {
                ResultType = compilation.GetSpecialType(SpecialType.System_RuntimeTypeHandle);
            }
            else if (symbol is IMethodSymbol)
            {
                ResultType = compilation.GetSpecialType(SpecialType.System_RuntimeMethodHandle);
            }
            else if (symbol is IFieldSymbol)
            {
                ResultType = compilation.GetSpecialType(SpecialType.System_RuntimeFieldHandle);
            }
            else
            {
                throw new NotImplementedException(); // error case
            }
        }

        public override ITypeSymbol ResultType { get; }
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
            ResultType = compilation.GetSpecialType(SpecialType.System_IntPtr);
        }

        public IExpression Size { get; }
        public override ITypeSymbol ResultType { get; }
    }
}