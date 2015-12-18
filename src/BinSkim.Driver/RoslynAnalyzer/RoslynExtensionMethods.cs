// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    internal static class RoslynExtensionMethods
    {
        public static ResultKind ConvertToMessageKind(this DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Hidden:
                case DiagnosticSeverity.Warning:
                {
                    return ResultKind.Error;
                }

                case DiagnosticSeverity.Error:
                {
                    return ResultKind.Error;
                }

                case DiagnosticSeverity.Info:
                {
                    return ResultKind.Note;
                }

                default: 
                {
                    throw new InvalidOperationException("Unrecognized diagnostic severity value: " + severity.ToString());
                }
            }
        }
    }
}
