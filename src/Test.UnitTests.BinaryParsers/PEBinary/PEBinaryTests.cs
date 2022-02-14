// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Dia2Lib;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PEBinaryTests
    {
        internal static string TestData = GetTestDirectory("Test.UnitTests.BinaryParsers" + Path.DirectorySeparatorChar + "TestsData");
        internal static string BaselineTestsDataDirectory = GetTestDirectory(@"Test.FunctionalTests.BinSkim.Driver" + Path.DirectorySeparatorChar + "BaselineTestsData");

        internal static string GetTestDirectory(string relativeDirectory)
        {
            var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            string codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
            string dirPath = Path.GetDirectoryName(codeBasePath);
            dirPath = Path.Combine(dirPath, string.Format(@"..{0}..{0}..{0}..{0}src{0}", Path.DirectorySeparatorChar));
            dirPath = Path.GetFullPath(dirPath);
            return Path.Combine(dirPath, relativeDirectory);
        }

        [Fact]
        public void PEBinary_PdbAvailable()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(BaselineTestsDataDirectory, "Native_x64_VS2013_Default.dll");
            using (var peBinary = new PEBinary(new Uri(fileName)))
            {
                peBinary.Pdb.Should().NotBeNull();
                peBinary.StrippedPdb.Should().BeNull();
                peBinary.PdbParseException.Should().BeNull();
            }
        }

        [Fact]
        public void PEBinary_NoPdbAvailable()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(BaselineTestsDataDirectory, "Native_x86_VS2013_PdbMissing.exe");
            using (var peBinary = new PEBinary(new Uri(fileName)))
            {
                peBinary.Pdb.Should().BeNull();
                peBinary.StrippedPdb.Should().BeNull();
                peBinary.PdbParseException.Should().NotBeNull();
            }
        }

        [Fact]
        public void PEBinary_PdbIsStripped()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(BaselineTestsDataDirectory, "Native_x86_VS2017_15.5.4_PdbStripped.dll");
            using (var peBinary = new PEBinary(new Uri(fileName)))
            {
                peBinary.Pdb.Should().BeNull();
                peBinary.StrippedPdb.Should().NotBeNull();
                peBinary.PdbParseException.Should().NotBeNull();
            }
        }

        [Fact]
        public void PEBinary_ContainsExpectedLanguageCode()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            ContainsLanguageCode("clangcl.pe.c.codeview.exe", Language.C).Should().BeTrue();
            ContainsLanguageCode("clangcl.pe.cpp.codeview.exe", Language.Cxx).Should().BeTrue();
            // As of v1.58.1 Rust official compiler RustC does not yet use the new CV_CFL_LANG code for Rust.
            // We will have another test file when new version use it.
            ContainsLanguageCode("Native_x64_RustC_Rust_debuginfo2_v1.58.1.exe", Language.Rust).Should().BeFalse();
            ContainsLanguageCode("Native_x64_VS2019_CPlusPlus_DEBUG_DEFAULT.dll", Language.Cxx).Should().BeTrue();
        }

        [Fact]
        public void PEBinary_CanCreateIDiaSourceFromMsdia()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            Action action = () => { IDiaDataSource source = ProgramDatabase.MsdiaComWrapper.GetDiaSource(); };
            action.Should().NotThrow();
        }

        [Fact]
        public void PEBinary_TryLoadPdbFromSymbolFolder()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(TestData, "PE/Native_x64.dll");
            using (var peBinary = new PEBinary(new Uri(fileName), localSymbolDirectories: Path.Combine(TestData, "SymbolsFolder")))
            {
                peBinary.Pdb.Should().NotBeNull();
                peBinary.StrippedPdb.Should().BeNull();
                peBinary.PdbParseException.Should().BeNull();
            }
        }

        private static bool ContainsLanguageCode(string fileName, Language language)
        {
            string fileFullPath = Path.Combine(TestData, "PE", fileName);
            using (var peBinary = new PEBinary(new Uri(fileFullPath)))
            {
                peBinary.Valid.Should().BeTrue();
                peBinary.Pdb.Should().NotBeNull();
                peBinary.PdbParseException.Should().BeNull();

                var languages = new HashSet<Language>();
                foreach (DisposableEnumerableView<Symbol> omView in peBinary.Pdb.CreateObjectModuleIterator())
                {
                    ObjectModuleDetails omDetails = omView.Value.GetObjectModuleDetails();
                    if (omDetails.Library == omDetails.Name)
                    {
                        languages.Add(omDetails.Language);
                    }
                }
                return languages.Contains(language);
            }
        }
    }
}
