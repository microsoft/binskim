// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class CompilerCommandLineTests
    {
        [Fact]
        public void EmptyCommandLineAllFalse()
        {
            var commandLine = new CompilerCommandLine("");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeFalse();
            commandLine.OptimizationsEnabled.Should().BeFalse();
            commandLine.UsesDebugCRuntime.Should().BeFalse();
            commandLine.WholeProgramOptimization.Should().BeFalse();

            commandLine = new CompilerCommandLine(string.Empty);
            commandLine.EliminateDuplicateStringsEnabled.Should().BeFalse();
            commandLine.OptimizationsEnabled.Should().BeFalse();
            commandLine.UsesDebugCRuntime.Should().BeFalse();
            commandLine.WholeProgramOptimization.Should().BeFalse();
        }

        [Fact]
        public void OptimizationSettingsBasicTest()
        {
            // Enable flags
            var commandLine = new CompilerCommandLine("/O1");
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-O1");
            commandLine.OptimizationsEnabled.Should().BeTrue();

            commandLine = new CompilerCommandLine("/O2");
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-O2");
            commandLine.OptimizationsEnabled.Should().BeTrue();

            commandLine = new CompilerCommandLine("/Og");
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-Og");
            commandLine.OptimizationsEnabled.Should().BeTrue();

            commandLine = new CompilerCommandLine("/Os");
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-Os");
            commandLine.OptimizationsEnabled.Should().BeTrue();

            commandLine = new CompilerCommandLine("/Ot");
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-Ot");
            commandLine.OptimizationsEnabled.Should().BeTrue();

            commandLine = new CompilerCommandLine("/Ox");
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-Ox");
            commandLine.OptimizationsEnabled.Should().BeTrue();

            // Disable flags
            commandLine = new CompilerCommandLine("/Od");
            commandLine.OptimizationsEnabled.Should().BeFalse();
            commandLine = new CompilerCommandLine("-Od");
            commandLine.OptimizationsEnabled.Should().BeFalse();

            // Unknown flag
            commandLine = new CompilerCommandLine("/Oa");
            commandLine.OptimizationsEnabled.Should().BeFalse();
            commandLine = new CompilerCommandLine("-Oa");
            commandLine.OptimizationsEnabled.Should().BeFalse();
        }

        [Fact]
        public void OptimizationSettingsMultipleFlagsTest()
        {
            // Last option between on and off wins
            var commandLine = new CompilerCommandLine("/O1 /O2 /Od");
            commandLine.OptimizationsEnabled.Should().BeFalse();
            commandLine = new CompilerCommandLine("-O1 -O2 -Od");
            commandLine.OptimizationsEnabled.Should().BeFalse();

            commandLine = new CompilerCommandLine("/Od /O1 /O2");
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-Od -O1 -O2");
            commandLine.OptimizationsEnabled.Should().BeTrue();

            commandLine = new CompilerCommandLine("/O1 /Od /O2");
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-O1 -Od -O2");
            commandLine.OptimizationsEnabled.Should().BeTrue();
        }

        [Fact]
        public void DebugCRuntimeTest()
        {
            var commandLine = new CompilerCommandLine("/MT");
            commandLine.UsesDebugCRuntime.Should().BeFalse();
            commandLine = new CompilerCommandLine("-MT");
            commandLine.UsesDebugCRuntime.Should().BeFalse();

            commandLine = new CompilerCommandLine("/MD");
            commandLine.UsesDebugCRuntime.Should().BeFalse();
            commandLine = new CompilerCommandLine("-MD");
            commandLine.UsesDebugCRuntime.Should().BeFalse();

            commandLine = new CompilerCommandLine("/MTd");
            commandLine.UsesDebugCRuntime.Should().BeTrue();
            commandLine = new CompilerCommandLine("-MTd");
            commandLine.UsesDebugCRuntime.Should().BeTrue();

            commandLine = new CompilerCommandLine("/MDd");
            commandLine.UsesDebugCRuntime.Should().BeTrue();
            commandLine = new CompilerCommandLine("-MDd");
            commandLine.UsesDebugCRuntime.Should().BeTrue();

            // Last writer wins
            commandLine = new CompilerCommandLine("/MT /MTd");
            commandLine.UsesDebugCRuntime.Should().BeTrue();
            commandLine = new CompilerCommandLine("/MTd /MT");
            commandLine.UsesDebugCRuntime.Should().BeFalse();
        }

        [Fact]
        public void EliminateDuplicateStringsTest()
        {
            var commandLine = new CompilerCommandLine("/GF");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("/GF-");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeFalse();
            commandLine = new CompilerCommandLine("-GF");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-GF-");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeFalse();

            // Some optimization settings implicitly enable
            commandLine = new CompilerCommandLine("/O1");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("/O2");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-O1");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeTrue();
            commandLine = new CompilerCommandLine("-O2");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeTrue();
        }

        [Fact]
        public void WholeProgramOptimizationTest()
        {
            var commandLine = new CompilerCommandLine("/GL");
            commandLine.WholeProgramOptimization.Should().BeTrue();
            commandLine = new CompilerCommandLine("/GL-");
            commandLine.WholeProgramOptimization.Should().BeFalse();
            commandLine = new CompilerCommandLine("-GL");
            commandLine.WholeProgramOptimization.Should().BeTrue();
            commandLine = new CompilerCommandLine("-GL-");
            commandLine.WholeProgramOptimization.Should().BeFalse();

            // Last writer wins
            commandLine = new CompilerCommandLine("/GL- /GL");
            commandLine.WholeProgramOptimization.Should().BeTrue();
            commandLine = new CompilerCommandLine("/GL /GL-");
            commandLine.WholeProgramOptimization.Should().BeFalse();
            commandLine = new CompilerCommandLine("-GL- -GL");
            commandLine.WholeProgramOptimization.Should().BeTrue();
            commandLine = new CompilerCommandLine("-GL -GL-");
            commandLine.WholeProgramOptimization.Should().BeFalse();
        }

        [Fact]
        public void RealisticExample()
        {
            // Taken from a real (anonymized) OSS project compiler command-line.  Include path list considerably shortened
            // for brevity.
            var commandLine = new CompilerCommandLine(@"
-c -ID:\_w\1\s\externals\zlib-1.2.12\ -ID:\_w\1\s\Contoso -ID:\_w\1\s\Include -ID:\_w\1\s\Include\internal 
-ID:\_w\1\s\PC -Zi -nologo -W3 -WX- -diagnostics:column -O2 -Oi -GL -D_HAVE_ZLIB -D_USRDLL -DWIN32 -D_WIN64
-D_M_X64 -DNDEBUG -D_WINDLL -GF -Gm- -MD -GS -Gy -fp:precise -Zc:wchar_t -Zc:forScope -Zc:inline -external:W3
-Gd -TC -FC -errorreport:queue -Zm200 -utf-8 
-I""C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC\14.29.30133\include""
-I""C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC\14.29.30133\atlmfc\include""
-I""C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\VS\include""
-I""C:\Program Files (x86)\Windows Kits\10\Include\10.0.22000.0\ucrt""
-I""C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\VS\UnitTest\include""
-external:I""C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC\14.29.30133\include""
-X");
            commandLine.EliminateDuplicateStringsEnabled.Should().BeTrue();
            commandLine.OptimizationsEnabled.Should().BeTrue();
            commandLine.UsesDebugCRuntime.Should().BeFalse();
            commandLine.WholeProgramOptimization.Should().BeTrue();
        }
    }
}
