// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

using Dia2Lib;

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

        [Theory]
        [InlineData("Determinism/OK/CPlusPlus_SourceLink.pdb", true)]
        [InlineData("Determinism/OK/msfz/CPlusPlus_SourceLink.pdb", true)]
        [InlineData("clangcl.14.pe.c.codeview.pdbpagesize_8192.exe.pdb", false)]
        [InlineData("Native_x64_RustC_Rust_debuginfo2_v1.59.0.pdb", false)]
        public void Dia_ReadsRequiredNativeMetadata(string fixture, bool requireSourceLink)
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string path = Path.Combine(PEBinaryTests.TestData, "PE",
                fixture.Replace('/', Path.DirectorySeparatorChar));
            SortedDictionary<string, string[]> snapshot = CaptureContract(path);

            snapshot["Compilands"].Should().NotBeEmpty();
            snapshot["Functions"].Should().NotBeEmpty();
            if (requireSourceLink)
            {
                snapshot["Sources"].Should().NotBeEmpty();
                snapshot["SourceLink"].Should().NotBeEmpty();
                snapshot["Segments"].Should().NotBeEmpty();
                snapshot["SectionContributions"].Should().NotBeEmpty();
            }
        }

        [Fact]
        public void Dia_ExecutableCompilandMembershipMatchesSectionContributions()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            // DIA symbol IDs are session-local, so compare compiland names.
            var expected = new HashSet<string>(StringComparer.Ordinal);
            ForEachDiaTable(SourceLinkPdbPath, (session, table) =>
            {
                if (table is not IDiaEnumSectionContribs contributionTable) { return; }

                for (uint item = 0; item < contributionTable.Count; item++)
                {
                    IDiaSectionContrib contribution = contributionTable.Item(item);
                    try
                    {
                        if (contribution.execute == 0)
                        {
                            continue;
                        }

                        session.symbolById(contribution.compilandId, out IDiaSymbol compiland);
                        try
                        {
                            expected.Add(compiland.name);
                        }
                        finally { ResourceReleaser.Release(compiland); }
                    }
                    finally { ResourceReleaser.Release(contribution); }
                }
            });

            expected.Should().NotBeEmpty();

            using var pdb = new Pdb(pdbPath: SourceLinkPdbPath);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            var moduleNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (DisposableEnumerableView<Symbol> moduleView in pdb.CreateObjectModuleIterator())
            {
                Symbol module = moduleView.Value;
                moduleNames.Add(module.Name).Should().BeTrue("each object module must have a unique name");
                if (pdb.CompilandWithIdIsInExecutableSectionContrib(module.SymIndexId))
                {
                    actual.Add(module.Name);
                }
            }

            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void Dia_LoadsTheConfiguredNativeLibrary()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var pdb = new Pdb(pdbPath: SourceLinkPdbPath);
            pdb.GlobalScope.Should().NotBeNull();

            string expectedDll = DiaCandidatePath ?? Path.Combine(AppContext.BaseDirectory, "msdia140.dll");
            using Process process = Process.GetCurrentProcess();
            process.Modules.Cast<ProcessModule>().Should().Contain(module =>
                Path.GetFullPath(module.FileName).Equals(Path.GetFullPath(expectedDll), StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Dia_MsfzPreservesNativeContract()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string msfzPath = Path.Combine(Path.GetDirectoryName(SourceLinkPdbPath),
                "msfz", Path.GetFileName(SourceLinkPdbPath));
            SortedDictionary<string, string[]> classic = CaptureContract(SourceLinkPdbPath);
            SortedDictionary<string, string[]> msfz = CaptureContract(msfzPath);
            msfz.Keys.Should().Equal(classic.Keys);
            foreach (string section in classic.Keys)
            {
                msfz[section].Should().Equal(classic[section], $"MSFZ preserves {section}");
            }
        }

        private static SortedDictionary<string, string[]> CaptureContract(string path)
        {
            using var pdb = new Pdb(pdbPath: path);
            var compilands = new List<string>();
            var sources = new List<string>();
            var functions = new List<string>();
            foreach (DisposableEnumerableView<Symbol> moduleView in pdb.CreateObjectModuleIterator())
            {
                Symbol module = moduleView.Value;
                ObjectModuleDetails details = module.GetObjectModuleDetails();
                compilands.Add(JsonSerializer.Serialize(new
                {
                    details.Name,
                    details.Library,
                    details.CompilerName,
                    FrontEnd = details.CompilerFrontEndVersion.ToString(),
                    BackEnd = details.CompilerBackEndVersion.ToString(),
                    details.Language,
                    details.HasSecurityChecks,
                    details.HasDebugInfo,
                    details.RawCommandLine,
                    details.RawLinkerCommandLine,
                    ExecutableContribution = pdb.CompilandWithIdIsInExecutableSectionContrib(module.SymIndexId)
                }));

                foreach (DisposableEnumerableView<SourceFile> sourceView in pdb.CreateSourceFileIterator(module))
                {
                    SourceFile source = sourceView.Value;
                    sources.Add(JsonSerializer.Serialize(new
                    {
                        Module = module.Name,
                        source.FileName,
                        source.HashType,
                        Hash = Convert.ToHexString(source.Hash)
                    }));
                }
            }

            foreach (DisposableEnumerableView<Symbol> functionView in pdb.CreateGlobalFunctionIterator())
            {
                Symbol function = functionView.Value;
                functions.Add(JsonSerializer.Serialize(new
                {
                    function.Name,
                    function.RawCxxUndecoratedName,
                    function.IsSafeBuffers,
                    function.HasSecurityChecks,
                    function.IsManaged,
                    function.LocationType
                }));
            }

            var segments = new List<string>();
            var contributions = new List<string>();
            ForEachDiaTable(path, (_, table) =>
            {
                if (table is IDiaEnumSegments segmentTable)
                {
                    for (uint item = 0; item < segmentTable.Count; item++)
                    {
                        IDiaSegment segment = segmentTable.Item(item);
                        try
                        {
                            segments.Add(JsonSerializer.Serialize(new
                            {
                                segment.addressSection,
                                segment.length,
                                segment.read,
                                segment.write,
                                segment.execute,
                                Writable = pdb.IsSegmentWithIdWritable(segment.addressSection)
                            }));
                        }
                        finally { ResourceReleaser.Release(segment); }
                    }
                }
                if (table is IDiaEnumSectionContribs contributionTable)
                {
                    for (uint item = 0; item < contributionTable.Count; item++)
                    {
                        IDiaSectionContrib contribution = contributionTable.Item(item);
                        try
                        {
                            contributions.Add(JsonSerializer.Serialize(new
                            {
                                contribution.compilandId,
                                contribution.addressSection,
                                contribution.addressOffset,
                                contribution.length,
                                contribution.read,
                                contribution.write,
                                contribution.execute
                            }));
                        }
                        finally { ResourceReleaser.Release(contribution); }
                    }
                }
            });

            return new SortedDictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Identity"] = new[] { pdb.GlobalScope.Guid.ToString(), pdb.IsStripped.ToString() },
                ["Compilands"] = compilands.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ["Sources"] = sources.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ["Functions"] = functions.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ["SourceLink"] = pdb.WindowsPdbGetSourceLinkDocuments().OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ["Segments"] = segments.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ["SectionContributions"] = contributions.OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
        }

        private static void ForEachDiaTable(string path, Action<IDiaSession, IDiaTable> action)
        {
            IDiaDataSource sourceData = MsdiaComWrapper.GetDiaSource();
            IDiaSession session = null;
            IDiaEnumTables tables = null;
            try
            {
                sourceData.loadDataFromPdb(path);
                sourceData.openSession(out session);
                session.getEnumTables(out tables);
                for (int index = 0; index < tables.Count; index++)
                {
                    IDiaTable table = tables.Item(index);
                    try
                    {
                        action(session, table);
                    }
                    finally { ResourceReleaser.Release(table); }
                }
            }
            finally
            {
                if (tables != null) { ResourceReleaser.Release(tables); }
                if (session != null) { ResourceReleaser.Release(session); }
                ResourceReleaser.Release(sourceData);
            }
        }
    }
}
