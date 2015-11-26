using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    class TestRoslynAnalyzer : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(id: "T1001",
                                                                             title: "Symbol named reporter",
                                                                             messageFormat: "Symbol encountered in MSIL '{0}'",
                                                                             category: "Test",
                                                                             defaultSeverity: DiagnosticSeverity.Info,
                                                                             isEnabledByDefault: true,
                                                                             description: "Symbol encountered in MSIL '{0}",
                                                                             helpLinkUri: null,
                                                                             customTags: WellKnownDiagnosticTags.NotConfigurable);

        private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = ImmutableArray.Create(Rule);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return s_supportedDiagnostics;
            }
        }

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.RegisterCompilationStartAction(compilationContext =>
            {
                compilationContext.RegisterSymbolAction(context =>
                {
                    AnalyzeSymbol((INamedTypeSymbol)context.Symbol, context.ReportDiagnostic);
                },
                SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol(INamedTypeSymbol namedType, Action<Diagnostic> addDiagnostic)
        {

            // Non-sealed non-abstract attribute type.
            addDiagnostic(CreateDiagnostic(namedType.MetadataName));
        }

        public static Diagnostic CreateDiagnostic(
            params object[] args)
        {
            return Diagnostic.Create(Rule,
                     location: null,
                     additionalLocations: null,
                     messageArgs: args);
        }
    }
}
