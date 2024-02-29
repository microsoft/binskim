// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class RulePropertyTests
    {
        private static readonly string[] ExpectedLogPdbLoadExceptionRules = new string[]
        {
            "BA2002.DoNotIncorporateVulnerableDependencies",
            "BA2006.BuildWithSecureTools",
            "BA2007.EnableCriticalCompilerWarnings",
            "BA2011.EnableStackProtection",
            "BA2013.InitializeStackProtection",
            "BA2014.DoNotDisableStackProtectionForFunctions",
            "BA2024.EnableSpectreMitigations",
            "BA2025.EnableShadowStack",
            "BA2026.EnableMicrosoftCompilerSdlSwitch",
            "BA6001.DisableIncrementalLinkingInReleaseBuilds",
            "BA6002.EliminateDuplicateStrings",
            "BA6004.EnableComdatFolding",
            "BA6005.EnableOptimizeReferences",
            "BA6006.EnableLinkTimeCodeGeneration"
        };

        [Fact]
        public void RulePropertyTests_LogPdbLoadException()
        {
            WindowsBinaryAndPdbSkimmerBase[] skimmers =
                GetAllWindowsBinaryAndPdbSkimmers("BinSkim.Rules.dll");
            IEnumerable<WindowsBinaryAndPdbSkimmerBase> actualLogPdbLoadExceptionRules =
                skimmers.Where(s => s.LogPdbLoadException);
            IEnumerable<WindowsBinaryAndPdbSkimmerBase> unexpectedLogPdbLoadExceptionRules =
                actualLogPdbLoadExceptionRules.Where(s => !ExpectedLogPdbLoadExceptionRules.Contains(s.Moniker));

            if (unexpectedLogPdbLoadExceptionRules.Any())
            {
                Assert.Fail(string.Format("Please examine if the following rules should enable 'LogPdbLoadException': {0}",
                                          string.Join(", ", unexpectedLogPdbLoadExceptionRules.Select(skimmer => skimmer.Moniker))));
            }
        }

        private static WindowsBinaryAndPdbSkimmerBase[] GetAllWindowsBinaryAndPdbSkimmers(string rulesAssemblyName)
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            string assemblyPath = Path.Combine(directory, rulesAssemblyName);
            var assembly = Assembly.LoadFrom(assemblyPath);
            Type[] assemblyTypes = assembly.GetTypes();
            IEnumerable<Type> inheritanceTypes =
                assemblyTypes.Where(t => t.BaseType == typeof(WindowsBinaryAndPdbSkimmerBase));
            IEnumerable<WindowsBinaryAndPdbSkimmerBase> instances =
                inheritanceTypes.Select(t => (WindowsBinaryAndPdbSkimmerBase)Activator.CreateInstance(t));
            return instances.ToArray();
        }
    }
}
