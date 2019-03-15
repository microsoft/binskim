// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Sarif;

namespace Microsoft.CodeAnalysis.IL
{
    internal static class RoslynExtensionMethods
    {
        public static ReportingDescriptor ConvertToRuleDescriptor(this Diagnostic diagnostic)
        {
            // TODO we should consume the standard Roslyn->SARIF emit code here.

            DiagnosticDescriptor diagnosticDescriptor = diagnostic.Descriptor;

            var rule = new ReportingDescriptor();
            rule.MessageStrings = new Dictionary<string, MultiformatMessageString>();
            rule.MessageStrings["Default"] = new MultiformatMessageString { Text = diagnosticDescriptor.MessageFormat.ToString() };
            rule.FullDescription = new MultiformatMessageString { Text = diagnosticDescriptor.Description.ToString() };
            rule.HelpUri = new Uri(diagnosticDescriptor.HelpLinkUri);
            rule.Id = diagnosticDescriptor.Id;

            // TODO: review this decision
            rule.Name = diagnostic.GetType().Name;

            foreach (string tag in diagnosticDescriptor.CustomTags)
            {
                rule.Tags.Add(tag);
            }

            rule.DefaultConfiguration = new ReportingConfiguration();
            rule.DefaultConfiguration.Level = diagnosticDescriptor.DefaultSeverity.ConvertToRuleConfigurationDefaultLevel();

            rule.SetProperty("Category", diagnosticDescriptor.Category);
            rule.SetProperty("IsEnabledByDefault", diagnosticDescriptor.IsEnabledByDefault.ToString());

            rule.ShortDescription = new MultiformatMessageString { Text = diagnosticDescriptor.Title.ToString() };

            // No Roslyn analog for these available from diagnostic
            //rule.Options

            return rule;
        }

        public static Region ConvertToRegion(this Location location)
        {
            if (location == Location.None) { return null; }

            var region = new Region();

            FileLinePositionSpan flps = location.GetLineSpan();

            // Roslyn text position numbering is 0-based
            region.StartLine = flps.Span.Start.Line + 1;
            region.StartColumn = flps.Span.Start.Character + 1;
            region.EndLine = flps.Span.End.Line + 1;
            region.EndColumn = flps.Span.End.Character + 1;

            return region;
        }

        public static FailureLevel ConvertToRuleConfigurationDefaultLevel(this DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Error:
                {
                    return FailureLevel.Error;
                }

                case DiagnosticSeverity.Hidden:
                case DiagnosticSeverity.Warning:
                {
                    return FailureLevel.Warning;
                }

                case DiagnosticSeverity.Info:
                {
                    return FailureLevel.Note;
                }

                default:
                {
                    throw new InvalidOperationException("Unrecognized diagnostic severity value: " + severity.ToString());
                }
            }
        }
        public static FailureLevel ConvertToResultLevel(this DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Error:
                {
                    return FailureLevel.Error;
                }

                case DiagnosticSeverity.Hidden:
                case DiagnosticSeverity.Warning:
                {
                    return FailureLevel.Warning;
                }

                case DiagnosticSeverity.Info:
                {
                    return FailureLevel.Note;
                }

                default:
                {
                    throw new InvalidOperationException("Unrecognized diagnostic severity value: " + severity.ToString());
                }
            }
        }
    }
}
