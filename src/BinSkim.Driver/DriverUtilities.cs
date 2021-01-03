// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Reflection;

using Microsoft.CodeAnalysis.IL.Rules;

namespace Microsoft.CodeAnalysis.IL
{
    internal static class DriverUtilities
    {
        private static readonly Assembly[] s_defaultCompositionAssemblies =
                                        new Assembly[] {
                                            typeof(DoNotShipVulnerableBinaries).Assembly
                                        };

        public static ImmutableArray<T> GetExports<T>(IEnumerable<Assembly> assemblies = null)
        {
            CompositionHost container = CreateCompositionContainer<T>(assemblies ?? s_defaultCompositionAssemblies);
            return container.GetExports<T>().ToImmutableArray();
        }

        private static CompositionHost CreateCompositionContainer<T>(IEnumerable<Assembly> assemblies = null)
        {
            ConventionBuilder conventions = GetConventions<T>();

            assemblies = assemblies ?? new Assembly[] { typeof(DoNotShipVulnerableBinaries).Assembly };

            return new ContainerConfiguration()
                .WithAssemblies(assemblies, conventions)
                .CreateContainer();
        }

        private static ConventionBuilder GetConventions<T>()
        {
            var conventions = new ConventionBuilder();

            // New per-analyzer options mechanism 
            conventions.ForTypesDerivedFrom<T>()
                .Export<T>();

            return conventions;
        }
    }
}
