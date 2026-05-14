// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        public override IEnumerable<Assembly> DefaultPluginAssemblies
        {
            get
            {
                var assemblies = new List<Assembly>
                {
                    typeof(MarkImageAsNXCompatible).Assembly,
                    typeof(ElfBinary).Assembly // Faults in BinaryParsers configuration.
                };
                
                // Try to load internal rules if available and not disabled by environment variable
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BINSKIM_DISABLE_INTERNAL_RULES_AUTOLOAD")))
                {
                    try
                    {
                        // Try loading from the same directory as this exe
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            string dllPath = Path.Combine(Path.GetDirectoryName(exePath), "BinSkim.Rules.Internal.dll");
                            if (File.Exists(dllPath))
                            {
                                Assembly internalRulesAsm = Assembly.LoadFrom(dllPath);
                                assemblies.Add(internalRulesAsm);
                            }
                        }
                    }
                    catch { } // Silently ignore if internal rules unavailable
                }
                
                return assemblies;
            }
        }

        public override IOptionsProvider AdditionalOptionsProvider => new BinaryAnalyzerContext();
    }
}
