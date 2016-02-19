// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.IL
{
    /// <summary>
    /// Basic analysis context provided to Roslyn analyzers for operation start registrations. 
    /// These actions will subsequently be invoked as we visit the IL of all analysis targets.
    /// </summary>
    internal sealed class RoslynOperationBlockStartAnalysisContext : OperationBlockStartAnalysisContext
    {
        public ActionMap<OperationAnalysisContext, OperationKind> OperationActions { get; }
        public Action<OperationBlockAnalysisContext> OperationBlockEndActions { get; private set; }

        public RoslynOperationBlockStartAnalysisContext(ImmutableArray<IOperation> operationBlocks, ISymbol owningSymbol, Compilation compilation, AnalyzerOptions options, CancellationToken cancellationToken)
            : base(operationBlocks, owningSymbol, compilation, options, cancellationToken)
        {
            OperationActions = new ActionMap<OperationAnalysisContext, OperationKind>();
        }

        public override void RegisterOperationBlockEndAction(Action<OperationBlockAnalysisContext> action)
        {
            OperationBlockEndActions += action;
        }

        public override void RegisterOperationAction(Action<OperationAnalysisContext> action, ImmutableArray<OperationKind> operationKinds)
        {
            OperationActions.Add(action, operationKinds);
        }
    }
}
