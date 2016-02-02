// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool ISymbol.CanBeReferencedByName => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IAssemblySymbol ISymbol.ContainingAssembly => ContainingMethod.ContainingAssembly;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IModuleSymbol ISymbol.ContainingModule => ContainingMethod.ContainingModule;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INamespaceSymbol ISymbol.ContainingNamespace => ContainingMethod.ContainingNamespace;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        INamedTypeSymbol ISymbol.ContainingType => ContainingMethod.ContainingType;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        ISymbol ISymbol.ContainingSymbol => ContainingMethod;

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

    internal sealed class LabelSymbol : LocalOrLabelSymbol, ILabelSymbol
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
}
