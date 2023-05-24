// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Net;
using System.Net.Http;
using System.Reflection;

using Microsoft.CodeAnalysis.IL.Rules;
using Microsoft.CodeAnalysis.Sarif;

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

        public static IList<string> GetInaccessibleSymbolPaths(string symbolPathString)
        {
            IList<string> inaccessibleList = new List<string>();
            if (string.IsNullOrWhiteSpace(symbolPathString)) { return inaccessibleList; }

            string[] symbolServers = Array.FindAll(symbolPathString.Split('*'), path =>
            path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

            foreach (string symbolServer in symbolServers)
            {
                try
                {
                    var httpClient = new HttpClientWrapper();
                    HttpResponseMessage httpResponseMessage = httpClient.GetAsync(symbolServer).GetAwaiter().GetResult();

                    if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
                    {
                        inaccessibleList.Add(symbolServer);
                    }
                }
                catch
                {
                    inaccessibleList.Add(symbolServer);
                }
            }

            return inaccessibleList;
        }
    }
}
