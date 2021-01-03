// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.IL
{
    internal sealed class RoslynSymbolVisitor : SymbolVisitor
    {
        private readonly Action<ISymbol> action;

        public RoslynSymbolVisitor(Action<ISymbol> action)
        {
            this.action = action;
        }

        public override void DefaultVisit(ISymbol symbol)
        {
            this.action(symbol);
        }

        public override void VisitAssembly(IAssemblySymbol symbol)
        {
            foreach (IModuleSymbol module in symbol.Modules) { this.Visit(module); }
            base.VisitAssembly(symbol);
        }

        public override void VisitModule(IModuleSymbol symbol)
        {
            this.Visit(symbol.GlobalNamespace);
            base.VisitModule(symbol);
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (INamespaceOrTypeSymbol member in symbol.GetMembers()) { this.Visit(member); }
            base.VisitNamespace(symbol);
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            foreach (ISymbol member in symbol.GetMembers()) { this.Visit(member); }
            base.VisitNamedType(symbol);
        }
    }
}
