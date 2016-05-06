// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Sarif;
using System.Reflection;

namespace Microsoft.CodeAnalysis.IL
{
    internal enum MetadataImportOptions : byte
    {
        /// <summary>  
        /// Only import public and protected symbols.  
        /// </summary>  
        Public = 0,

        /// <summary>  
        /// Import public, protected and internal symbols.  
        /// </summary>  
        Internal = 1,

        /// <summary>  
        /// Import all symbols.
        /// </summary>
        All = 2,
    }

    internal static class RoslynExtensionMethods
    {
        // Temporary workaround while blocked on https://github.com/dotnet/roslyn/issues/6748  
        // This method is only called once. No fancy tricks required for speed.
        public static void SetMetadataImportOptions(this CompilationOptions instance, MetadataImportOptions options)
        {
            typeof(CompilationOptions)
                .GetProperty(nameof(MetadataImportOptions), BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(instance, options);
        }

        public static IRule ConvertToRuleDescriptor(this Diagnostic diagnostic)
        {
            // TODO we should consume the standard Roslyn->SARIF emit code here.

            DiagnosticDescriptor diagnosticDescriptor = diagnostic.Descriptor;

            var rule = new Rule();
            rule.MessageFormats = new Dictionary<string, string>();
            rule.MessageFormats["Default"] = diagnosticDescriptor.MessageFormat.ToString();
            rule.FullDescription = diagnosticDescriptor.Description.ToString();

            if (!String.IsNullOrEmpty(diagnosticDescriptor.HelpLinkUri))
            {
            rule.HelpUri = new Uri(diagnosticDescriptor.HelpLinkUri);
            }

            rule.Id = diagnosticDescriptor.Id;

            // TODO: review this decision
            rule.Name = diagnostic.GetType().Name;

            rule.Properties = new Dictionary<string, string>();

            foreach (string tag in diagnosticDescriptor.CustomTags)
            {
                rule.Properties[tag] = String.Empty;
            }

            rule.Properties["Category"] = diagnosticDescriptor.Category;
            rule.Properties["DefaultSeverity"] = diagnosticDescriptor.DefaultSeverity.ToString();
            rule.Properties["IsEnabledByDefault"] = diagnosticDescriptor.IsEnabledByDefault.ToString();

            rule.ShortDescription = diagnosticDescriptor.Title.ToString();

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

        public static ResultLevel ConvertToMessageKind(this DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Error:
                {
                    return ResultLevel.Error;
                }

                case DiagnosticSeverity.Hidden:
                case DiagnosticSeverity.Warning:
                {
                    return ResultLevel.Warning;
                }

                case DiagnosticSeverity.Info:
                {
                    return ResultLevel.Note;
                }

                default: 
                {
                    throw new InvalidOperationException("Unrecognized diagnostic severity value: " + severity.ToString());
                }
            }
        }
    }
}
