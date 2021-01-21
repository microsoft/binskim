// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public class EnableSpectreMitigationsTests
    {
        public EnableSpectreMitigationsTests()
        {
            // Reset the static cache of compiler data before each test, because
            // we use a couple of different Policy configurations in testing.
            EnableSpectreMitigations.compilerData = null;
        }

        [Fact]
        public void LoadCompilerDataFromConfig_ParsesAndCachesAsExpected()
        {
            var context = new BinaryAnalyzerContext
            {
                Policy = new PropertiesDictionary()
            };

            this.AddFakeConfigTestData(context.Policy);

            Dictionary<MachineFamily, CompilerVersionToMitigation[]> result = EnableSpectreMitigations.LoadCompilerDataFromConfig(context.Policy);

            this.ValidateResultFromFakeTestData(result[MachineFamily.X86]);
            result.Should().ContainKeys(new MachineFamily[] { MachineFamily.X86, MachineFamily.Arm });
        }

        [Fact]
        public void CreateSortedVersionDictionary_ParsesAsExpected()
        {
            PropertiesDictionary versionList = this.GenerateMachineFamilyData();

            CompilerVersionToMitigation[] result = EnableSpectreMitigations.CreateSortedVersionDictionary(versionList);

            this.ValidateResultFromFakeTestData(result);
        }

        private void ValidateResultFromFakeTestData(CompilerVersionToMitigation[] results)
        {
            results.Should().BeEquivalentTo(this.testData, "Loaded from test data.");
        }

        [Theory]
        [InlineData("1.0.0.0 - 1.*.*.*", "1.11.0.0 - 2.5.0.0")]
        [InlineData("1.0.0.0 - 1.8.0.0", "0.9.0.0 - 1.9.0.0")]
        [InlineData("1.0.0.0 - 1.8.0.0", "1.1.0.0 - 1.7.0.0")]
        public void CreateSortedVersionDictionary_OverlappingVersionRange_ThrowsException(string firstRange, string secondRange)
        {
            var versionList = new PropertiesDictionary
            {
                { firstRange, (CompilerMitigations.D2GuardSpecLoadAvailable).ToString() },
                { secondRange, (CompilerMitigations.QSpectreAvailable).ToString() }
            };

            Assert.Throws<InvalidOperationException>(() => EnableSpectreMitigations.CreateSortedVersionDictionary(versionList));
        }

        [Fact]
        public void CreateSortedVersionDictionary_BadVersionRange_ThrowsException()
        {
            var versionList = new PropertiesDictionary
            {
                { "8.0.0.0 - 1.0.0.0", (CompilerMitigations.D2GuardSpecLoadAvailable).ToString() }
            };

            Assert.Throws<InvalidOperationException>(() => EnableSpectreMitigations.CreateSortedVersionDictionary(versionList));
        }

        [Theory]
        [InlineData("0.0.0.1", ExtendedMachine.I386, CompilerMitigations.None)]
        [InlineData("0.0.0.1", ExtendedMachine.Amd64, CompilerMitigations.None)]
        [InlineData("2.0.0.500", ExtendedMachine.Arm, (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable))]
        [InlineData("2.0.1.0", ExtendedMachine.Arm64, (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable))]
        [InlineData("1.11.250.502", ExtendedMachine.I386, (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.D2GuardSpecLoadAvailable))]
        [InlineData("2.500.0.0", ExtendedMachine.I386, (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable))]
        [InlineData("100.0.0.0", ExtendedMachine.I386, CompilerMitigations.None)]
        public void GetCompilerData_VersionPresent_WorksAsExpected(string versionStr, ExtendedMachine machine, CompilerMitigations expectedMitgations)
        {
            var context = new BinaryAnalyzerContext
            {
                Policy = new PropertiesDictionary()
            };

            this.AddFakeConfigTestData(context.Policy);

            CompilerMitigations actualMitigations = EnableSpectreMitigations.GetAvailableMitigations(context, machine, new Version(versionStr));
            Assert.Equal(expectedMitgations, actualMitigations);
        }

        [Theory]
        [InlineData("0.0.0.1", ExtendedMachine.I386, "1.0.100.5")]
        [InlineData("1.12.0.0", ExtendedMachine.Amd64, "2.0.0.0")]
        [InlineData("3.0.0.0", ExtendedMachine.I386, "2.0.0.0")]
        [InlineData("0.0.0.1", ExtendedMachine.Arm, "1.0.0.0")]
        [InlineData("1.9.0.0", ExtendedMachine.Arm, "2.0.0.0")]
        [InlineData("3.0.0.0", ExtendedMachine.Arm, "2.0.1.0")]
        public void GetNextCompilerVersionUp_WithSpectreMitigations_WorksAsExpected(string firstVersionStr, ExtendedMachine machine, string expectedVersionStr)
        {
            var actualVersion = new Version(firstVersionStr);

            var context = new BinaryAnalyzerContext
            {
                Policy = new PropertiesDictionary()
            };

            this.AddFakeConfigTestData(context.Policy);

            Version nextUp = EnableSpectreMitigations.GetClosestCompilerVersionWithSpectreMitigations(context, machine, actualVersion);

            nextUp.Should().BeEquivalentTo(new Version(expectedVersionStr));
        }

        [Fact]
        public void GetClosestCompilerVersionWithSpectreMitigations_UnsupportedOnMachine()
        {
            var context = new BinaryAnalyzerContext
            {
                Policy = new PropertiesDictionary()
            };
            var BA2024Config = new PropertiesDictionary();
            var mitigatedCompilers = new PropertiesDictionary();
            PropertiesDictionary fakeX86Data = this.GenerateMachineFamilyData();

            mitigatedCompilers.Add(nameof(MachineFamily.X86), fakeX86Data);

            BA2024Config.Add("MitigatedCompilers", mitigatedCompilers);
            context.Policy.Add("BA2024.EnableSpectreMitigations.Options", BA2024Config);

            Assert.Null(EnableSpectreMitigations.GetClosestCompilerVersionWithSpectreMitigations(context, ExtendedMachine.Arm, new Version(1, 0, 100, 5)));
        }

        private void AddFakeConfigTestData(PropertiesDictionary policy)
        {
            var BA2024Config = new PropertiesDictionary();
            var mitigatedCompilers = new PropertiesDictionary();
            PropertiesDictionary fakeX86Data = this.GenerateMachineFamilyData();

            var fakeArmData = new PropertiesDictionary
            {
                { "1.0.0.0 - 1.8.*.*", (CompilerMitigations.D2GuardSpecLoadAvailable | CompilerMitigations.QSpectreAvailable).ToString() },
                { "2.0.0.0 - 2.0.0.*", (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable).ToString() },
                { "2.0.1.0 - 2.*.*.*", (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable).ToString() }
            };

            mitigatedCompilers.Add(nameof(MachineFamily.X86), fakeX86Data);
            mitigatedCompilers.Add(nameof(MachineFamily.Arm), fakeArmData);

            BA2024Config.Add("MitigatedCompilers", mitigatedCompilers);
            policy.Add("BA2024.EnableSpectreMitigations.Options", BA2024Config);
        }

        private PropertiesDictionary GenerateMachineFamilyData()
        {
            var data = new PropertiesDictionary();

            foreach (CompilerVersionToMitigation entry in this.testData)
            {
                this.AddCompilerMitigationDataToDictionary(data, entry);
            }

            return data;
        }

        private readonly CompilerVersionToMitigation[] testData = new CompilerVersionToMitigation[]
        {
            new CompilerVersionToMitigation()
            {
                MinimalSupportedVersion = new Version(1, 0, 100, 5),
                MaximumSupportedVersion = new Version(1, 10, int.MaxValue, int.MaxValue),
                SupportedMitigations = CompilerMitigations.QSpectreAvailable,
            },
            new CompilerVersionToMitigation()
            {
                MinimalSupportedVersion = new Version(1, 11, 250, 0),
                MaximumSupportedVersion = new Version(1, 11, 250, 500),
                SupportedMitigations = CompilerMitigations.QSpectreAvailable,
            },
            new CompilerVersionToMitigation()
            {
                MinimalSupportedVersion = new Version(1, 11, 250, 501),
                MaximumSupportedVersion = new Version(1, 11, 250, int.MaxValue),
                SupportedMitigations = CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.D2GuardSpecLoadAvailable,
            },
            new CompilerVersionToMitigation()
            {
                MinimalSupportedVersion = new Version(2, 0, 0, 0),
                MaximumSupportedVersion = new Version(2, int.MaxValue, int.MaxValue, int.MaxValue),
                SupportedMitigations = (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable),
            }
        };

        private void AddCompilerMitigationDataToDictionary(PropertiesDictionary dictionary, CompilerVersionToMitigation data)
        {
            string start = data.MinimalSupportedVersion.ToString().Replace(int.MaxValue.ToString(), "*");
            string end = data.MaximumSupportedVersion.ToString().Replace(int.MaxValue.ToString(), "*");
            string key = start + " - " + end;
            dictionary.Add(key, data.SupportedMitigations.ToString());
        }
    }
}
