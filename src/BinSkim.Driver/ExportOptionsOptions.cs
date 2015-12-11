// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CommandLine;

namespace Microsoft.CodeAnalysis.IL
{
    [Verb("exportOptions", HelpText = "Export rule options to an XML file that can be edited and used to configure subsequent analysis.")]
    internal class ExportOptionsOptions
    {
        [Value(0, HelpText = "Output path for exported analysis options", Required = true)]
        public string OutputFilePath { get; set; }
    }
}
