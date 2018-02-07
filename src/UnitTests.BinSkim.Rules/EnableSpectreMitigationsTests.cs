// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.IL.Sdk;
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
            var context = new BinaryAnalyzerContext();
            context.Policy = new PropertiesDictionary();

            AddFakeConfigTestData(context.Policy);
            Version result;

            Version latestVersion = new Version(1, 0, 100, 5);

            result = EnableSpectreMitigations.GetMostCurrentCompilerVersionsWithSpectreMitigations(
                        context, ExtendedMachine.I386);

            result.Should().Equals(latestVersion);
        }

        [Fact]
        public void LoadCompilerDataFromConfig_ParsesAndCachesAsExpected()
        {
            var context = new BinaryAnalyzerContext();
            context.Policy = new PropertiesDictionary();

            AddFakeConfigTestData(context.Policy);

            Dictionary<MachineFamily, CompilerVersionToMitigation[]> result = EnableSpectreMitigations.LoadCompilerDataFromConfig(context.Policy);

            ValidateResultFromFakeTestData(result[MachineFamily.X86]);
            result.Should().ContainKeys(new MachineFamily[] { MachineFamily.X86, MachineFamily.Arm });
        }

        [Fact]
        public void CreateSortedVersionDictionary_ParsesAsExpected()
        {
            var versionList = GenerateMachineFamilyData();

            CompilerVersionToMitigation[] result = EnableSpectreMitigations.CreateSortedVersionDictionary(versionList);

            ValidateResultFromFakeTestData(result);
        }

        private void ValidateResultFromFakeTestData(CompilerVersionToMitigation[] results)
        {
            results.ShouldAllBeEquivalentTo(testData, "Loaded from test data.");
        }

        [Theory]
        [InlineData("1.0.0.0 - 1.*.*.*", "1.11.0.0 - 2.5.0.0")]
        [InlineData("1.0.0.0 - 1.8.0.0", "0.9.0.0 - 1.9.0.0")]
        [InlineData("1.0.0.0 - 1.8.0.0", "1.1.0.0 - 1.7.0.0")]
        public void CreateSortedVersionDictionary_OverlappingVersionRange_ThrowsException(string firstRange, string secondRange)
        {
            var versionList = new PropertiesDictionary();

            versionList.Add(firstRange, (CompilerMitigations.D2GuardSpecLoadAvailable).ToString());
            versionList.Add(secondRange, (CompilerMitigations.QSpectreAvailable).ToString());

            Assert.Throws<InvalidOperationException>(() => EnableSpectreMitigations.CreateSortedVersionDictionary(versionList));
        }

        [Fact]
        public void CreateSortedVersionDictionary_BadVersionRange_ThrowsException()
        {
            var versionList = new PropertiesDictionary();

            versionList.Add("8.0.0.0 - 1.0.0.0", (CompilerMitigations.D2GuardSpecLoadAvailable).ToString());

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
            var context = new BinaryAnalyzerContext();
            context.Policy = new PropertiesDictionary();

            AddFakeConfigTestData(context.Policy);

            CompilerMitigations actualMitigations = EnableSpectreMitigations.GetAvailableMitigations(context, machine, new Version(versionStr));
            Assert.Equal(expectedMitgations, actualMitigations);
        }

        private void AddFakeConfigTestData(PropertiesDictionary policy)
        {
            PropertiesDictionary BA2024Config = new PropertiesDictionary();
            PropertiesDictionary mitigatedCompilers = new PropertiesDictionary();
            PropertiesDictionary fakeX86Data = GenerateMachineFamilyData();

            PropertiesDictionary fakeArmData = new PropertiesDictionary();
            fakeArmData.Add("1.0.0.0 - 1.8.*.*", (CompilerMitigations.D2GuardSpecLoadAvailable | CompilerMitigations.QSpectreAvailable).ToString());
            fakeArmData.Add("2.0.0.0 - 2.0.0.*", (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable).ToString());
            fakeArmData.Add("2.0.1.0 - 2.*.*.*", (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable).ToString());

            mitigatedCompilers.Add(MachineFamily.X86.ToString(), fakeX86Data);
            mitigatedCompilers.Add(MachineFamily.Arm.ToString(), fakeArmData);


            BA2024Config.Add("MitigatedCompilers", mitigatedCompilers);
            policy.Add("BA2024.EnableSpectreMitigations.Options", BA2024Config);
        }

        private PropertiesDictionary GenerateMachineFamilyData()
        {
            PropertiesDictionary data = new PropertiesDictionary();

            foreach(var entry in testData)
            {
                AddCompilerMitigationDataToDictionary(data, entry);
            }

            return data;
        }

        private CompilerVersionToMitigation[] testData = new CompilerVersionToMitigation[]
        {
            new CompilerVersionToMitigation()
            {
                Start = new Version(1, 0, 100, 5),
                End = new Version(1, 10, int.MaxValue, int.MaxValue),
                MitigationState = CompilerMitigations.QSpectreAvailable,
            },
            new CompilerVersionToMitigation()
            {
                Start = new Version(1, 11, 250, 0),
                End = new Version(1, 11, 250, 500),
                MitigationState = CompilerMitigations.QSpectreAvailable,
            },
            new CompilerVersionToMitigation()
            {
                Start = new Version(1, 11, 250, 501),
                End = new Version(1, 11, 250, int.MaxValue),
                MitigationState = CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.D2GuardSpecLoadAvailable,
            },
            new CompilerVersionToMitigation()
            {
                Start = new Version(2, 0, 0, 0),
                End = new Version(2, int.MaxValue, int.MaxValue, int.MaxValue),
                MitigationState = (CompilerMitigations.NonoptimizedCodeMitigated | CompilerMitigations.QSpectreAvailable),
            }
        };
        
        private void AddCompilerMitigationDataToDictionary(PropertiesDictionary dictionary, CompilerVersionToMitigation data)
        {
            string start = data.Start.ToString().Replace(int.MaxValue.ToString(), "*");
            string end = data.End.ToString().Replace(int.MaxValue.ToString(), "*");
            string key = start + " - " + end;
            dictionary.Add(key, data.MitigationState.ToString());
        }
    }
}