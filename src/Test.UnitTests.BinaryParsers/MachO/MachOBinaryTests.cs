// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.MachO
{
    public class MachOBinaryTests
    {
        internal static string TestData = GetTestDirectory("Test.UnitTests.BinaryParsers" + Path.DirectorySeparatorChar + "TestsData");

        internal static string GetTestDirectory(string relativeDirectory)
        {
            var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            string codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
            string dirPath = Path.GetDirectoryName(codeBasePath);
            dirPath = Path.Combine(dirPath, string.Format("..{0}..{0}..{0}..{0}src{0}", Path.DirectorySeparatorChar));
            dirPath = Path.GetFullPath(dirPath);
            return Path.Combine(dirPath, relativeDirectory);
        }

        [Fact]
        public void ValidateMachO_WithDwarf5()
        {
            // GNU C17 11.1.0 -fPIC -mmacosx-version-min=10.15.0 -mtune=core2 -gdwarf-5 -fstack-clash-protection
            string fileName = Path.Combine(TestData, "MachO/macho.gcc-lib.o");
            using var binary = new MachOBinary(new Uri(fileName));
            foreach (SingleMachOBinary macho in binary.MachOs)
            {
                macho.DwarfVersion.Should().Be(5);
                macho.GetLanguage().Should().Be(DwarfLanguage.C11);
                macho.CommandLineInfos.Should().OnlyContain(x => x.CommandLine.Contains("fstack-clash-protection"));
            }
        }
    }
}
