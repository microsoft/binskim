// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class DiaTests
    {
        private static string SourceLinkPdbPath => Path.Combine(
            PEBinaryTests.TestData, "PE", "Determinism", "OK", "CPlusPlus_SourceLink.pdb");

        [Fact]
        public void Dia_ReadsCompilerAndSourceChecksumMetadata()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var pdb = new Pdb(pdbPath: SourceLinkPdbPath);
            pdb.GlobalScope.Guid.Should().NotBe(Guid.Empty);
            pdb.GetCompilerNameFromCompilandDetails().Should().NotBeNullOrWhiteSpace();

            int nativeCompilandCount = 0;
            int checksumCount = 0;
            foreach (DisposableEnumerableView<Symbol> moduleView in pdb.CreateObjectModuleIterator())
            {
                Symbol module = moduleView.Value;
                ObjectModuleDetails details = module.GetObjectModuleDetails();
                if (details.Language is Language.C or Language.Cxx)
                {
                    nativeCompilandCount++;
                }

                foreach (DisposableEnumerableView<SourceFile> sourceView in pdb.CreateSourceFileIterator(module))
                {
                    SourceFile source = sourceView.Value;
                    source.FileName.Should().NotBeNullOrWhiteSpace();
                    if (source.HashType == HashType.None)
                    {
                        continue;
                    }

                    int expectedLength = source.HashType switch
                    {
                        HashType.MD5 => 16,
                        HashType.SHA1 => 20,
                        HashType.SHA256 => 32,
                        _ => throw new InvalidOperationException($"Unexpected checksum type: {source.HashType}")
                    };

                    source.Hash.Should().HaveCount(expectedLength);
                    checksumCount++;
                }
            }

            nativeCompilandCount.Should().BeGreaterThan(0);
            checksumCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Dia_ReadsSourceLinkStreams()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var pdb = new Pdb(pdbPath: SourceLinkPdbPath);
            string[] sourceLinks = pdb.WindowsPdbGetSourceLinkDocuments().ToArray();
            sourceLinks.Should().NotBeEmpty();

            foreach (string sourceLink in sourceLinks)
            {
                using JsonDocument document = JsonDocument.Parse(sourceLink);
                document.RootElement.TryGetProperty("documents", out JsonElement mappings).Should().BeTrue();
                mappings.ValueKind.Should().Be(JsonValueKind.Object);
                mappings.EnumerateObject().Should().NotBeEmpty();
            }
        }
    }
}
