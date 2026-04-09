// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class CommandLineHelperTests
    {
        // Tests for the /Fp (Name .pch file) option parsing fix.
        // MSVC emits precompiled header filenames as /Fpfilename.pch (no colon).
        // Some tools may emit /Fp:filename.pch (with colon).
        // The prefix "/Fp" (without colon) must match both forms.

        [Fact]
        public void GetOptionValue_FpWithoutColon_MatchesNocolonForm()
        {
            // MSVC standard form: /Fpfilename.pch
            string optionValue = string.Empty;
            string[] options = { "/Fp" };

            bool found = CommandLineHelper.GetOptionValue(
                "/ZH:SHA_256 /Yupch.h /Fpstdafx.pch",
                options,
                CommandLineHelper.OrderOfPrecedence.FirstWins,
                ref optionValue);

            found.Should().BeTrue();
            optionValue.Should().Be("stdafx.pch");
        }

        [Fact]
        public void GetOptionValue_FpWithoutColon_MatchesColonForm()
        {
            // Colon form: /Fp:filename.pch — prefix "/Fp" matches, value starts with ':'
            string optionValue = string.Empty;
            string[] options = { "/Fp" };

            bool found = CommandLineHelper.GetOptionValue(
                "/ZH:SHA_256 /Yupch.h /Fp:stdafx.pch",
                options,
                CommandLineHelper.OrderOfPrecedence.FirstWins,
                ref optionValue);

            found.Should().BeTrue();
            // Caller should TrimStart(':') to normalize — raw value includes leading colon
            optionValue.TrimStart(':').Should().Be("stdafx.pch");
        }

        [Fact]
        public void GetOptionValue_FpWithoutColon_MatchesQuotedForm()
        {
            // Quoted form: /Fp"path\\to\\MyProject.pch" — value includes surrounding quotes
            string optionValue = string.Empty;
            string[] options = { "/Fp" };

            bool found = CommandLineHelper.GetOptionValue(
                "/ZH:SHA_256 /Yupch.h /Fp\"path\\to\\MyProject.pch\"",
                options,
                CommandLineHelper.OrderOfPrecedence.FirstWins,
                ref optionValue);

            found.Should().BeTrue();
            // Caller may Trim('"') to normalize the quoted value
            optionValue.Trim('"').Should().Be(@"path\to\MyProject.pch");
        }

        [Fact]
        public void GetOptionValue_FpColonPrefix_DoesNotMatchNocolonForm()
        {
            // Documents the original bug: "/Fp:" prefix fails to match /Fpfilename.pch
            string optionValue = string.Empty;
            string[] options = { "/Fp:" };

            bool found = CommandLineHelper.GetOptionValue(
                "/ZH:SHA_256 /Yupch.h /Fpstdafx.pch",
                options,
                CommandLineHelper.OrderOfPrecedence.FirstWins,
                ref optionValue);

            found.Should().BeFalse();
        }

        [Fact]
        public void GetOptionValue_FpWithoutColon_WorksWithDashPrefix()
        {
            // CommandLineHelper accepts both '/' and '-' as switch prefixes
            string optionValue = string.Empty;
            string[] options = { "/Fp" };

            bool found = CommandLineHelper.GetOptionValue(
                "-ZH:SHA_256 -Yupch.h -Fpstdafx.pch",
                options,
                CommandLineHelper.OrderOfPrecedence.FirstWins,
                ref optionValue);

            found.Should().BeTrue();
            optionValue.Should().Be("stdafx.pch");
        }

        [Fact]
        public void GetOptionValue_FpWithoutColon_NotFoundWhenAbsent()
        {
            string optionValue = string.Empty;
            string[] options = { "/Fp" };

            bool found = CommandLineHelper.GetOptionValue(
                "/ZH:SHA_256 /Yupch.h",
                options,
                CommandLineHelper.OrderOfPrecedence.FirstWins,
                ref optionValue);

            found.Should().BeFalse();
            optionValue.Should().BeEmpty();
        }
    }
}
