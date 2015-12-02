// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL
{
    internal static class RoslynExtensionMethods
    {
        public static MessageKind ConvertToMessageKind(this DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Hidden:
                case DiagnosticSeverity.Warning:
                case DiagnosticSeverity.Error:
                {
                    return MessageKind.Fail;
                }

                case DiagnosticSeverity.Info:
                {
                    return MessageKind.Note;
                }

                default: 
                {
                    throw new InvalidOperationException("Unrecognized diagnostic severity value: " + severity.ToString());
                }
            }
        }
    }
}
