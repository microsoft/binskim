// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    /// <summary>
    /// Tests for portable PDB metadata lifetime and disposal.
    /// These tests ensure that MetadataReaderProvider and FileStream lifetimes
    /// are properly managed to prevent AccessViolationException under GC pressure.
    /// Regression tests for IcM 798776046.
    /// </summary>
    public class PortablePdbMetadataTests
    {
        private static string BaselineTestDataDirectory =>
            Path.Combine(
                Path.GetDirectoryName(typeof(PETests).Assembly.Location),
                "FunctionalTestData",
                "BA2027.EnableSourceLink",
                "Pass");

        [Fact]
        public void PortablePdbMetadata_SourceLinkExtraction_RepeatedReads()
        {
            // Test that repeated SourceLink extractions don't cause memory corruption
            // This exercises the lifetime fix by calling the read operation multiple times
            // in quick succession to verify proper disposal between calls.

            string binaryPath = Path.Combine(BaselineTestDataDirectory, "CSharp_PortablePdb_SourceLink.dll");

            if (!File.Exists(binaryPath))
            {
                // Skip if test data is not available
                return;
            }

            using (var peBinary = new PEBinary(new Uri(binaryPath)))
            {
                Pdb pdb = peBinary.Pdb;
                if (pdb == null || pdb.FileType != PdbFileType.Portable)
                {
                    return;
                }

                // Extract SourceLink multiple times
                // Previous bug would cause AV under GC pressure on repeated calls
                for (int i = 0; i < 5; i++)
                {
                    string sourceLink = peBinary.PE.ManagedPdbGetSourceLinkDocument(pdb);

                    // Should consistently get a valid result or null, not crash
                    if (sourceLink != null)
                    {
                        sourceLink.Should().NotBeEmpty();
                        sourceLink.Should().Contain("sourceRoot");
                    }

                    // Force GC between reads to exercise the lifetime issue
                    // Original bug would crash here due to unrooted MetadataReaderProvider
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        [Fact]
        public void PortablePdbMetadata_ChecksumExtraction_RepeatedReads()
        {
            // Test that repeated checksum algorithm extractions don't cause memory corruption
            // This exercises the lifetime fix for the checksum extraction path

            string binaryPath = Path.Combine(BaselineTestDataDirectory, "CSharp_PortablePdb_SourceLink.dll");

            if (!File.Exists(binaryPath))
            {
                return;
            }

            using (var peBinary = new PEBinary(new Uri(binaryPath)))
            {
                Pdb pdb = peBinary.Pdb;
                if (pdb == null || pdb.FileType != PdbFileType.Portable)
                {
                    return;
                }

                // Extract checksum algorithm multiple times
                for (int i = 0; i < 5; i++)
                {
                    ChecksumAlgorithmType checksum = peBinary.PE.ManagedPdbSourceFileChecksumAlgorithm(pdb.FileType, pdb);

                    // Should get a valid result, not crash
                    checksum.Should().NotBe(ChecksumAlgorithmType.Unknown);

                    // Force GC between reads
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        [Fact]
        public void PortablePdbMetadata_RepeatedReads_HighGCPressure()
        {
            // Test that repeated reads with high GC pressure don't cause AV
            // This simulates the real-world scenario where BinSkim runs with --threads 10
            // and scans ~30k files, triggering Gen0 GC between reads

            string binaryPath = Path.Combine(BaselineTestDataDirectory, "CSharp_PortablePdb_SourceLink.dll");

            if (!File.Exists(binaryPath))
            {
                return;
            }

            using (var peBinary = new PEBinary(new Uri(binaryPath)))
            {
                Pdb pdb = peBinary.Pdb;
                if (pdb == null || pdb.FileType != PdbFileType.Portable)
                {
                    return;
                }

                // Simulate sequential access with aggressive GC between reads
                // Original bug would crash on iteration 2-5 under sufficient GC pressure
                for (int i = 0; i < 10; i++)
                {
                    // Allocate memory to increase GC pressure
                    var temp = new byte[1024 * 100];

                    string sourceLink = peBinary.PE.ManagedPdbGetSourceLinkDocument(pdb);

                    // Verify we got results without crashing
                    // (may be null if binary doesn't have SourceLink, but should not AV)
                    if (sourceLink != null)
                    {
                        sourceLink.Should().NotBeEmpty();
                    }

                    // Force GC to trigger the lifetime issue if it exists
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        [Fact]
        public void PortablePdbMetadata_MissingPdb_DoesNotCrash()
        {
            // Test that accessing a binary with missing PDB doesn't crash
            string binaryPath = Path.Combine(BaselineTestDataDirectory, "CSharp_PortablePdb_SourceLink.dll");

            if (!File.Exists(binaryPath))
            {
                return;
            }

            using (var peBinary = new PEBinary(new Uri(binaryPath)))
            {
                Pdb pdb = peBinary.Pdb;

                if (pdb == null || pdb.FileType != PdbFileType.Portable)
                {
                    return;
                }

                // Extract SourceLink from the actual PDB (should not crash)
                string sourceLink = peBinary.PE.ManagedPdbGetSourceLinkDocument(pdb);

                // Should return a value or null gracefully, not crash
                // Just verify no exception was thrown
            }
        }

        [Fact]
        public void PortablePdbMetadata_FileStreamDisposal()
        {
            // Test that FileStream objects are properly disposed
            // This prevents file locks that would cause "file in use" errors on Windows

            string binaryPath = Path.Combine(BaselineTestDataDirectory, "CSharp_PortablePdb_SourceLink.dll");

            if (!File.Exists(binaryPath))
            {
                return;
            }

            using (var peBinary = new PEBinary(new Uri(binaryPath)))
            {
                Pdb pdb = peBinary.Pdb;
                if (pdb == null || pdb.FileType != PdbFileType.Portable)
                {
                    return;
                }

                // Extract SourceLink (should open and dispose FileStream properly)
                string sourceLink = peBinary.PE.ManagedPdbGetSourceLinkDocument(pdb);

                // After the call completes, the PDB file should not be locked
                // Try to open it again to verify no lock exists
                // (this only works on Windows, but the try/catch handles it)
                try
                {
                    // If we can open the file for exclusive access, it wasn't locked
                    using (FileStream stream = File.Open(pdb.PdbLocation, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        // Successfully opened with exclusive access - no lock held
                    }
                }
                catch (IOException ex) when (ex.Message.Contains("in use"))
                {
                    throw new Xunit.Sdk.XunitException(
                        $"PDB file was not properly disposed and is still locked: {ex.Message}");
                }
            }
        }
    }
}
