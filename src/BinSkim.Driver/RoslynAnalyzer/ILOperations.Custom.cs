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

    // TODO -- Raise to: 
    //
    //    bool succeeded = false;
    //    try { 
    //       ...;
    //       succeeded = true 
    //   } finally { 
    //       if (!succeeded) { ... } 
    //   }

    internal sealed class TryFaultStatement : TryStatement
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
    internal sealed class EndFilter : Statement
    {
        public EndFilter(IExpression expression)
        {
            Expression = expression;
        }

        public IExpression Expression { get; }
        public override OperationKind Kind => OperationKind.None;
    }

    // temporary node to mark end of filter. replaced appropriately when exception blocks are built.
    internal sealed class EndFinally : Statement
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
    internal sealed class BlockExpression : Expression
    {
        public BlockExpression(IBlockStatement block, IExpression expression)
        {
            Block = block;
            Expression = expression;
        }

        public IBlockStatement Block { get; }
        public IExpression Expression { get; }

        public override ITypeSymbol ResultType => Expression.ResultType;
        public override OperationKind Kind => OperationKind.None;
    }

    internal sealed class BreakStatement : Statement
    {
        public override OperationKind Kind => OperationKind.None;
    }
}