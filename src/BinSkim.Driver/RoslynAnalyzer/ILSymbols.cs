// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;

// TODO: Figure out versioning here. Either we have to move in to Roslyn 
// or Roslyn needs to provide a version-safe API for generating symbols.
// ...Or is there an existing API that I'm missing?

#pragma warning disable RS1009 // Only internal implementations of this interface are allowed.

namespace Microsoft.CodeAnalysis.IL
{
    internal abstract class LocalOrLabelSymbol : ISymbol
    {
        protected LocalOrLabelSymbol(string name, IMethodSymbol containingMethod)
        {
            Name = name;
            ContainingMethod = containingMethod;
        }

        public IMethodSymbol ContainingMethod { get; }
        public string Name { get; }
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
        bool ISymbol.CanBeReferencedByName => false;
        IAssemblySymbol ISymbol.ContainingAssembly => ContainingMethod.ContainingAssembly;
        IModuleSymbol ISymbol.ContainingModule => ContainingMethod.ContainingModule;
        INamespaceSymbol ISymbol.ContainingNamespace => ContainingMethod.ContainingNamespace;
        INamedTypeSymbol ISymbol.ContainingType => ContainingMethod.ContainingType;
        ISymbol ISymbol.ContainingSymbol => ContainingMethod;
        Accessibility ISymbol.DeclaredAccessibility => Accessibility.NotApplicable;
        ImmutableArray<SyntaxReference> ISymbol.DeclaringSyntaxReferences => ImmutableArray<SyntaxReference>.Empty;
        bool ISymbol.HasUnsupportedMetadata => false;
        bool ISymbol.IsAbstract => false;
        bool ISymbol.IsDefinition => true;
        bool ISymbol.IsExtern => false;
        bool ISymbol.IsImplicitlyDeclared => true;
        bool ISymbol.IsOverride => false;
        bool ISymbol.IsSealed => false;
        bool ISymbol.IsStatic => false;
        bool ISymbol.IsVirtual => false;
        string ISymbol.Language => "IL";
        ImmutableArray<Location> ISymbol.Locations => ImmutableArray<Location>.Empty;
        string ISymbol.MetadataName => Name;
        ISymbol ISymbol.OriginalDefinition => this;
        ImmutableArray<AttributeData> ISymbol.GetAttributes() => ImmutableArray<AttributeData>.Empty;
        string ISymbol.GetDocumentationCommentId() => null;
        string ISymbol.GetDocumentationCommentXml(CultureInfo preferredCulture, bool expandIncludes, CancellationToken cancellationToken) => null;
    }

    internal sealed class LocalSymbol : LocalOrLabelSymbol, ILocalSymbol
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

        object ILocalSymbol.ConstantValue => null;
        bool ILocalSymbol.HasConstantValue => false;
        bool ILocalSymbol.IsConst => false;
        bool ILocalSymbol.IsFunctionValue => false;
    }

    internal sealed class LabelSymbol : LocalOrLabelSymbol, ILabelSymbol
    {
        public LabelSymbol(int offset, IMethodSymbol containingMethod)
            : base(GenerateName(offset), containingMethod)
        {
        }

        public override SymbolKind Kind => SymbolKind.Label;

        private static string GenerateName(int offset)
        {
            return $"IL{offset:X}";
        }

        public override void Accept(SymbolVisitor visitor)
        {
            visitor.VisitLabel(this);
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            return visitor.VisitLabel(this);
        }
    }
}
