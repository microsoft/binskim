// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection.PortableExecutable;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class LinkerCommandLineTests
    {
        [Fact]
        public void EmptyCommandLineAllFalse()
        {
            // All options should return false when the command line is empty
            var commandLine = new LinkerCommandLine("");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine(string.Empty);
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();
        }

        [Fact]
        public void OnByDefaultSettingsForNonEmptyCommandLine()
        {
            // Some settings are enabled by default but are suppressed for empty command
            // lines.  Any non-null command line should enable them.
            var commandLine = new LinkerCommandLine("foo");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            // Incremental linking is documented as defaulting to true.  However, so do other options
            // such as /OPT:REF that disable incremental linking.
            commandLine.IncrementalLinking.Should().BeFalse();

            // The rest should still be false
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
        }

        [Fact]
        public void DebugOptionDisablesSomeOthers()
        {
            // Different simple variants
            var commandLine = new LinkerCommandLine("/debug");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/DEBUG");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-debug");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            // More advanced options
            commandLine = new LinkerCommandLine("/debug:full");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-debug:full");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            // Do not over-match against /DEBUGTYPE
            commandLine = new LinkerCommandLine("/debugtype:cv,pdata,fixup");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("-debugtype:cv,pdata,fixup");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            // DEBUG:NONE does not enable debug setting to disable optimizations
            commandLine = new LinkerCommandLine("/debug:none");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();
        }

        [Fact]
        public void COMDATFoldingTest()
        {
            var commandLine = new LinkerCommandLine("/opt:noicf");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-opt:noicf");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/OPT:noicf");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/opt:NOICF");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();

            // /debug turns off these optimizations by default, so that we can test re-enabling them.
            commandLine = new LinkerCommandLine("/debug /opt:icf");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug -opt:icf");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug -OPT:icf");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug /opt:ICF");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            // The order of arguments should not matter.  /debug only turns off ICF if it is not explicitly
            // specified.
            commandLine = new LinkerCommandLine("/opt:icf /debug");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-opt:icf /debug");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-OPT:icf /debug");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/opt:ICF /debug");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            // /PROFILE implies NOICF
            commandLine = new LinkerCommandLine("/profile");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
        }

        [Fact]
        public void OptimizeReferencesTest()
        {
            var commandLine = new LinkerCommandLine("/opt:noref");
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-opt:noref");
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/OPT:noref");
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/opt:NOREF");
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            // /debug turns off these optimizations by default
            // opt:ref implicitly enables opt:icf so both should become true
            commandLine = new LinkerCommandLine("/debug /opt:ref");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            // The order between an explicit /opt:ref and /debug should not matter.  Debug only affects
            // implicit enable.
            commandLine = new LinkerCommandLine("/opt:ref /debug ");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            // /PROFILE implies REF
            commandLine = new LinkerCommandLine("/profile");
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();
        }

        [Fact]
        public void MultipleOptimizationSettingsTest()
        {
            // -debug clears these settings so we can test that they get re-enabled (or not) as expected
            var commandLine = new LinkerCommandLine("/debug /opt:ref,icf");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug /opt:icf,ref");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug /opt:noicf,ref");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug /opt:icf,noref");
            commandLine.ComdatFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug /opt:noicf,noref");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug /opt:noicf,noref,lbr");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug /opt:noicf,noref,nolbr");
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();
        }

        [Fact]
        public void LinkTimeCodeGenerationTest()
        {
            var commandLine = new LinkerCommandLine("/LTCG");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();
            commandLine = new LinkerCommandLine("-LTCG");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();
            commandLine = new LinkerCommandLine("/ltcg");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();
            commandLine = new LinkerCommandLine("-ltcg");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/LTCG:OFF");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
            commandLine = new LinkerCommandLine("-LTCG:OFF");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
            commandLine = new LinkerCommandLine("/ltcg:off");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
            commandLine = new LinkerCommandLine("-ltcg:off");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/LTCG:INCREMENTAL");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();
            commandLine = new LinkerCommandLine("-LTCG:INCREMENTAL");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();
            commandLine = new LinkerCommandLine("/ltcg:incremental");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();
            commandLine = new LinkerCommandLine("-ltcg:incremental");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();

            // Last writer wins
            commandLine = new LinkerCommandLine("/LTCG:OFF /LTCG");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();
            commandLine = new LinkerCommandLine("/LTCG /LTCG:OFF");
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
        }

        [Fact]
        public void IncrementalLinkingTest()
        {
            // Direct enable/disable
            LinkerCommandLine commandLine = new LinkerCommandLine("/incremental");
            commandLine.IncrementalLinking.Should().BeTrue();
            commandLine = new LinkerCommandLine("-incremental");
            commandLine.IncrementalLinking.Should().BeTrue();
            commandLine = new LinkerCommandLine("/INCREMENTAL");
            commandLine.IncrementalLinking.Should().BeTrue();
            commandLine = new LinkerCommandLine("-INCREMENTAL");
            commandLine.IncrementalLinking.Should().BeTrue();

            commandLine = new LinkerCommandLine("/incremental:no");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine = new LinkerCommandLine("-incremental:no");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine = new LinkerCommandLine("/INCREMENTAL:NO");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine = new LinkerCommandLine("-INCREMENTAL:NO");
            commandLine.IncrementalLinking.Should().BeFalse();

            // Last writer wins
            commandLine = new LinkerCommandLine("/incremental /incremental:no");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine = new LinkerCommandLine("/incremental:no /incremental");
            commandLine.IncrementalLinking.Should().BeTrue();

            // Implicitly enabled by /debug
            commandLine = new LinkerCommandLine("/debug");
            commandLine.IncrementalLinking.Should().BeTrue();

            // Implicitly disabled despite /debug with several other options
            commandLine = new LinkerCommandLine("/debug /opt:ref");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine = new LinkerCommandLine("/debug /opt:icf");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine = new LinkerCommandLine("/debug /opt:lbr");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine = new LinkerCommandLine("/debug /order");
            commandLine.IncrementalLinking.Should().BeFalse();

            // Implicitly enabled by /debug but not overridden by disabled optimizations
            commandLine = new LinkerCommandLine("/debug /opt:noref");
            commandLine.IncrementalLinking.Should().BeTrue();
            commandLine = new LinkerCommandLine("/debug /opt:noicf");
            commandLine.IncrementalLinking.Should().BeTrue();
            commandLine = new LinkerCommandLine("/debug /opt:nolbr");
            commandLine.IncrementalLinking.Should().BeTrue();

            // Implied disable by /PROFILE.  Can be explicitly re-enabled.
            commandLine = new LinkerCommandLine("/profile");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine = new LinkerCommandLine("/profile /incremental");
            commandLine.IncrementalLinking.Should().BeTrue();
            commandLine = new LinkerCommandLine("/incremental /profile");
            commandLine.IncrementalLinking.Should().BeTrue();
        }

        [Fact]
        public void RealisticCommandLineTest()
        {
            // Taken from a real (anonymized) project linker command-line.  Include path list considerably shortened
            // for brevity.
            var commandLine = new LinkerCommandLine(@"
/ERRORREPORT:QUEUE /OUT:D:\\_w\\1\\b\\bin\\amd64\\Contoso.dll /INCREMENTAL:NO /NOLOGO /LIBPATH:D:\\_w\\1\\b\\bin\\amd64\\
/NODEFAULTLIB:LIBC /MANIFEST:NO /DEBUG /PDB:D:\\_w\\1\\b\\bin\\amd64\\Contoso.pdb /SUBSYSTEM:WINDOWS
/PGD:D:\\_w\\1\\b\\bin\\amd64\\python310.pgd /LTCG:PGUpdate
/LTCGOUT:D:\\_w\\1\\s\\PCbuild\\obj\\310amd64_PGUpdate\\contoso\\contoso.iobj /TLBID:1 /DYNAMICBASE /NXCOMPAT
/IMPLIB:D:\\_w\\1\\b\\bin\\amd64\\contoso.lib /MACHINE:X64 /OPT:REF,NOICF /DLL");
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();
            commandLine.ComdatFoldingEnabled.Should().BeFalse();
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeTrue();
        }
    }
}
