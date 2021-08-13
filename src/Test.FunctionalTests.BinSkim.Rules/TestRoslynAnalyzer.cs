// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class TestRoslynAnalyzer : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor SymbolNameRule = new DiagnosticDescriptor(id: "T1001",
                                                                             title: "Symbol name reporter",
                                                                             messageFormat: "Symbol encountered in MSIL '{0}'",
                                                                             category: "Test",
                                                                             defaultSeverity: DiagnosticSeverity.Info,
                                                                             isEnabledByDefault: true,
                                                                             description: "Symbol name reporting rule.",
                                                                             helpLinkUri: null,
                                                                             customTags: WellKnownDiagnosticTags.NotConfigurable);

        internal static DiagnosticDescriptor CallbackReportingRule = new DiagnosticDescriptor(id: "T1002",
                                                                      title: "Roslyn callback reporter",
                                                                      messageFormat: "Roslyn '{0}' callback invoked analyzing '{1}'",
                                                                      category: "Test",
                                                                      defaultSeverity: DiagnosticSeverity.Info,
                                                                      isEnabledByDefault: true,
                                                                      description: "Roslyn callback reporting rule.",
                                                                      helpLinkUri: null,
                                                                      customTags: WellKnownDiagnosticTags.NotConfigurable);

        private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = ImmutableArray.Create(SymbolNameRule, CallbackReportingRule);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.RegisterCompilationStartAction(compilationContext =>
            {
                // The compilation start action cannot report a diagnostic,
                // so we do not report T1002 here.

                compilationContext.RegisterSymbolAction(context =>
                    // We do not report a callback here, as the analysis itself serves.
                    AnalyzeSymbol((INamedTypeSymbol)context.Symbol, context.ReportDiagnostic),
                SymbolKind.NamedType);

                compilationContext.RegisterCompilationEndAction(context =>
                {
                    string targetName = Path.GetFileName(context.Compilation.References.First().Display);
                    context.ReportDiagnostic(
                        CreateDiagnostic(CallbackReportingRule, "RegisterCompilationEndAction", targetName));
                });
            });

            analysisContext.RegisterCompilationAction(context =>
            {
                string targetName = Path.GetFileName(context.Compilation.References.First().Display);
                context.ReportDiagnostic(
                    CreateDiagnostic(CallbackReportingRule, "RegisterCompilationAction", targetName));
            });
        }

        private static void AnalyzeSymbol(INamedTypeSymbol namedType, Action<Diagnostic> addDiagnostic)
        {
            string symbolName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            addDiagnostic(CreateDiagnostic(SymbolNameRule, symbolName));
        }

        public static Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, params object[] args)
        {
            return Diagnostic.Create(descriptor,
                     location: null,
                     additionalLocations: null,
                     messageArgs: args);
        }
    }
}
