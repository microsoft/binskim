// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis.BinSkim.Rules;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.BinSkim
{
    internal class ExportCommand : DriverCommand<ExportOptions>
    {
        private static Assembly[] s_defaultCompositionAssemblies =
                                        new Assembly[] {
                                            typeof(DoNotShipVulnerableBinaries).Assembly
                                        };

        public override int Run(ExportOptions exportOptions)
        {
            int result = FAILED;
            PropertyBag allOptions = new PropertyBag();

            // The export command could be updated in the future to accept an arbitrary set
            // of analyzers for which to build an options XML file suitable for configuring them.
            // Currently, we perform discovery against the built-in CodeFormatter rules
            // and analyzers only.
            foreach (IOptionsProvider provider in GetOptionsProviders(s_defaultCompositionAssemblies))
            {
                foreach (IOption option in provider.GetOptions())
                {
                    allOptions.SetProperty(option, option.DefaultValue);
                }
            }
            allOptions.SaveTo(exportOptions.OutputPath, id: "binskim-policy");
            Console.WriteLine("Options file saved to: " + Path.GetFullPath(exportOptions.OutputPath));

            result = SUCCEEDED;

            return result;
        }

        public static ImmutableArray<IOptionsProvider> GetOptionsProviders(IEnumerable<Assembly> assemblies)
        {
            var container = CreateCompositionContainer(assemblies);
            return container.GetExports<IOptionsProvider>().ToImmutableArray();
        }

        private static CompositionHost CreateCompositionContainer(IEnumerable<Assembly> assemblies = null)
        {
            ConventionBuilder conventions = GetConventions();

            assemblies = assemblies ?? new Assembly[] { typeof(DoNotShipVulnerableBinaries).Assembly };

            return new ContainerConfiguration()
                .WithAssemblies(assemblies, conventions)
                .CreateContainer();
        }

        private static ConventionBuilder GetConventions()
        {
            var conventions = new ConventionBuilder();

            // New per-analyzer options mechanism 
            conventions.ForTypesDerivedFrom<IOptionsProvider>()
                .Export<IOptionsProvider>();

            return conventions;
        }
    }
}
