// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.Metadata;
using System.Threading;

// TODO: Figure out versioning here. Either we have to move in to Roslyn 
// or Roslyn needs to provide a version-safe API for generating symbols.
// ...Or is there an existing API that I'm missing?

#pragma warning disable RS1009 // Only internal implementations of this interface are allowed.

namespace Microsoft.CodeAnalysis.IL
{
    internal abstract class Symbol : ISymbol
    {
        protected Symbol(string name, ISymbol containingSymbol)
        {
            Name = name;
            ContainingSymbol = containingSymbol;
        }

        public string Name { get; }
        public ISymbol ContainingSymbol { get; }
        public IMethodSymbol ContainingMethod => ContainingSymbol as IMethodSymbol;
        public INamedTypeSymbol ContainingType => (ContainingSymbol as INamedTypeSymbol) ?? (ContainingMethod?.ContainingType);

        public abstract SymbolKind Kind { get; }
        public abstract void Accept(SymbolVisitor visitor);
        public abstract TResult Accept<TResult>(SymbolVisitor<TResult> visitor);

        ImmutableArray<SymbolDisplayPart> ISymbol.ToDisplayParts(SymbolDisplayFormat format)
        {
            return ImmutableArray<SymbolDisplayPart>.Empty; // TODO
        }

        string ISymbol.ToDisplayString(SymbolDisplayFormat format)
        {
            return Name; // TODO
        }

        ImmutableArray<SymbolDisplayPart> ISymbol.ToMinimalDisplayParts(
            SemanticModel semanticModel, 
            int position, 
            SymbolDisplayFormat format)
        {
            return ImmutableArray<SymbolDisplayPart>.Empty; // TODO
        }

        string ISymbol.ToMinimalDisplayString(
            SemanticModel semanticModel, 
            int position,
            SymbolDisplayFormat format)
        {
            return Name; // TODO
        }

        bool IEquatable<ISymbol>.Equals(ISymbol other) => base.Equals(other);
        public sealed override int GetHashCode() => base.GetHashCode();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.CanBeReferencedByName => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IAssemblySymbol ISymbol.ContainingAssembly => ContainingSymbol.ContainingAssembly;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IModuleSymbol ISymbol.ContainingModule => ContainingSymbol.ContainingModule;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INamespaceSymbol ISymbol.ContainingNamespace => ContainingSymbol.ContainingNamespace;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        Accessibility ISymbol.DeclaredAccessibility => Accessibility.NotApplicable;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<SyntaxReference> ISymbol.DeclaringSyntaxReferences => ImmutableArray<SyntaxReference>.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.HasUnsupportedMetadata => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.IsAbstract => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.IsDefinition => true;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.IsExtern => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.IsImplicitlyDeclared => true;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.IsOverride => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.IsSealed => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.IsStatic => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.IsVirtual => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string ISymbol.Language => "IL";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<Location> ISymbol.Locations => ImmutableArray<Location>.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string ISymbol.MetadataName => Name;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ISymbol ISymbol.OriginalDefinition => this;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        Handle ISymbol.MetadataHandle => default(Handle);

        ImmutableArray<AttributeData> ISymbol.GetAttributes() => ImmutableArray<AttributeData>.Empty;
        string ISymbol.GetDocumentationCommentId() => null;
        string ISymbol.GetDocumentationCommentXml(CultureInfo preferredCulture, bool expandIncludes, CancellationToken cancellationToken) => null;
    }


    internal abstract class MethodOwnedSymbol : Symbol
    {
        protected MethodOwnedSymbol(string name, IMethodSymbol containingMethod)
            : base(name, containingMethod)
        {
        }
    }

    internal sealed class LocalSymbol : MethodOwnedSymbol, ILocalSymbol
    {
        public LocalSymbol(string name, IMethodSymbol containingMethod, ITypeSymbol type)
            : base(name, containingMethod)
        {
            Type = type;
        }

        public ITypeSymbol Type { get; }
        public override SymbolKind Kind => SymbolKind.Local;

        public override void Accept(SymbolVisitor visitor)
        {
            visitor.VisitLocal(this);
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            return visitor.VisitLocal(this);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object ILocalSymbol.ConstantValue => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ILocalSymbol.HasConstantValue => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ILocalSymbol.IsConst => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ILocalSymbol.IsFunctionValue => false;

        public override string ToString()
        {
            return $"Local: {Type.Name} {Name}";
        }
    }

    internal sealed class LabelSymbol : MethodOwnedSymbol, ILabelSymbol
    {
        public LabelSymbol(int offset, IMethodSymbol containingMethod, string prefix = "IL")
            : base(GenerateName(offset), containingMethod)
        {
        }

        public override SymbolKind Kind => SymbolKind.Label;

        private static string GenerateName(int offset)
        {
            return $"IL_{offset:X}";
        }

        public override void Accept(SymbolVisitor visitor)
        {
            visitor.VisitLabel(this);
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            return visitor.VisitLabel(this);
        }

        public override string ToString()
        {
            return $"Label: {Name}";
        }
    }

    internal sealed class ParameterSymbol : MethodOwnedSymbol, IParameterSymbol
    {
        public ParameterSymbol(IMethodSymbol containingMethod, int ordinal, ITypeSymbol type)
            :base(string.Empty, containingMethod)
        {
            Ordinal = ordinal;
            Type = type;
        }

        public int Ordinal { get; }
        public ITypeSymbol Type { get; }
        public override SymbolKind Kind => SymbolKind.Parameter;

        public override void Accept(SymbolVisitor visitor)
        {
            visitor.VisitParameter(this);
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            return visitor.VisitParameter(this);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        RefKind IParameterSymbol.RefKind { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<CustomModifier> IParameterSymbol.CustomModifiers => ImmutableArray<CustomModifier>.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IParameterSymbol.HasExplicitDefaultValue => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IParameterSymbol.IsOptional => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IParameterSymbol.IsParams => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IParameterSymbol.IsThis => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IParameterSymbol IParameterSymbol.OriginalDefinition => this;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object IParameterSymbol.ExplicitDefaultValue
        {
            get { throw new InvalidOperationException(); }
        }
    }

    // Placeholder for magic md array methods.
    // TODO: Raise to proper array creation/access expressions.
    internal sealed class ArrayMethodSymbol : Symbol, IMethodSymbol
    {
        public static ArrayMethodSymbol Get(IArrayTypeSymbol arrayType, Compilation compilation)
        {
            return new ArrayMethodSymbol("Get", arrayType, compilation, returnType: arrayType.ElementType);
        }

        public static ArrayMethodSymbol Set(IArrayTypeSymbol arrayType, Compilation compilation)
        {
            return new ArrayMethodSymbol("Set", arrayType, compilation, trailingParameterType: arrayType.ElementType);
        }

        public static ArrayMethodSymbol Ctor(IArrayTypeSymbol arrayType, Compilation compilation)
        {
            return new ArrayMethodSymbol(".ctor", arrayType, compilation);
        }

        private ArrayMethodSymbol(
            string name, 
            IArrayTypeSymbol arrayType,
            Compilation compilation,
            ITypeSymbol returnType = null,
            ITypeSymbol trailingParameterType = null)
            : base(name, arrayType)
        {
            var parameters = ImmutableArray.CreateBuilder<IParameterSymbol>(arrayType.Rank + (trailingParameterType == null ? 0 : 1));

            for (int i = 0; i < arrayType.Rank; i++)
            {
                parameters.Add(new ParameterSymbol(this, i, compilation.GetSpecialType(SpecialType.System_Int32)));
            }

            if (trailingParameterType != null)
            {
                parameters.Add(new ParameterSymbol(this, arrayType.Rank, trailingParameterType));
            }

            ReturnType = returnType ?? compilation.GetSpecialType(SpecialType.System_Void);
            Parameters = parameters.MoveToImmutable();
        }

        public ITypeSymbol ReturnType { get; }
        public ImmutableArray<IParameterSymbol> Parameters { get; }
        public override SymbolKind Kind => SymbolKind.Method;

        public override void Accept(SymbolVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            throw new NotImplementedException();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IMethodSymbol.ReturnsVoid => ReturnType.SpecialType == SpecialType.System_Void;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int IMethodSymbol.Arity => 0;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INamedTypeSymbol IMethodSymbol.AssociatedAnonymousDelegate => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ISymbol IMethodSymbol.AssociatedSymbol => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IMethodSymbol IMethodSymbol.ConstructedFrom => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<IMethodSymbol> IMethodSymbol.ExplicitInterfaceImplementations => ImmutableArray<IMethodSymbol>.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IMethodSymbol.HidesBaseMethodsByName => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IMethodSymbol.IsAsync => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IMethodSymbol.IsCheckedBuiltin => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IMethodSymbol.IsExtensionMethod => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IMethodSymbol.IsGenericMethod => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool IMethodSymbol.IsVararg => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        MethodKind IMethodSymbol.MethodKind => MethodKind.Ordinary;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IMethodSymbol IMethodSymbol.OriginalDefinition => this;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IMethodSymbol IMethodSymbol.OverriddenMethod => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IMethodSymbol IMethodSymbol.PartialDefinitionPart => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IMethodSymbol IMethodSymbol.PartialImplementationPart => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ITypeSymbol  IMethodSymbol.ReceiverType => ContainingType;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IMethodSymbol IMethodSymbol.ReducedFrom => null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<CustomModifier> IMethodSymbol.ReturnTypeCustomModifiers => ImmutableArray<CustomModifier>.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<ITypeSymbol> IMethodSymbol.TypeArguments => ImmutableArray<ITypeSymbol>.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ImmutableArray<ITypeParameterSymbol> IMethodSymbol.TypeParameters => ImmutableArray<ITypeParameterSymbol>.Empty;


        ITypeSymbol IMethodSymbol.GetTypeInferredDuringReduction(ITypeParameterSymbol reducedFromTypeParameter)
        {
            throw new InvalidOperationException();
        }

        IMethodSymbol IMethodSymbol.ReduceExtensionMethod(ITypeSymbol receiverType)
        {
            return null;
        }

        ImmutableArray<AttributeData> IMethodSymbol.GetReturnTypeAttributes()
        {
            return ImmutableArray<AttributeData>.Empty;
        }

        IMethodSymbol IMethodSymbol.Construct(params ITypeSymbol[] typeArguments)
        {
            throw new InvalidOperationException();
        }

        DllImportData IMethodSymbol.GetDllImportData()
        {
            return null;
        }
    }
}
