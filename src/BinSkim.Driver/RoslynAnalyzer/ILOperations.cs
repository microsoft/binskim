// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Semantics;
using System.Collections.Immutable;
using System;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.IL
{
    // TODO: To ease first pass of Roslyn upgrade
    using IExpression = IOperation;
    using IStatement = IOperation;

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

        public virtual ITypeSymbol Type => null;
        public virtual Optional<object> ConstantValue => default(Optional<object>);

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

        public void Accept(OperationVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            throw new NotImplementedException();
        }
    }

    internal abstract class Expression : Operation, IExpression
    {
        public abstract override ITypeSymbol Type { get; }
    }

    internal abstract class HasArgumentsExpression : Expression, IHasArgumentsExpression
    {
        protected HasArgumentsExpression(ImmutableArray<IArgument> arguments)
        {
            Arguments = arguments;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ImmutableArray<IArgument> Arguments { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<IArgument> IHasArgumentsExpression.ArgumentsInParameterOrder => Arguments;

        public IArgument GetArgumentMatchingParameter(IParameterSymbol parameter)
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
        public override ITypeSymbol Type => TargetMethod.ReturnType;
        public override OperationKind Kind => OperationKind.InvocationExpression;

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

        //NOTE: Not just .ContainingType to allow for magic array methods that belong to array types.
        public override ITypeSymbol Type => Constructor.ContainingSymbol as ITypeSymbol; 
        public override OperationKind Kind => OperationKind.ObjectCreationExpression;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<ISymbolInitializer> IObjectCreationExpression.MemberInitializers => ImmutableArray<ISymbolInitializer>.Empty;
    }

    internal sealed class Argument : Operation, IArgument
    {
        public Argument(IParameterSymbol parameter, IExpression value)
        {
            Parameter = parameter;
            Value = value;
        }

        public IParameterSymbol Parameter { get; }
        public IExpression Value { get; }
        public override OperationKind Kind => OperationKind.Argument;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ArgumentKind IArgument.ArgumentKind => ArgumentKind.Positional;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IExpression IArgument.InConversion => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IExpression IArgument.OutConversion => null;

        public override string ToString()
        {
            return $"Argument [{Parameter?.Name ?? "(vararg)"} : {Value}]";
        }
    }

    internal sealed class DefaultValueExpression : Expression
    {
        public DefaultValueExpression(ITypeSymbol type)
        {
            Type = type;
        }

        public override ITypeSymbol Type { get; }
        public override OperationKind Kind => OperationKind.DefaultValueExpression;
    }

    internal abstract class ReferenceExpression : Expression, IReferenceExpression
    {
        protected ReferenceExpression(ITypeSymbol type)
        {
            Type = type;
        }

        public sealed override ITypeSymbol Type { get; }

        public IReferenceExpression WithType(ITypeSymbol type)
        {
            if (type == null || type == Type)
                return this;

            return WithTypeCore(type);
        }

        protected abstract IReferenceExpression WithTypeCore(ITypeSymbol type);
    }

    internal sealed class ArrayElementReferenceExpression : ReferenceExpression, IArrayElementReferenceExpression
    {
        public ArrayElementReferenceExpression(IExpression arrayReference, IExpression index, ITypeSymbol type)
            : this(arrayReference, ImmutableArray.Create(index), type)
        {
        }

        public ArrayElementReferenceExpression(IExpression arrayReference, ImmutableArray<IExpression> indices, ITypeSymbol type)
            : base(type)
        {
            ArrayReference = arrayReference;
            Indices = indices;
        }

        public IExpression ArrayReference { get; }
        public ImmutableArray<IExpression> Indices { get; }
        public override OperationKind Kind => OperationKind.ArrayElementReferenceExpression;

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new ArrayElementReferenceExpression(ArrayReference, Indices, type);
        }
    }

    internal sealed class PointerIndirectionReferenceExpression : ReferenceExpression, IPointerIndirectionReferenceExpression
    {
        public PointerIndirectionReferenceExpression(IExpression pointer, ITypeSymbol type)
            : base(type)
        {
            Pointer = pointer;
        }

        public IExpression Pointer { get; }
        public override OperationKind Kind => OperationKind.PointerIndirectionReferenceExpression;

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new PointerIndirectionReferenceExpression(Pointer, type);
        }
    }

    internal class LocalReferenceExpression : ReferenceExpression, ILocalReferenceExpression
    {
        public LocalReferenceExpression(ILocalSymbol local)
            : this(local, local.Type)
        {
        }

        public LocalReferenceExpression(ILocalSymbol local, ITypeSymbol type)
            : base(type)
        {
            Local = local;
        }

        public ILocalSymbol Local { get; }
        public override OperationKind Kind => OperationKind.LocalReferenceExpression;

        public override string ToString()
        {
            return $"LocalReference [{Local.Name}]";
        }

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new LocalReferenceExpression(Local, type);
        }
    }

    internal class ParameterReferenceExpression : ReferenceExpression, IParameterReferenceExpression
    {
        public ParameterReferenceExpression(IParameterSymbol parameter)
            : this(parameter, parameter.Type)
        {
        }

        public ParameterReferenceExpression(IParameterSymbol parameter, ITypeSymbol type)
            : base(type)
        {
            Parameter = parameter;
        }

        public IParameterSymbol Parameter { get; }
        public override OperationKind Kind => OperationKind.ParameterReferenceExpression;

        public override string ToString()
        {
            return $"ParameterReference [{Parameter.Name}]";
        }

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new ParameterReferenceExpression(Parameter, type);
        }
    }

    internal sealed class InstanceReferenceExpression : ReferenceExpression, IInstanceReferenceExpression
    {
        public InstanceReferenceExpression(IMethodSymbol method)
            : this(method.ContainingType)
        {
        }

        public InstanceReferenceExpression(ITypeSymbol type)
            : base(type)
        {
        }

        public InstanceReferenceKind InstanceReferenceKind => InstanceReferenceKind.Explicit; // TODO/FEEDBACK: needs to be adjusted to base sometimes, but what about other bindings?
        public override OperationKind Kind => OperationKind.InstanceReferenceExpression;

        public override string ToString()
        {
            return "InstanceReference";
        }

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new InstanceReferenceExpression(type);
        }
    }

    internal abstract class MemberReferenceExpression : ReferenceExpression, IMemberReferenceExpression
    {
        protected MemberReferenceExpression(IExpression instance, ISymbol member, ITypeSymbol type)
            : base(type)
        {
            Instance = instance;
            Member = member;
        }

        public IExpression Instance { get; }
        public ISymbol Member { get; }
    }

    internal sealed class FieldReferenceExpression : MemberReferenceExpression, IFieldReferenceExpression
    {
        public FieldReferenceExpression(IExpression instance, IFieldSymbol field)
            : this(instance, field, field.Type)
        {
        }

        public FieldReferenceExpression(IExpression instance, ISymbol member, ITypeSymbol type)
            : base(instance, member, type)
        {
        }

        public IFieldSymbol Field => (IFieldSymbol)Member;
        public override OperationKind Kind => OperationKind.FieldReferenceExpression;

        protected override IReferenceExpression WithTypeCore(ITypeSymbol type)
        {
            return new FieldReferenceExpression(Instance, Field, type);
        }
    }

    internal abstract class HasOperatorMethodExpression : Expression, IHasOperatorMethodExpression
    {
        // operator method calls will be raised as regular invocations so this is always false/null.
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IHasOperatorMethodExpression.UsesOperatorMethod => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IMethodSymbol IHasOperatorMethodExpression.OperatorMethod => null;
    }

    internal sealed class UnaryOperatorExpression : HasOperatorMethodExpression, IUnaryOperatorExpression
    {
        public UnaryOperatorExpression(UnaryOperationKind unaryOperationKind, IExpression operand, ITypeSymbol type)
        {
            UnaryOperationKind = unaryOperationKind;
            Operand = operand;
            Type = type;
        }

        public UnaryOperationKind UnaryOperationKind { get; }
        public IExpression Operand { get; }
        public override ITypeSymbol Type { get; }

        public override OperationKind Kind => OperationKind.UnaryOperatorExpression;
    }

    internal sealed class BinaryOperatorExpression : HasOperatorMethodExpression, IBinaryOperatorExpression
    {
        public BinaryOperatorExpression(BinaryOperationKind binaryOperationKind, IExpression left, IExpression right, ITypeSymbol type)
        {
            BinaryOperationKind = binaryOperationKind;
            LeftOperand = left;
            RightOperand = right;
            Type = type;
        }

        public BinaryOperationKind BinaryOperationKind { get; }
        public IExpression LeftOperand { get; }
        public IExpression RightOperand { get; }
        public override ITypeSymbol Type { get; }
        public override OperationKind Kind => OperationKind.BinaryOperatorExpression;
    }

    internal sealed class ConversionExpression : HasOperatorMethodExpression, IConversionExpression
    {
        public ConversionExpression(ConversionKind conversionKind, IExpression operand, ITypeSymbol type)
        {
            ConversionKind = conversionKind;
            Operand = operand;
            Type = type;
        }

        public ConversionKind ConversionKind { get; }
        public IExpression Operand { get; }
        public override ITypeSymbol Type { get; }
        public override OperationKind Kind => OperationKind.ConversionExpression;

        bool IConversionExpression.IsExplicit => true;
    }

    internal sealed class SizeOfExpression : Expression, ISizeOfExpression
    {
        public SizeOfExpression(Compilation compilation, ITypeSymbol typeOperand)
        {
            TypeOperand = typeOperand;
            Type = compilation.GetSpecialType(SpecialType.System_UInt32);
        }

        public ITypeSymbol TypeOperand { get; }
        public override ITypeSymbol Type { get; }
        public override OperationKind Kind => OperationKind.SizeOfExpression;
    }

    internal sealed class LiteralExpression : Expression, ILiteralExpression
    {
        public LiteralExpression(object value, ITypeSymbol type)
        {
            ConstantValue = value;
            Type = type;
        }

        public override Optional<object> ConstantValue { get; }
        public override ITypeSymbol Type { get; }
        public override OperationKind Kind => OperationKind.LiteralExpression;

        // TODO: Proper IL/round-trippable syntax.
        public string Text => ConstantValue.ToString();

        public override string ToString()
        {
            return $"Literal [{ConstantValue.Value ?? "null"}]";
        }
    }

    internal sealed class AddressOfExpression : Expression, IAddressOfExpression
    {
        public AddressOfExpression(Compilation compilation, IReferenceExpression reference)
        {
            Reference = reference;
            Type = compilation.CreatePointerTypeSymbol(reference.Type);
        }

        public IReferenceExpression Reference { get; }
        public override ITypeSymbol Type { get; }
        public override OperationKind Kind => OperationKind.AddressOfExpression;
    }

    internal sealed class ArrayCreationExpression : Expression, IArrayCreationExpression
    {
        public ArrayCreationExpression(Compilation compilation, ITypeSymbol elementType, IExpression size)
        {
            ElementType = elementType;
            DimensionSizes = ImmutableArray.Create(size);
            Type = compilation.CreateArrayTypeSymbol(elementType);
        }

        public ITypeSymbol ElementType { get; }
        public ImmutableArray<IExpression> DimensionSizes { get; }
        public override ITypeSymbol Type { get; }
        public override OperationKind Kind => OperationKind.ArrayCreationExpression;

        IArrayInitializer IArrayCreationExpression.Initializer => null;
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
        public override ITypeSymbol Type => Target.Type;

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
        public BlockStatement(ImmutableArray<IStatement> statements)
            : this(statements, ImmutableArray<ILocalSymbol>.Empty)
        {
        }

        public BlockStatement(ImmutableArray<IStatement> statements, ImmutableArray<ILocalSymbol> locals)
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

        // This is allowed to be null and so we do that uniformly.
        IExpression ILabelStatement.LabeledStatement => null;

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
            IfTrueStatement = ifTrue;
        }
        public IExpression Condition { get; }
        public IStatement IfTrueStatement { get; }
        public override OperationKind Kind => OperationKind.IfStatement;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IStatement IIfStatement.IfFalseStatement => null; // we always goto on true and fallthrough on false 
    }

    internal sealed class BranchStatement : Statement, IBranchStatement
    {
        public BranchStatement(ILabelSymbol target)
        {
            Target = target;
        }

        public ILabelSymbol Target { get; }
        public override OperationKind Kind => OperationKind.BranchStatement;
        public BranchKind BranchKind => BranchKind.GoTo;

        public override string ToString()
        {
            return $"GoTo {Target.Name}";
        }
    }

    internal sealed class ThrowStatement : Statement, IThrowStatement
    {
        public ThrowStatement(IExpression thrown)
        {
            ThrownObject = thrown;
        }

        public IExpression ThrownObject { get; }
        public override OperationKind Kind => OperationKind.ThrowStatement;

        public override string ToString()
        {
            return ThrownObject == null ? "Throw" : $"Throw [{ThrownObject}]";
        }
    }

    internal sealed class ReturnStatement : Statement, IReturnStatement
    {
        public ReturnStatement(IExpression returned)
        {
            ReturnedValue = returned;
        }

        public IExpression ReturnedValue { get; }
        public override OperationKind Kind => OperationKind.ReturnStatement;

        public override string ToString()
        {
            return ReturnedValue == null ? "Return" : $"Return [{ReturnedValue}]";
        }
    }

    internal abstract class TryStatement : Statement, ITryStatement
    {
        public TryStatement(IBlockStatement body)
        {
            Body = body;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IBlockStatement Body { get; }

        public virtual ImmutableArray<ICatchClause> Catches => ImmutableArray<ICatchClause>.Empty;
        public virtual IBlockStatement FinallyHandler => null;

        public override OperationKind Kind => OperationKind.TryStatement;
    }

    internal sealed class TryCatchStatement : TryStatement
    {
        public TryCatchStatement(IBlockStatement body, ImmutableArray<ICatchClause> catches)
            : base(body)
        {
            Catches = catches;
        }

        public override ImmutableArray<ICatchClause> Catches { get; }
    }

    internal sealed class TryFinallyStatement : TryStatement
    {
        public TryFinallyStatement(IBlockStatement body, IBlockStatement finallyHandler)
            : base(body)
        {
            FinallyHandler = finallyHandler;
        }

        public override IBlockStatement FinallyHandler { get; }
    }

    internal sealed class CatchClause : Operation, ICatchClause
    {
        public CatchClause(ITypeSymbol caughtType, ILocalSymbol exceptionLocal, IExpression filter, IBlockStatement handler)
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

        public override OperationKind Kind => OperationKind.CatchClause;
    }

    internal sealed class SwitchStatement : Statement, ISwitchStatement
    {
        public SwitchStatement(IExpression value, ImmutableArray<ISwitchCase> cases)
        {
            Value = value;
            Cases = cases;
        }

        public IExpression Value { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ImmutableArray<ISwitchCase> Cases { get; }

        public override OperationKind Kind => OperationKind.SwitchStatement;
    }

    internal sealed class SwitchCase : Statement, ISwitchCase
    {
        public SwitchCase(IExpression value, IStatement body)
        {
            Clauses = ImmutableArray.Create<ICaseClause>(new SingleValueCaseClause(value));
            Body = ImmutableArray.Create<IStatement>(body);
        }

        public ImmutableArray<ICaseClause> Clauses { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ImmutableArray<IStatement> Body { get; }
        
        public override OperationKind Kind => OperationKind.SwitchCase;

        public override string ToString()
        {
            return $"Case {((SingleValueCaseClause)Clauses[0]).Value}";
        }
    }

    internal sealed class SingleValueCaseClause : Operation, ISingleValueCaseClause
    {
        public SingleValueCaseClause(IExpression value)
        {
            Value = value;
        }

        public IExpression Value { get; }

        public BinaryOperationKind Equality => BinaryOperationKind.IntegerEquals;
        public CaseKind CaseKind => CaseKind.SingleValue;
        public override OperationKind Kind => OperationKind.SingleValueCaseClause;
    }
}
