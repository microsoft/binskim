// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;
using Xunit.Abstractions;
using Microsoft.CodeAnalysis.Sarif;
using FluentAssertions;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class EnableSpectreMitigationsTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public EnableSpectreMitigationsTests(ITestOutputHelper output)
        {
            _testOutputHelper = output;
        }

        [Fact]
        public void GetMostCurrentCompilerVersionWithSpectreMitigations()
        {
            Version result;

            var context = new BinaryAnalyzerContext();
            context.Policy = new PropertiesDictionary();


            Version latestVersion = new Version(19, 13, 26029, 0);

            result = EnableSpectreMitigations.GetMostCurrentCompilerVersionsWithSpectreMitigations(
                        context, ExtendedMachine.I386);

            result.Should().Equals(latestVersion);

            // Version should now be cached in context object
            PropertiesDictionary properties;

            var newVersion = new Version(20, 1, 1, 1);
            string versionKey = EnableSpectreMitigations.BuildPropertiesKeyFromVersion(newVersion);

            properties = context.Policy.GetProperty(EnableSpectreMitigations.MitigatedCompilers);

            // Create new data rooted by new Major.Minor versioning vector
            properties[versionKey] = new PropertiesDictionary();
            properties = (PropertiesDictionary)properties[versionKey];

            // Insert mitigation data for x86 family
            properties["X86"] = new PropertiesDictionary();
            properties = (PropertiesDictionary)properties["X86"];

            // We only get minimum D2GuardSpecLoadAvailableVersion data
            // As this value represents the minimal supported version for
            // the most recent compiler toolchain. This logic will need to
            // be adjusted once /qSpectre support is broadly available.
            properties[EnableSpectreMitigations.MinimumD2GuardSpecLoadAvailableVersion.Name] = latestVersion;

            // Verify that insertion of more recent compiler doesn't impact helper,
            // because we should be retrieving the cached data.
            result = EnableSpectreMitigations.GetMostCurrentCompilerVersionsWithSpectreMitigations(
                        context, ExtendedMachine.I386);

            result.Should().Equals(latestVersion);

            // Now clear the cached value and verify the more recent compiler 
            // version is returned.
            context.Policy.Remove(EnableSpectreMitigations.MostCurrentSpectreSupportingCompilerVersion.Name);

            result = EnableSpectreMitigations.GetMostCurrentCompilerVersionsWithSpectreMitigations(
                        context, ExtendedMachine.I386);

            result.Should().Equals(newVersion);
        }
    }
}