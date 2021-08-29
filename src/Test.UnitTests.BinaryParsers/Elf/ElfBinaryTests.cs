// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.Dwarf;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers.Elf
{
    public class ElfBinaryTests
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
        public void ValidateDwarfV4_WithO2()
        {
            // Hello.c compiled using: gcc -Wall -O2 -g -gdwarf-4 hello.c -o hello4
            string fileName = Path.Combine(TestData, "Dwarf/hello-dwarf4-o2");
            using var binary = new ElfBinary(new Uri(fileName));
            binary.DwarfVersion.Should().Be(4);
            binary.CommandLineInfos.Should().OnlyContain(x => x.CommandLine.Contains("O2"));
            binary.GetLanguage().Should().Be(DwarfLanguage.C99);
        }

        [Fact]
        public void ValidateDwarfV5_WithO2()
        {
            // Hello.c compiled using: gcc -Wall -O2 -g -gdwarf-5 hello.c -o hello5
            string fileName = Path.Combine(TestData, "Dwarf/hello-dwarf5-o2");
            using var binary = new ElfBinary(new Uri(fileName));
            binary.DwarfVersion.Should().Be(5);
            binary.CommandLineInfos.Should().OnlyContain(x => x.CommandLine.Contains("O2"));
            binary.GetLanguage().Should().Be(DwarfLanguage.C11);
        }

        [Fact]
        public void ValidateDwarfV4_WithO2_Split_DebugFileExists()
        {
            // dwotest.cpp compiled using: gcc -Wall -O2 -g -gdwarf-4 dwotest.cpp -gsplit-dwarf -o dwotest.gcc.4.o
            string fileName = Path.Combine(TestData, "Dwarf/DwarfSplitV4/dwotest.gcc.4.o");
            using var binary = new ElfBinary(new Uri(fileName));
            binary.DwarfVersion.Should().Be(4);
            binary.GetLanguage().Should().Be(DwarfLanguage.CPlusPlus);
        }

        [Fact]
        public void ValidateDwarfV5_WithO2_Split_DebugFileExists()
        {
            // dwotest.cpp compiled using: gcc -Wall -O2 -g -gdwarf-5 dwotest.cpp -gsplit-dwarf -o dwotest.gcc.5.o
            string fileName = Path.Combine(TestData, "Dwarf/DwarfSplitV5/dwotest.gcc.5.o");
            using var binary = new ElfBinary(new Uri(fileName));
            binary.DwarfVersion.Should().Be(5);
            binary.GetLanguage().Should().Be(DwarfLanguage.CPlusPlus14);
        }

        [Fact]
        public void ValidateDwarfV4_WithO2_Split_DebugFileMissing()
        {
            // dwotest.cpp compiled using: gcc -Wall -O2 -g -gdwarf-4 dwotest.cpp -gsplit-dwarf -o dwotest.gcc.4.o
            string fileName = Path.Combine(TestData, "Dwarf/DwarfSplitV4DebugFileMissing/dwotest.gcc.4.o");
            using var binary = new ElfBinary(new Uri(fileName));
            binary.DwarfVersion.Should().Be(4);
            binary.GetLanguage().Should().Be(DwarfLanguage.Unknown); //missing dwo file should not cause exception
        }

        [Fact]
        public void ValidateDwarfV5_WithO2_Split_DebugFileMissing()
        {
            // dwotest.cpp compiled using: gcc -Wall -O2 -g -gdwarf-5 dwotest.cpp -gsplit-dwarf -o dwotest.gcc.5.o
            string fileName = Path.Combine(TestData, "Dwarf/DwarfSplitV5DebugFileMissing/dwotest.gcc.5.o");
            using var binary = new ElfBinary(new Uri(fileName));
            binary.DwarfVersion.Should().Be(5);
            binary.GetLanguage().Should().Be(DwarfLanguage.Unknown); //missing dwo file should not cause exception
        }

        [Fact]
        public void ValidateDwarfV4_WithO2_Split_DebugFileInAnotherDirectory()
        {
            // dwotest.cpp compiled using: gcc -Wall -O2 -g -gdwarf-4 dwotest.cpp -gsplit-dwarf -o dwotest.gcc.4.o
            string fileName = Path.Combine(TestData,
                "Dwarf/DwarfSplitV4DebugFileInAnotherDirectory/BinaryDirectory/dwotest.gcc.4.o");

            // test for: when not pass in directories
            using var binaryWithoutPathList = new ElfBinary(new Uri(fileName));
            binaryWithoutPathList.GetLanguage().Should().Be(DwarfLanguage.Unknown);

            // test for: when able to find in any of the pass in directories
            string localSymbolDirectory1 = Path.Combine(TestData,
                "Dwarf/DwarfSplitV4DebugFileInAnotherDirectory/NotExists");
            string localSymbolDirectory2 = Path.Combine(TestData,
                "WithoutDwoFiles");
            string localSymbolDirectory3 = Path.Combine(TestData,
                "Dwarf/DwarfSplitV4DebugFileInAnotherDirectory/AnotherLocalSymbolDirectory");
            var pathListFound = new List<string>() { localSymbolDirectory1, localSymbolDirectory2, localSymbolDirectory3 };
            using var binaryFound = new ElfBinary(new Uri(fileName), string.Join(';', pathListFound));
            binaryFound.DwarfVersion.Should().Be(4);
            binaryFound.GetLanguage().Should().Be(DwarfLanguage.CPlusPlus);

            // test for: when not able to find in any of the pass in directories, also not able to find in same directory
            var pathListNotFound = new List<string>() { localSymbolDirectory1, localSymbolDirectory2 };
            using var binaryNotFound = new ElfBinary(new Uri(fileName), string.Join(';', pathListNotFound));
            binaryNotFound.GetLanguage().Should().Be(DwarfLanguage.Unknown);
        }

        [Fact]
        public void ValidateDwarfV4_WithO2_Split_DebugFileInSameDirectory()
        {
            // dwotest.cpp compiled using: gcc -Wall -O2 -g -gdwarf-4 dwotest.cpp -gsplit-dwarf -o dwotest.gcc.4.o
            string fileName = Path.Combine(TestData,
                "Dwarf/DwarfSplitV4/dwotest.gcc.4.o");

            // test for: when not able to find in any of the pass in directories, should try load in same directory
            string localSymbolDirectory1 = Path.Combine(TestData,
                "Dwarf/DwarfSplitV4DebugFileInAnotherDirectory/NotExists");
            string localSymbolDirectory2 = Path.Combine(TestData,
                "WithoutDwoFiles");
            var pathList = new List<string>() { localSymbolDirectory1, localSymbolDirectory2 };
            using var binary = new ElfBinary(new Uri(fileName), string.Join(';', pathList));
            binary.DwarfVersion.Should().Be(4);
            binary.GetLanguage().Should().Be(DwarfLanguage.CPlusPlus);
        }

        [Fact]
        public void Validate_DebugFileType()
        {
            // test for: build with no debug
            // compiled using: gcc -o gcc.nodebug gcc.nodebug.c
            string fileName = Path.Combine(TestData, "Dwarf/DebugFileType/BinaryDirectory/gcc.nodebug");
            using var binaryNodebug = new ElfBinary(new Uri(fileName));
            binaryNodebug.DebugFileType.Should().Be(DebugFileType.NoDebug);
            binaryNodebug.DebugFileLoaded.Should().Be(false);

            // test for: build with debug included
            // compiled using:
            // gcc -ggdb -fPIC -fstack-protector-strong --param ssp-buffer-size=4 -fstack-clash-protection
            // -o gcc.objcopy.stripall.addgnudebuglink gcc.objcopy.stripall.addgnudebuglink.c
            fileName = Path.Combine(TestData, "Dwarf/DebugFileType/BinaryDirectory/gcc.objcopy.stripall.addgnudebuglink.full");
            using var binaryDebugIncluded = new ElfBinary(new Uri(fileName));
            binaryDebugIncluded.DebugFileType.Should().Be(DebugFileType.DebugIncluded);
            binaryDebugIncluded.DebugFileLoaded.Should().Be(true);

            // test for: build with debug, but stripped, target the debug file itself
            // compiled using:
            // 1.
            // gcc -ggdb -fPIC -fstack-protector-strong --param ssp-buffer-size=4 -fstack-clash-protection
            // -o gcc.objcopy.stripall.addgnudebuglink gcc.objcopy.stripall.addgnudebuglink.c
            // 2.
            // objcopy --only-keep-debug gcc.objcopy.stripall.addgnudebuglink gcc.objcopy.stripall.addgnudebuglink.dbg
            fileName = Path.Combine(TestData, "Dwarf/DebugFileType/AnotherLocalSymbolDirectory/gcc.objcopy.stripall.addgnudebuglink.dbg");
            using var binaryDebugOnlyFileDebuglink = new ElfBinary(new Uri(fileName));
            binaryDebugOnlyFileDebuglink.DebugFileType.Should().Be(DebugFileType.DebugOnlyFileDebuglink);
            binaryDebugOnlyFileDebuglink.DebugFileLoaded.Should().Be(false);

            // test for: build with debug, but stripped, and a second strip on the debug file, target the debug file itself
            // compiled using:
            // 1.
            // gcc -ggdb -fPIC -fstack-protector-strong --param ssp-buffer-size=4 -fstack-clash-protection
            // -o gcc.objcopy.stripall.addgnudebuglink gcc.objcopy.stripall.addgnudebuglink.c
            // 2.
            // objcopy --only-keep-debug gcc.objcopy.stripall.addgnudebuglink gcc.objcopy.stripall.addgnudebuglink.dbg
            // 3.
            // objcopy --strip-all gcc.objcopy.stripall.addgnudebuglink.dbg
            fileName = Path.Combine(TestData, "Dwarf/DebugFileType/AnotherLocalSymbolDirectory2/gcc.objcopy.stripall.addgnudebuglink.dbg");
            using var binaryDebugOnlyFileDebuglinkDebugStripped = new ElfBinary(new Uri(fileName));
            binaryDebugOnlyFileDebuglinkDebugStripped.DebugFileType.Should().Be(DebugFileType.DebugOnlyFileWithDebugStripped);
            binaryDebugOnlyFileDebuglinkDebugStripped.DebugFileLoaded.Should().Be(false);

            // test for: build with debug, but stripped, before link to debug file
            // compiled using:
            // 1.
            // gcc -ggdb -fPIC -fstack-protector-strong --param ssp-buffer-size=4 -fstack-clash-protection
            // -o gcc.objcopy.stripall.addgnudebuglink gcc.objcopy.stripall.addgnudebuglink.c
            // 2.
            // objcopy --strip-all gcc.objcopy.stripall.addgnudebuglink
            fileName = Path.Combine(TestData, "Dwarf/DebugFileType/BinaryDirectory/gcc.objcopy.stripall.addgnudebuglink.nolink");
            using var binaryNoLink = new ElfBinary(new Uri(fileName));
            binaryNoLink.DebugFileType.Should().Be(DebugFileType.NoDebug);
            binaryNoLink.DebugFileLoaded.Should().Be(false);

            // test for: build with debug, but stripped, after link to debug file, but debug file missing
            // compiled using:
            // 1.
            // gcc -ggdb -fPIC -fstack-protector-strong --param ssp-buffer-size=4 -fstack-clash-protection
            // -o gcc.objcopy.stripall.addgnudebuglink gcc.objcopy.stripall.addgnudebuglink.c
            // 2.
            // objcopy --only-keep-debug gcc.objcopy.stripall.addgnudebuglink gcc.objcopy.stripall.addgnudebuglink.dbg
            // 3.
            // objcopy --strip-all gcc.objcopy.stripall.addgnudebuglink
            // 4.
            // objcopy --add-gnu-debuglink=gcc.objcopy.stripall.addgnudebuglink.dbg gcc.objcopy.stripall.addgnudebuglink
            fileName = Path.Combine(TestData, "Dwarf/DebugFileType/BinaryDirectory/gcc.objcopy.stripall.addgnudebuglink");
            using var binaryWithLinkDebugFileMissing = new ElfBinary(new Uri(fileName));
            binaryWithLinkDebugFileMissing.DebugFileType.Should().Be(DebugFileType.FromDebuglink);
            binaryWithLinkDebugFileMissing.DebugFileLoaded.Should().Be(false);

            // test for: build with debug, but stripped, after link to debug file, debug file exists
            // compiled using same commands.
            using var binaryWithLink = new ElfBinary(new Uri(fileName), Path.Combine(TestData, "Dwarf/DebugFileType/AnotherLocalSymbolDirectory/"));
            binaryWithLink.DebugFileType.Should().Be(DebugFileType.FromDebuglink);
            binaryWithLink.DebugFileLoaded.Should().Be(true);


            // test for: build with debug, but stripped, after link to debug file, debug file exists but also stripped
            // compiled using same commands. And do another strip on the dbg file.
            // DebugFileType should be the same but since dbg file is also stripped, the DebugFileLoaded should be false.
            // 5.
            // objcopy --strip-all gcc.objcopy.stripall.addgnudebuglink.dbg
            using var binaryWithLinkDebugStriped = new ElfBinary(new Uri(fileName), Path.Combine(TestData, "Dwarf/DebugFileType/AnotherLocalSymbolDirectory2/"));
            binaryWithLinkDebugStriped.DebugFileType.Should().Be(DebugFileType.FromDebuglink);
            binaryWithLinkDebugStriped.DebugFileLoaded.Should().Be(false);

            // test for: build with split dwarf debug, debug file missing
            // compiled using:
            // gcc -g -gdwarf-5 -fPIC -fstack-protector-strong --param ssp-buffer-size=4 -fstack-clash-protection
            // -o gcc.gsplitdwarf.5 gcc.gsplitdwarf.5.c -gsplit-dwarf
            fileName = Path.Combine(TestData, "Dwarf/DebugFileType/BinaryDirectory/gcc.gsplitdwarf.5");
            using var binaryFromDwoDebugFileMissing = new ElfBinary(new Uri(fileName));
            binaryFromDwoDebugFileMissing.DebugFileType.Should().Be(DebugFileType.FromDwo);
            binaryFromDwoDebugFileMissing.DebugFileLoaded.Should().Be(false);

            // test for: build with split dwarf debug, debug file exists
            // compiled using same commands.
            using var binaryFromDwo = new ElfBinary(new Uri(fileName), Path.Combine(TestData, "Dwarf/DebugFileType/AnotherLocalSymbolDirectory/"));
            binaryFromDwo.DebugFileType.Should().Be(DebugFileType.FromDwo);
            binaryFromDwo.DebugFileLoaded.Should().Be(true);

            // test for: build with split dwarf debug, target the debug file itself
            // compiled using same commands.
            fileName = Path.Combine(TestData, "Dwarf/DebugFileType/AnotherLocalSymbolDirectory/gcc.gsplitdwarf.5.dwo");
            using var binaryDebugOnlyFileDwo = new ElfBinary(new Uri(fileName));
            binaryDebugOnlyFileDwo.DebugFileType.Should().Be(DebugFileType.DebugOnlyFileDwo);
            binaryDebugOnlyFileDwo.DebugFileLoaded.Should().Be(false);
        }
    }
}
