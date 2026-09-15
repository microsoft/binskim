// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;

using Xunit;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    /// <summary>
    /// Regression test for IcM 798776046: portable PDB MetadataReaderProvider and
    /// FileStream lifetime bug in PE.ManagedPdbGetSourceLinkDocument.
    ///
    /// On the unfixed (main) code, TryGetPortablePdbMetadataReader opens the PDB file but
    /// never roots the MetadataReaderProvider or disposes the FileStream. On .NET 9 with
    /// Dynamic PGO, the GC collects the unrooted provider while BlobReader still points at
    /// its memory, causing AccessViolationException in ReadUTF8.
    ///
    /// The fix makes PE own the MetadataReaderProvider and FileStream as instance fields,
    /// disposing them in PE.Dispose(). The PDB data stays valid for the PE's analysis
    /// lifetime and the file handle is released deterministically on disposal.
    ///
    /// This test verifies the PDB file is NOT locked after PE disposal.
    /// On main (unfixed), File.Open with FileShare.None fails because the leaked stream
    /// still holds the file open. On the fix, PE.Dispose() releases it.
    /// </summary>
    public class BA2027_PortablePdbLifetimeTests
    {
        private static readonly string TestDataDirectory = Path.Combine(
            Environment.CurrentDirectory,
            "FunctionalTestData",
            "BA2027.EnableSourceLink",
            "Pass");

        /// <summary>
        /// Verifies that PE.Dispose() releases the PDB FileStream opened by
        /// ManagedPdbGetSourceLinkDocument. FAILS on main (unfixed), PASSES on the fix.
        /// </summary>
        [Fact]
        public void ManagedPdbGetSourceLinkDocument_ReleasesFileHandle_AfterDispose()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string sourceDll = Path.Combine(TestDataDirectory, "CSharp_PortablePdb_SourceLink.dll");
            string sourcePdb = Path.Combine(TestDataDirectory, "CSharp_PortablePdb_SourceLink.pdb");

            File.Exists(sourceDll).Should().BeTrue(
                because: "BA2027 PortablePdb test fixture must be deployed to FunctionalTestData.");
            File.Exists(sourcePdb).Should().BeTrue(
                because: "BA2027 PortablePdb test fixture must be deployed to FunctionalTestData.");

            // Copy to an isolated temp directory so other tests/DIA don't interfere.
            string tempDir = Path.Combine(Path.GetTempPath(), "BinSkim_PdbLifetime_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string tempDll = Path.Combine(tempDir, "CSharp_PortablePdb_SourceLink.dll");
                string tempPdb = Path.Combine(tempDir, "CSharp_PortablePdb_SourceLink.pdb");
                File.Copy(sourceDll, tempDll);
                File.Copy(sourcePdb, tempPdb);

                string sourceLinkResult;

                using (var pe = new PE(tempDll))
                {
                    // TryOpenAssociatedPortablePdb finds the PDB adjacent to the DLL,
                    // so the Pdb parameter's PdbLocation is never accessed.
                    sourceLinkResult = pe.ManagedPdbGetSourceLinkDocument(pdb: null);
                }

                // Verify the code path was actually exercised.
                sourceLinkResult.Should().NotBeNullOrEmpty(
                    because: "TryOpenAssociatedPortablePdb should find the PDB adjacent to the DLL " +
                             "and extract the SourceLink document.");

                // THE REGRESSION ASSERTION:
                // On main (unfixed): the FileStream is leaked — this throws IOException.
                // On the fix: PE.Dispose() released the stream — this succeeds.
                Action openExclusive = () =>
                {
                    using (File.Open(tempPdb, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                    }
                };

                openExclusive.Should().NotThrow<IOException>(
                    because: "PE.Dispose() should release the PDB FileStream. " +
                             "A leaked handle (the original bug) prevents exclusive access.");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }

        /// <summary>
        /// Same regression test for the checksum extraction code path.
        /// </summary>
        [Fact]
        public void ManagedPdbSourceFileChecksumAlgorithm_ReleasesFileHandle_AfterDispose()
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            string sourceDll = Path.Combine(TestDataDirectory, "CSharp_PortablePdb_SourceLink.dll");
            string sourcePdb = Path.Combine(TestDataDirectory, "CSharp_PortablePdb_SourceLink.pdb");

            File.Exists(sourceDll).Should().BeTrue(
                because: "BA2027 PortablePdb test fixture must be deployed to FunctionalTestData.");
            File.Exists(sourcePdb).Should().BeTrue(
                because: "BA2027 PortablePdb test fixture must be deployed to FunctionalTestData.");

            string tempDir = Path.Combine(Path.GetTempPath(), "BinSkim_PdbLifetime_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string tempDll = Path.Combine(tempDir, "CSharp_PortablePdb_SourceLink.dll");
                string tempPdb = Path.Combine(tempDir, "CSharp_PortablePdb_SourceLink.pdb");
                File.Copy(sourceDll, tempDll);
                File.Copy(sourcePdb, tempPdb);

                ChecksumAlgorithmType checksumResult;

                using (var pe = new PE(tempDll))
                {
                    checksumResult = pe.ManagedPdbSourceFileChecksumAlgorithm(
                        PdbFileType.Portable, pdb: null);
                }

                checksumResult.Should().NotBe(ChecksumAlgorithmType.Unknown,
                    because: "the portable PDB should have a valid checksum algorithm.");

                Action openExclusive = () =>
                {
                    using (File.Open(tempPdb, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                    }
                };

                openExclusive.Should().NotThrow<IOException>(
                    because: "PE.Dispose() should release the PDB FileStream.");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }
}
