// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Reflection;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL
{
    internal class ExportConfigurationCommand : ExportConfigurationCommandBase
    {
        public override IEnumerable<Assembly> DefaultPluginAssemblies => new Assembly[] {
            typeof(MarkImageAsNXCompatible).Assembly,
            typeof(ElfBinary).Assembly // Faults in BinaryParsers configuration.
        };

        public override IOptionsProvider AdditionalOptionsProvider => new BinaryAnalyzerContext();
    }
}
