// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine(string.Empty);
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
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
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            // The rest should still be false
            commandLine.IncrementalLinking.Should().BeFalse();
            commandLine.LinkTimeCodeGenerationEnabled.Should().BeFalse();
        }

        [Fact]
        public void DebugOptionDisablesSomeOthers()
        {
            // Different simple variants
            var commandLine = new LinkerCommandLine("/debug");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/DEBUG");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-debug");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            // More advanced options
            commandLine = new LinkerCommandLine("/debug:full");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-debug:full");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            // Do not over-match against /DEBUGTYPE
            commandLine = new LinkerCommandLine("/debugtype:cv,pdata,fixup");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("-debugtype:cv,pdata,fixup");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();
        }

        [Fact]
        public void COMDATFoldingTest()
        {
            var commandLine = new LinkerCommandLine("/opt:noicf");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("-opt:noicf");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/OPT:noicf");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/opt:NOICF");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();

            // /debug turns off these optimizations by default, so that we can test re-enabling them.
            commandLine = new LinkerCommandLine("/debug /opt:icf");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug -opt:icf");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug -OPT:icf");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug /opt:ICF");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();
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

            // /debug turns off these optimizations by default, so that we can test re-enabling them.
            // opt:ref implicitly enables opt:icf so both should become true
            commandLine = new LinkerCommandLine("/debug /opt:ref");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug -opt:ref");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug -OPT:ref");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug /opt:REF");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();
        }

        [Fact]
        public void MultipleOptimizationSettingsTest()
        {
            // -debug clears these settings so we can test that they get re-enabled (or not) as expected
            var commandLine = new LinkerCommandLine("/debug /opt:ref,icf");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug /opt:icf,ref");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug /opt:noicf,ref");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeTrue();

            commandLine = new LinkerCommandLine("/debug /opt:icf,noref");
            commandLine.COMDATFoldingEnabled.Should().BeTrue();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug /opt:noicf,noref");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug /opt:noicf,noref,lbr");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
            commandLine.OptimizeReferencesEnabled.Should().BeFalse();

            commandLine = new LinkerCommandLine("/debug /opt:noicf,noref,nolbr");
            commandLine.COMDATFoldingEnabled.Should().BeFalse();
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
        }
    }
}
