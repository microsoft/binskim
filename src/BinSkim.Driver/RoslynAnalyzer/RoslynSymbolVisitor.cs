// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.CodeAnalysis.IL
{
    internal sealed class RoslynSymbolVisitor : SymbolVisitor
    {
        private readonly Action<ISymbol> _action;

        public RoslynSymbolVisitor(Action<ISymbol> action)
        {
            _action = action;
        }

        public override void DefaultVisit(ISymbol symbol)
        {
            _action(symbol);
        }

        public override void VisitAssembly(IAssemblySymbol symbol)
        {
            foreach (var module in symbol.Modules)
            {
                Visit(module);
            }

            base.VisitAssembly(symbol);
        }

        public override void VisitModule(IModuleSymbol symbol)
        {
            Visit(symbol.GlobalNamespace);

            base.VisitModule(symbol);
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                Visit(member);
            }

            base.VisitNamespace(symbol);
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                Visit(member);
            }

            base.VisitNamedType(symbol);
        }

        public static void Visit(ISymbol root, Action<ISymbol> action)
        {
            new RoslynSymbolVisitor(action).Visit(root);
        }
    }
}
