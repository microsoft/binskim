// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

using Dia2Lib;

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PEBinaryTests
    {
        internal static string BaselineTestsDataDirectory = GetTestDirectory(@"Test.FunctionalTests.BinSkim.Driver" + Path.DirectorySeparatorChar + "BaselineTestsData");

        private static string GetTestDirectory(string relativeDirectory)
        {
            var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            var codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
            var dirPath = Path.GetDirectoryName(codeBasePath);
            dirPath = Path.Combine(dirPath, string.Format(@"..{0}..{0}..{0}..{0}src{0}", Path.DirectorySeparatorChar));
            dirPath = Path.GetFullPath(dirPath);
            return Path.Combine(dirPath, relativeDirectory);
        }
    
        [Fact]
        public void PdbAvailable()
        {
            string fileName = Path.Combine(BaselineTestsDataDirectory, "Native_x64_VS2013_Default.pdb");
            PEBinary peBinary = new PEBinary(new Uri(fileName));
            peBinary.Pdb.Should().NotBeNull();
            peBinary.StrippedPdb.Should().BeNull();
            peBinary.PdbParseException.Should().BeNull();
        }

        [Fact]
        public void NoPdbAvailable()
        {
            string fileName = Path.Combine(BaselineTestsDataDirectory, "Native_x86_VS2013_PdbMissing.exe");
            PEBinary peBinary = new PEBinary(new Uri(fileName));
            peBinary.Pdb.Should().BeNull();
            peBinary.StrippedPdb.Should().BeNull();
            peBinary.PdbParseException.Should().NotBeNull();
        }

        [Fact]
        public void PdbIsStripped()
        {
            string fileName = Path.Combine(BaselineTestsDataDirectory, "Native_x86_VS2017_15.5.4_PdbStripped.dll");
            PEBinary peBinary = new PEBinary(new Uri(fileName));
            peBinary.Pdb.Should().BeNull();
            peBinary.StrippedPdb.Should().NotBeNull();
            peBinary.PdbParseException.Should().NotBeNull();
        }

        [Fact]
        public void IsWixBinary()
        {
            string fileName = Path.Combine(BaselineTestsDataDirectory, "Wix_3.11.1_VS2017_Bootstrapper.exe");
            PEBinary peBinary = new PEBinary(new Uri(fileName));
            peBinary.Pdb.Should().BeNull();
            peBinary.PE.IsWixBinary.Should().BeTrue();

            // Verify a random other exe to ensure it is properly reporting as not a WIX bootstrapper
            fileName = Path.Combine(BaselineTestsDataDirectory, "MixedMode_x64_VS2015_Default.exe");
            peBinary = new PEBinary(new Uri(fileName));
            peBinary.PE.IsWixBinary.Should().BeFalse();
        }

        [Fact]
        public void CanCreateIDiaSourceFromMsdia()
        {
            Action action = () => { IDiaDataSource source = ProgramDatabase.MsdiaComWrapper.GetDiaSource(); };
            action.ShouldNotThrow();
        }
    }
}
