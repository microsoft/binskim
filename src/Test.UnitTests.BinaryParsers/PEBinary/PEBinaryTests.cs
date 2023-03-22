// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Dia2Lib;

using FluentAssertions;
using FluentAssertions.Execution;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PEBinaryTests
    {
        internal static string TestData = GetTestDirectory("Test.UnitTests.BinaryParsers" + Path.DirectorySeparatorChar + "TestData");
        internal static string BaselineTestDataDirectory = GetTestDirectory(@"Test.FunctionalTests.BinSkim.Driver" + Path.DirectorySeparatorChar + "BaselineTestData");

        internal static string GetTestDirectory(string relativeDirectory)
        {
            var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().Location);
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

            (string fileName, bool fromBaselineFolder)[] testCases = new[]
            {
                ("clangcl.14.pe.c.codeview.pdbpagesize_default.exe", false),
                ("clangcl.14.pe.c.codeview.pdbpagesize_4096.exe", false),
                ("clangcl.14.pe.c.codeview.pdbpagesize_8192.exe", false),
                ("clangcl.14.pe.c.codeview.pdbpagesize_16384.exe", false),
                ("clangcl.14.pe.c.codeview.pdbpagesize_32768.exe", false),
                ("Native_x64_VS2022_PDBPageSize_8192.exe", false),
                ("Native_x64_VS2013_Default.dll", true)
            };

            foreach ((string fileName, bool fromBaselineFolder) testCase in testCases)
            {
                string fileFullPath = testCase.fromBaselineFolder
                ? Path.Combine(BaselineTestDataDirectory, testCase.fileName)
                : Path.Combine(TestData, "PE", testCase.fileName);
                using (var peBinary = new PEBinary(new Uri(fileFullPath)))
                {
                    peBinary.Pdb.Should().NotBeNull();
                    peBinary.StrippedPdb.Should().BeNull();
                    peBinary.PdbParseException.Should().BeNull();
                }
            }
        }

        [Fact]
        public void PEBinary_NoPdbAvailable()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            (string fileName, bool fromBaselineFolder)[] testCases = new[]
            {
                ("clangcl.14.pe.c.codeview.pdbpagesize_8192_pdbmissing.exe", false),
                ("Native_x86_VS2013_PdbMissing.exe", true)
            };

            foreach ((string fileName, bool fromBaselineFolder) testCase in testCases)
            {
                string fileFullPath = testCase.fromBaselineFolder
                ? Path.Combine(BaselineTestDataDirectory, testCase.fileName)
                : Path.Combine(TestData, "PE", testCase.fileName);
                using (var peBinary = new PEBinary(new Uri(fileFullPath)))
                {
                    peBinary.Pdb.Should().BeNull();
                    peBinary.StrippedPdb.Should().BeNull();
                    peBinary.PdbParseException.Should().NotBeNull();
                }
            }
        }

        [Fact]
        public void PEBinary_PdbIsStripped()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string fileName = Path.Combine(BaselineTestDataDirectory, "Native_x86_VS2017_15.5.4_PdbStripped.dll");
            using (var peBinary = new PEBinary(new Uri(fileName)))
            {
                peBinary.Pdb.Should().BeNull();
                peBinary.StrippedPdb.Should().NotBeNull();
                peBinary.PdbParseException.Should().NotBeNull();
            }
        }

        [Fact]
        public void PEBinary_NativeBinaryContainsExpectedLanguageCode()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using (new AssertionScope())
            {
                ContainsLanguageCode("clangcl.pe.c.codeview.exe", Language.C).Should().BeTrue();
                ContainsLanguageCode("clangcl.pe.cpp.codeview.exe", Language.Cxx).Should().BeTrue();
                // Rust official compiler RustC supports this new CV_CFL_LANG value starting from version v1.59.0.
                ContainsLanguageCode("Native_x64_RustC_Rust_debuginfo2_v1.58.1.exe", Language.Rust).Should().BeFalse();
                ContainsLanguageCode("Native_x64_RustC_Rust_debuginfo2_v1.59.0.exe", Language.Rust).Should().BeTrue();
                ContainsLanguageCode("Native_x64_VS2019_CPlusPlus_DEBUG_DEFAULT.dll", Language.Cxx).Should().BeTrue();
            }
        }

        [Fact]
        public void PEBinary_IsManaged()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using (new AssertionScope())
            {
                IsManaged("clangcl.pe.c.codeview.exe").Should().BeFalse();
                IsManaged("clangcl.pe.cpp.codeview.exe").Should().BeFalse();
                IsManaged("Native_x64_RustC_Rust_debuginfo2_v1.59.0.exe").Should().BeFalse();
                IsManaged("Native_x64_VS2019_CPlusPlus_DEBUG_DEFAULT.dll").Should().BeFalse();
                IsManaged("Managed_x64_VS2022_CSharp_Net48_Default.exe").Should().BeTrue();
                IsManaged("Managed_x64_VS2022_CSharp_Net70_Default.exe").Should().BeFalse();
                IsManaged("Managed_x64_VS2022_CSharp_Net70_Default.dll").Should().BeTrue();
                IsManaged("Native_x64_VS2022_CSharp_Net70_Default_AOT.exe").Should().BeFalse();
                IsManaged("Native_x64_VS2022_CSharp_Net70_Default_AOT.dll").Should().BeFalse();
                IsManaged("Uwp_x64_VS2022_CSharp_22H2_Default.exe").Should().BeTrue();
                IsManaged("Uwp_x64_VS2022_CSharp_22H2_Default_Native.exe").Should().BeFalse();
                IsManaged("Uwp_x64_VS2022_CSharp_22H2_Default_Native.dll").Should().BeFalse();
                IsManaged("Managed_x64_VS2022_CSharp_NetCore31_Default.exe").Should().BeFalse();
                IsManaged("Managed_x64_VS2022_CSharp_NetCore31_Default.dll").Should().BeTrue();
                // Currently does not support single file app. The file inside should be managed.
                IsManaged("Managed_x64_VS2022_CSharp_Net70_Default_SelfContained_SingleFile.exe").Should().BeFalse();
            }
        }

        [Fact]
        public void PEBinary_IsDotNetCore()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using (new AssertionScope())
            {
                IsDotNetCore("clangcl.pe.c.codeview.exe").Should().BeFalse();
                IsDotNetCore("clangcl.pe.cpp.codeview.exe").Should().BeFalse();
                IsDotNetCore("Native_x64_RustC_Rust_debuginfo2_v1.59.0.exe").Should().BeFalse();
                IsDotNetCore("Native_x64_VS2019_CPlusPlus_DEBUG_DEFAULT.dll").Should().BeFalse();
                IsDotNetCore("Managed_x64_VS2022_CSharp_Net48_Default.exe").Should().BeFalse();
                IsDotNetCore("Managed_x64_VS2022_CSharp_Net70_Default.exe").Should().BeFalse();
                IsDotNetCore("Managed_x64_VS2022_CSharp_Net70_Default.dll").Should().BeTrue();
                IsDotNetCore("Native_x64_VS2022_CSharp_Net70_Default_AOT.exe").Should().BeFalse();
                IsDotNetCore("Native_x64_VS2022_CSharp_Net70_Default_AOT.dll").Should().BeFalse();
                IsDotNetCore("Uwp_x64_VS2022_CSharp_22H2_Default.exe").Should().BeTrue();
                IsDotNetCore("Uwp_x64_VS2022_CSharp_22H2_Default_Native.exe").Should().BeFalse();
                IsDotNetCore("Uwp_x64_VS2022_CSharp_22H2_Default_Native.dll").Should().BeFalse();
                IsDotNetCore("Managed_x64_VS2022_CSharp_NetCore31_Default.exe").Should().BeFalse();
                IsDotNetCore("Managed_x64_VS2022_CSharp_NetCore31_Default.dll").Should().BeTrue();
            }
        }

        [Fact]
        public void PEBinary_IsILLibrary()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using (new AssertionScope())
            {
                IsILLibrary("Native_x64_VS2022_CSharp_Net70_Default_AOT.dll").Should().BeFalse();
                IsILLibrary("Managed_x64_VS2022_CSharp_Net70_Default_ReadyToRun.dll").Should().BeTrue();
            }
        }

        [Fact]
        public void PEBinary_IsDotNetNative()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using (new AssertionScope())
            {
                IsDotNetNative("Uwp_x64_VS2022_CSharp_22H2_Default.exe").Should().BeFalse();
                IsDotNetNative("Uwp_x64_VS2022_CSharp_22H2_Default_Native.exe").Should().BeTrue();
                IsDotNetNative("Uwp_x64_VS2022_CSharp_22H2_Default_Native.dll").Should().BeTrue();
                // Native AOT is not the same as .NET Native
                IsDotNetNative("Native_x64_VS2022_CSharp_Net70_Default_AOT.exe").Should().BeFalse();
            }
        }

        [Fact]
        public void PEBinary_IsDotNetNativeBootstrapExe()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using (new AssertionScope())
            {
                IsDotNetNativeBootstrapExe("Uwp_x64_VS2022_CSharp_22H2_Default.exe").Should().BeFalse();
                IsDotNetNativeBootstrapExe("Uwp_x64_VS2022_CSharp_22H2_Default_Native.exe").Should().BeTrue();
                IsDotNetNativeBootstrapExe("Uwp_x64_VS2022_CSharp_22H2_Default_Native.dll").Should().BeFalse();
                // Native AOT is not the same as .NET Native
                IsDotNetNativeBootstrapExe("Native_x64_VS2022_CSharp_Net70_Default_AOT.exe").Should().BeFalse();
            }
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

        [Fact]
        public void PEBinary_PdbLoadTraceTests()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using (new AssertionScope())
            {
                PdbLoadTraceTest_Helper("Case_NoPdbSideBySide", "Case_GoodPdb_Symbol_symbols_dll", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 1);
                PdbLoadTraceTest_Helper("Case_NoPdbSideBySide", "Case_GoodPdb_Symbol_dll", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 2);
                PdbLoadTraceTest_Helper("Case_NoPdbSideBySide", "Case_GoodPdb_Symbol", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 3);
                PdbLoadTraceTest_Helper("Case_NoPdbSideBySide", null, tracePdbLoad: true, expectPdbLoadSuccess: false, expectPdbLoadTraceLineCount: 3);
                PdbLoadTraceTest_Helper("Case_NoPdbSideBySide", "Case_GoodPdb_Symbol_symbols_dll2", tracePdbLoad: true, expectPdbLoadSuccess: false, expectPdbLoadTraceLineCount: 6);

                PdbLoadTraceTest_Helper("Case_GoodSameNamePdbSideBySide", null, tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 3);
                PdbLoadTraceTest_Helper("Case_GoodSameNamePdbSideBySide", "Case_GoodPdb_Symbol_symbols_dll", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 1);

                // Most simple case.
                PdbLoadTraceTest_Helper("Case_GoodOriginalPdbSideBySide", null, tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 1);
                // Local one is not used at all since symbol folder is higher priority.
                PdbLoadTraceTest_Helper("Case_GoodOriginalPdbSideBySide", "Case_GoodPdb_Symbol", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 3);
                // Can not load from symbol folder for some reason, then local one is used.
                PdbLoadTraceTest_Helper("Case_GoodOriginalPdbSideBySide", "Case_BadPdb_Symbol_symbols_dll+BadPdb_Symbol_dll+BadPdb_Symbol", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 4);
                // Original Pdb is higher priority than same name pdb file.
                PdbLoadTraceTest_Helper("Case_BadOriginalPdbSideBySide+GoodSameNamePdbSideBySide", "Case_BadPdb_Symbol_symbols_dll+BadPdb_Symbol_dll+BadPdb_Symbol", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 6);
                // Case that all load failed.
                PdbLoadTraceTest_Helper("Case_BadOriginalPdbSideBySide+BadSameNamePdbSideBySide", "Case_BadPdb_Symbol_symbols_dll+BadPdb_Symbol_dll+BadPdb_Symbol", tracePdbLoad: true, expectPdbLoadSuccess: false, expectPdbLoadTraceLineCount: 6);
                //Case that load from symbol folder with same name pdb file.
                PdbLoadTraceTest_Helper("Case_BadOriginalPdbSideBySide+BadSameNamePdbSideBySide", "Case_BadPdb_Symbol_symbols_dll+BadPdb_Symbol_dll+BadPdb_Symbol+GoodPdb_SameName", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 7);
                // Case that trace is not enabled.
                PdbLoadTraceTest_Helper("Case_BadOriginalPdbSideBySide+BadSameNamePdbSideBySide", "Case_BadPdb_Symbol_symbols_dll+BadPdb_Symbol_dll+BadPdb_Symbol", tracePdbLoad: false, expectPdbLoadSuccess: false, expectPdbLoadTraceLineCount: 0);
                // Case that the second symbol file loaded successfully.
                PdbLoadTraceTest_Helper("Case_BadOriginalPdbSideBySide+GoodSameNamePdbSideBySide", "Case_BadPdb_Symbol_symbols_dll+GoodPdb_Symbol_dll+BadPdb_Symbol", tracePdbLoad: true, expectPdbLoadSuccess: true, expectPdbLoadTraceLineCount: 2);
            }
        }

        private static void PdbLoadTraceTest_Helper(
            string folderForPE,
            string folderForSymbol,
            bool tracePdbLoad,
            bool expectPdbLoadSuccess,
            int expectPdbLoadTraceLineCount
            )
        {
            PEBinary.ClearLocalSymbolDirectoriesCache();
            string caseName = "input is [" + string.Join("|", new object[] { folderForPE, folderForSymbol, tracePdbLoad, expectPdbLoadSuccess, expectPdbLoadTraceLineCount }) + "]";
            string pePath = Path.Combine(TestData, "PE/Trace", folderForPE, "PdbLoadTest.dll");
            string localSymbolDirectories = folderForSymbol == null ? null : Path.Combine(TestData, "SymbolsFolder/Trace/", folderForSymbol);
            using (var peBinary = new PEBinary(new Uri(pePath), localSymbolDirectories: localSymbolDirectories, tracePdbLoad: tracePdbLoad))
            {
                if (expectPdbLoadSuccess)
                {
                    peBinary.Pdb.Should().NotBeNull(caseName);
                    peBinary.PdbParseException.Should().BeNull(caseName);
                }
                else
                {
                    peBinary.Pdb.Should().BeNull(caseName);
                    peBinary.PdbParseException.Should().NotBeNull(caseName);
                }

                if (tracePdbLoad)
                {
                    peBinary.PdbLoadTrace.Should().NotBeNull(caseName);
                    string[] lines = peBinary.PdbLoadTrace.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                    lines.Length.Should().Be(expectPdbLoadTraceLineCount, caseName);

                    if (expectPdbLoadSuccess)
                    {
                        lines.Last().Should().Contain(nameof(DiaHresult.S_OK), caseName);
                    }
                    else
                    {
                        lines.Last().Should().NotContain(nameof(DiaHresult.S_OK), caseName);
                    }
                }
                else
                {
                    peBinary.PdbLoadTrace.Should().BeNull(caseName);
                }
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

        private static bool IsManaged(string fileName)
        {
            string fileFullPath = Path.Combine(TestData, "PE", fileName);
            using (var peBinary = new PEBinary(new Uri(fileFullPath)))
            {
                return peBinary.PE.IsManaged;
            }
        }

        private static bool IsDotNetCore(string fileName)
        {
            string fileFullPath = Path.Combine(TestData, "PE", fileName);
            using (var peBinary = new PEBinary(new Uri(fileFullPath)))
            {
                return peBinary.PE.IsDotNetCore;
            }
        }

        private static bool IsILLibrary(string fileName)
        {
            string fileFullPath = Path.Combine(TestData, "PE", fileName);
            using (var peBinary = new PEBinary(new Uri(fileFullPath)))
            {
                return peBinary.PE.IsILLibrary;
            }
        }

        private static bool IsDotNetNative(string fileName)
        {
            string fileFullPath = Path.Combine(TestData, "PE", fileName);
            using (var peBinary = new PEBinary(new Uri(fileFullPath)))
            {
                return peBinary.PE.IsDotNetNative;
            }
        }

        private static bool IsDotNetNativeBootstrapExe(string fileName)
        {
            string fileFullPath = Path.Combine(TestData, "PE", fileName);
            using (var peBinary = new PEBinary(new Uri(fileFullPath)))
            {
                return peBinary.PE.IsDotNetNativeBootstrapExe;
            }
        }
    }
}
