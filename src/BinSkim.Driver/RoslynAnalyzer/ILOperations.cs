// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Semantics;
using System.Collections.Immutable;
using System;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.IL
{
    // Things that are needed from a Compilation to ensure various invariants.
    internal interface ISymbolProvider
    {
        ITypeSymbol CreatePointerType(ITypeSymbol pointedAtType);

        // FEEDBACK: Do we need to support custom bounds? (Not supported in VB/C#)
        ITypeSymbol CreateArrayType(ITypeSymbol elementType, int rank);

        ITypeSymbol GetSpecialType(SpecialType specialType);
    }

    internal abstract class Operation : IOperation
    {
        public abstract OperationKind Kind { get; }

        // TODO: Fake this out. 
        // FEEDBACK: Should this be optional, with separate location?
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        SyntaxNode IOperation.Syntax => null;

        // TODO: Handle invalid IL gracefully
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IOperation.IsInvalid => false;

        public override string ToString()
        {
            var name = this.GetType().Name;

            if (name.EndsWith(nameof(Expression), StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - nameof(Expression).Length);
            }
            else if (name.EndsWith(nameof(Statement), StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - nameof(Statement).Length);
            }

            return name;
        }
    }

    internal abstract class Expression : Operation, IExpression
    {
        public abstract ITypeSymbol ResultType { get; }
        public virtual Optional<object> ConstantValue => default(Optional<object>);
    }

    internal abstract class HasArgumentsExpression : Expression
    {
        protected HasArgumentsExpression(ImmutableArray<IArgument> arguments)
        {
            Arguments = arguments;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ImmutableArray<IArgument> Arguments { get; }

        public IArgument ArgumentMatchingParameter(IParameterSymbol parameter)
        {
            int ordinal = parameter.Ordinal;
            if (ordinal < 0 || ordinal >= Arguments.Length)
            {
                return null;
            }

            var argument = Arguments[ordinal];
            if (!argument.Parameter.Equals(parameter))
            {
                return null;
            }

            return argument;
        }
    }

    internal sealed class InvocationExpression : HasArgumentsExpression, IInvocationExpression
    {
        public InvocationExpression(bool isVirtual, IExpression instance, IMethodSymbol targetMethod, ImmutableArray<IArgument> arguments)
            : base(arguments)
        {
            IsVirtual = isVirtual;
            Instance = instance;
            TargetMethod = targetMethod;
        }

        public bool IsVirtual { get; }
        public IExpression Instance { get; }
        public IMethodSymbol TargetMethod { get; }
        public override ITypeSymbol ResultType => TargetMethod.ReturnType;
        public override OperationKind Kind => OperationKind.InvocationExpression;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<IArgument> IInvocationExpression.ArgumentsInParameterOrder => Arguments;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<IArgument> IInvocationExpression.ArgumentsInSourceOrder => Arguments;
    }

    internal sealed class ObjectCreationExpression : HasArgumentsExpression, IObjectCreationExpression
    {
        public ObjectCreationExpression(IMethodSymbol constructor, ImmutableArray<IArgument> arguments)
            : base(arguments)
        {
            Constructor = constructor;
        }

        public IMethodSymbol Constructor { get; }
        public override ITypeSymbol ResultType => Constructor.ContainingType;
        public override OperationKind Kind => OperationKind.ObjectCreationExpression;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<IMemberInitializer> IObjectCreationExpression.MemberInitializers => ImmutableArray<IMemberInitializer>.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<IArgument> IObjectCreationExpression.ConstructorArguments => Arguments;
    }

    internal sealed class Argument : IArgument
    {
        public Argument(IParameterSymbol parameter, IExpression value)
        {
            Parameter = parameter;
            Value = value;
        }

        public IParameterSymbol Parameter { get; }
        public IExpression Value { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ArgumentKind IArgument.Kind => ArgumentKind.Positional;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IExpression IArgument.InConversion => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IExpression IArgument.OutConversion => null;

        public override string ToString()
        {
            return $"Argument [{Parameter.Name}: {Value}]";
        }
    }

    internal abstract class ReferenceExpression : Expression, IReferenceExpression
    {
    }

    internal sealed class ArrayElementReferenceExpression : ReferenceExpression, IArrayElementReferenceExpression
    {
        public ArrayElementReferenceExpression(IExpression arrayReference, ImmutableArray<IExpression> indices)
        {
            ArrayReference = arrayReference;
            Indices = indices;
        }

        public IExpression ArrayReference { get; }
        public ImmutableArray<IExpression> Indices { get; }
        public override ITypeSymbol ResultType => ((IArrayTypeSymbol)ArrayReference.ResultType).ElementType;
        public override OperationKind Kind => OperationKind.ArrayElementReferenceExpression;
    }

    internal sealed class PointerIndirectionReferenceExpression : ReferenceExpression, IPointerIndirectionReferenceExpression
    {
        public PointerIndirectionReferenceExpression(IExpression pointer)
        {
            Pointer = pointer;
        }

        public IExpression Pointer { get; }
        public override ITypeSymbol ResultType => ((IPointerTypeSymbol)Pointer.ResultType).PointedAtType;
        public override OperationKind Kind => OperationKind.PointerIndirectionReferenceExpression;
    }

    internal class LocalReferenceExpression : ReferenceExpression, ILocalReferenceExpression
    {
        public LocalReferenceExpression(ILocalSymbol local)
        {
            Local = local;
        }

        public ILocalSymbol Local { get; }
        public override ITypeSymbol ResultType => Local.Type;
        public override OperationKind Kind => OperationKind.LocalReferenceExpression;

        public override string ToString()
        {
            return $"LocalReference [{Local.Name}]";
        }
    }

    internal class ParameterReferenceExpression : ReferenceExpression, IParameterReferenceExpression
    {
        public ParameterReferenceExpression(IParameterSymbol parameter)
        {
            Parameter = parameter;
        }

        public IParameterSymbol Parameter { get; }
        public override ITypeSymbol ResultType => Parameter.Type;
        public override OperationKind Kind => OperationKind.ParameterReferenceExpression;

        public override string ToString()
        {
            return $"ParameterReference [{Parameter.Name}]";
        }
    }

    internal sealed class InstanceReferenceExpression : ParameterReferenceExpression, IInstanceReferenceExpression
    {
        public InstanceReferenceExpression(IMethodSymbol method)
            : base(null) // FEEDBACK: There no public API to get the this parameter. 
        {                // Do we need IInstanceReferenceExpression to be IParameterReferenceExpresison?
            ResultType = method.ContainingType;
        }

        public override ITypeSymbol ResultType { get; }
        public override OperationKind Kind => OperationKind.InstanceReferenceExpression;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IInstanceReferenceExpression.IsExplicit => true;

        public override string ToString()
        {
            return "InstanceReference";
        }
    }

    internal abstract class MemberReferenceExpression : ReferenceExpression, IMemberReferenceExpression
    {
        protected MemberReferenceExpression(IExpression instance)
        {
            Instance = instance;
        }

        public IExpression Instance { get; }
    }

    internal sealed class FieldReferenceExpression : MemberReferenceExpression, IFieldReferenceExpression
    {
        public FieldReferenceExpression(IExpression instance, IFieldSymbol field)
            : base(instance)
        {
            Field = field;
        }

        public IFieldSymbol Field { get; }

        public override OperationKind Kind => OperationKind.FieldReferenceExpression;
        public override ITypeSymbol ResultType => Field.Type;
    }

    // FEEDBACK: This does not appear to be used implemented by VB or C#, but seems
    // like what I might  need for ldftn and ldvirtfn. Do I have the interpretation right?
    internal sealed class MethodReferenceExpression : MemberReferenceExpression, IMethodReferenceExpression
    {
        public MethodReferenceExpression(ISymbolProvider provider, IExpression instance, bool isVirtual, IMethodSymbol method)
            : base(instance)
        {
            ResultType = provider.GetSpecialType(SpecialType.System_IntPtr);
        }

        public bool IsVirtual { get; }
        public IMethodSymbol Method { get; }
        public override ITypeSymbol ResultType { get; }
        public override OperationKind Kind => OperationKind.MethodReferenceExpression;
    }

    internal abstract class HasOperatorExpression : Expression, IHasOperatorExpression
    {
        // operator method calls will be raised as regular invocations so this is always false/null.
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IHasOperatorExpression.UsesOperatorMethod => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IMethodSymbol IHasOperatorExpression.Operator => null;
    }

    internal sealed class UnaryOperatorExpression : HasOperatorExpression, IUnaryOperatorExpression
    {
        public UnaryOperatorExpression(UnaryOperationKind unaryKind, IExpression operand, ITypeSymbol resultType)
        {
            UnaryKind = unaryKind;
            Operand = operand;
            ResultType = resultType;
        }

        public UnaryOperationKind UnaryKind { get; }
        public IExpression Operand { get; }
        public override ITypeSymbol ResultType { get; }

        public override OperationKind Kind => OperationKind.UnaryOperatorExpression;
    }

    internal sealed class BinaryOperatorExpression : HasOperatorExpression, IBinaryOperatorExpression
    {
        public BinaryOperatorExpression(BinaryOperationKind binaryKind, IExpression left, IExpression right, ITypeSymbol resultType)
        {
            BinaryKind = binaryKind;
            Left = left;
            Right = right;
            ResultType = resultType;
        }

        public BinaryOperationKind BinaryKind { get; }
        public IExpression Left { get; }
        public IExpression Right { get; }
        public override ITypeSymbol ResultType { get; }
        public override OperationKind Kind => OperationKind.BinaryOperatorExpression;
    }

    internal sealed class ConversionExpression : HasOperatorExpression, IConversionExpression
    {
        public ConversionExpression(ConversionKind conversion, IExpression operand, ITypeSymbol resultType)
        {
            Conversion = conversion;
            Operand = operand;
        }

        public ConversionKind Conversion { get; }
        public IExpression Operand { get; }
        public override ITypeSymbol ResultType { get; }
        public override OperationKind Kind => OperationKind.ConversionExpression;

        bool IConversionExpression.IsExplicit => true;
    }


    internal sealed class SizeOfExpression : Expression, ITypeOperationExpression
    {
        public SizeOfExpression(ISymbolProvider provider, ITypeSymbol typeOperand)
        {
            TypeOperand = typeOperand;
            ResultType = provider.GetSpecialType(SpecialType.System_UInt32);
        }

        public ITypeSymbol TypeOperand { get; }
        public override ITypeSymbol ResultType { get; }
        public override OperationKind Kind => OperationKind.TypeOperationExpression;
        public TypeOperationKind TypeOperationClass => TypeOperationKind.SizeOf; // FEEDBACK: Inconsistent naming of enum and member.
    }

    // TODO: TypeOfExpression should be here.
    // FEEDBACK: It would be easier to express ldtoken directly, with separate GetTypeFomHandle call.
    //           Also exposes gap for field and method handles.

    internal sealed class LiteralExpression : Expression, ILiteralExpression
    {
        public LiteralExpression(object value, ITypeSymbol type)
        {
            ConstantValue = value;
            ResultType = type;
        }

        public override Optional<object> ConstantValue { get; }
        public override ITypeSymbol ResultType { get; }
        public override OperationKind Kind => OperationKind.LiteralExpression;

        // TODO: Proper IL/round-trippable syntax.
        // FEEDBACK: Does Spelling belong on ILiteralExpression? Why not get it from IOperation.Syntax?
        public string Spelling => ConstantValue.ToString();

        public override string ToString()
        {
            return $"Literal [{ConstantValue}]";
        }
    }

    internal sealed class AddressOfExpression : Expression, IAddressOfExpression
    {
        public AddressOfExpression(Compilation compilation, IReferenceExpression addressed)
        {
            Addressed = addressed;
            ResultType = compilation.CreatePointerTypeSymbol(addressed.ResultType);
        }

        public IReferenceExpression Addressed { get; }
        public override ITypeSymbol ResultType { get; }
        public override OperationKind Kind => OperationKind.AddressOfExpression;
    }

    internal sealed class ArrayCreationExpression : Expression, IArrayCreationExpression
    {
        public ArrayCreationExpression(Compilation compilation, ITypeSymbol elementType, ImmutableArray<IExpression> dimensionSizes)
        {
            ElementType = elementType;
            DimensionSizes = dimensionSizes;
            ResultType = compilation.CreateArrayTypeSymbol(elementType, dimensionSizes.Length);
        }

        public ITypeSymbol ElementType { get; }
        public ImmutableArray<IExpression> DimensionSizes { get; }
        public override ITypeSymbol ResultType { get; }
        public override OperationKind Kind => OperationKind.ArrayCreationExpression;

        IArrayInitializer IArrayCreationExpression.ElementValues => null;
    }

    internal sealed class AssignmentExpression : Expression, IAssignmentExpression
    {
        public AssignmentExpression(IReferenceExpression target, IExpression value)
        {
            Target = target;
            Value = value;
        }

        public IReferenceExpression Target { get; }
        public IExpression Value { get; }

        public override OperationKind Kind => OperationKind.AssignmentExpression;
        public override ITypeSymbol ResultType => Target.ResultType;

        public override string ToString()
        {
            return $"Assignment [{Target} = {Value}]";
        }
    }

    internal abstract class Statement : Operation, IStatement
    {
    }

    internal sealed class ExpressionStatement : Statement, IExpressionStatement
    {
        public ExpressionStatement(IExpression expression)
        {
            Expression = expression;
        }

        public IExpression Expression { get; }
        public override OperationKind Kind => OperationKind.ExpressionStatement;

        public override string ToString()
        {
            return $"{Expression} (statement)";
        }
    }

    internal class BlockStatement : Statement, IBlockStatement
    {
        public BlockStatement(ImmutableArray<ILocalSymbol> locals, ImmutableArray<IStatement> statements)
        {
            Locals = locals;
            Statements = statements;
        }

        public ImmutableArray<ILocalSymbol> Locals { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ImmutableArray<IStatement> Statements { get; }
        public override OperationKind Kind => OperationKind.BlockStatement;
    }

    // FEEDBACK: Why is there both ILabelStatement and ILabeledStatement? Not sure which to use.
    internal sealed class LabelStatement : Statement, ILabelStatement
    {
        public LabelStatement(ILabelSymbol label)
        {
            Label = label;
        }

        public ILabelSymbol Label { get; }
        public override OperationKind Kind => OperationKind.LabelStatement;

        public override string ToString()
        {
            return $"Label: {Label.Name}";
        }
    }

    internal sealed class IfStatement : Statement, IIfStatement
    {
        public IfStatement(IExpression condition, IStatement ifTrue)
        {
            Condition = condition;
            IfTrue = ifTrue;
        }
        public IExpression Condition { get; }
        public IStatement IfTrue { get; }
        public override OperationKind Kind => OperationKind.IfStatement;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IStatement IIfStatement.IfFalse => null; // we always goto on true and fallthrough on false 
    }

    internal sealed class GoToStatement : Statement, IBranchStatement
    {
        public GoToStatement(ILabelSymbol target)
        {
            Target = target;
        }

        public ILabelSymbol Target { get; }

        // FEEDBACK: first non 1:1 I see between interface name and kind, others have distinct subkinds. Should this be BranchKind.GoTo and OperationKind.BranchStatement?
        public override OperationKind Kind => OperationKind.GoToStatement;

        public override string ToString()
        {
            return $"GoTo {Target.Name}";
        }
    }

    internal sealed class ThrowStatement : Statement, IThrowStatement
    {
        public ThrowStatement(IExpression thrown)
        {
            Thrown = thrown;
        }

        public IExpression Thrown { get; }
        public override OperationKind Kind => OperationKind.ThrowStatement;

        public override string ToString()
        {
            return Thrown == null ? "Throw" : $"Throw [{Thrown}]";
        }
    }

    internal sealed class ReturnStatement : Statement, IReturnStatement
    {
        public ReturnStatement(IExpression returned)
        {
            Returned = returned;
        }

        public IExpression Returned { get; }
        public override OperationKind Kind => OperationKind.ReturnStatement;

        public override string ToString()
        {
            return Returned == null ? "Return" : $"Return [{Returned}]";
        }
    }

    internal sealed class TryStatement : Statement, ITryStatement
    {
        public TryStatement(IBlockStatement body, ImmutableArray<ICatch> catches, IBlockStatement finallyHandler)
        {
            Body = body;
            Catches = catches;
            FinallyHandler = finallyHandler;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IBlockStatement Body { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ImmutableArray<ICatch> Catches { get; }
        public IBlockStatement FinallyHandler { get; }
        public override OperationKind Kind => OperationKind.TryStatement;
    }

    internal sealed class Catch : Operation, ICatch
    {
        public Catch(ITypeSymbol caughtType, ILocalSymbol exceptionLocal, IExpression filter, IBlockStatement handler)
        {
            CaughtType = caughtType;
            ExceptionLocal = exceptionLocal;
            Filter = filter;
            Handler = handler;
        }

        public ITypeSymbol CaughtType { get; }
        public ILocalSymbol ExceptionLocal { get; }
        public IExpression Filter { get; }
        public IBlockStatement Handler { get; }

        // FEEDBACK: inconsistently named vs. interface
        public override OperationKind Kind => OperationKind.CatchHandler;
    }
}
