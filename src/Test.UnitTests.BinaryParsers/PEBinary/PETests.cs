// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PETests
    {
        [Fact]
        public void IsWixBinary()
        {
            string fileName = Path.Combine(BaselineTestDataDirectory, "Wix_3.11.1_VS2017_Bootstrapper.exe");
            var peBinary = new PEBinary(new Uri(fileName));
            peBinary.Pdb.Should().BeNull();
            peBinary.PE.IsWixBinary.Should().BeTrue();

            // Verify a random other exe to ensure it is properly reporting as not a WIX bootstrapper
            fileName = Path.Combine(BaselineTestDataDirectory, "MixedMode_x64_VS2015_Default.exe");
            peBinary = new PEBinary(new Uri(fileName));
            peBinary.PE.IsWixBinary.Should().BeFalse();
        }

        [Fact]
        public void PEBinary_IsDotNetCoreBootstrapExe()
        {
            string fileName = Path.Combine(BaselineTestDataDirectory, "DotNetCore_win-x64_VS2019_Default.exe");
            PEBinary peBinary;
            using (peBinary = new PEBinary(new Uri(fileName)))
            {
                peBinary.PE.IsDotNetCoreBootstrapExe.Should().BeTrue();
            }

            // Verify a random other exe to ensure it is properly reporting as not a .NET Core bootstrapper
            fileName = Path.Combine(BaselineTestDataDirectory, "Wix_3.11.1_VS2017_Bootstrapper.exe");
            using (peBinary = new PEBinary(new Uri(fileName)))
            {
                peBinary.PE.IsDotNetCoreBootstrapExe.Should().BeFalse();
            }
        }

        [Fact]
        public void PEBinary_CanRecognizeDotNetBootstrappingExe()
        {
            foreach (string nativeUwpFileName in Directory.GetFiles(BaselineTestDataDirectory, "Uwp*Cpp*"))
            {
                if (nativeUwpFileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) { continue; }

                PEBinary peBinary;
                using (peBinary = new PEBinary(new Uri(nativeUwpFileName)))
                {
                    peBinary.PE.IsNativeUniversalWindowsPlatform.Should().BeTrue();
                }
            }

            foreach (string nonNativeUwpFileName in Directory.GetFiles(BaselineTestDataDirectory, "Uwp*"))
            {
                if (nonNativeUwpFileName.Contains("Cpp")) { continue; }
                if (nonNativeUwpFileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) { continue; }

                PEBinary peBinary;
                using (peBinary = new PEBinary(new Uri(nonNativeUwpFileName)))
                {
                    peBinary.PE.IsNativeUniversalWindowsPlatform.Should().BeFalse();
                }
            }
        }

        [Fact]
        public void PE_ComputePortableExecutableMetadata()
        {
            string[] filters = new[] { "*.dll", "*.exe" };
            string testDataDirectory = BaselineTestDataDirectory;

            var sb = new StringBuilder();

            foreach (string filter in filters)
            {
                foreach (string file in Directory.GetFiles(testDataDirectory, filter))
                {
                    this.ExaminePEMetadata(file, sb);
                }
            }

            sb.Length.Should().Be(0, because: sb.ToString());
        }

        private void ExaminePEMetadata(string file, StringBuilder sb)
        {
            var pe = new PE(file);

            bool isNative = file.Contains("Native");

            if (isNative)
            {
                bool isManaged = (pe.IsDotNetCore & pe.IsDotNetCore & pe.IsDotNetFramework & pe.IsDotNetStandard &
                pe.IsILLibrary & pe.IsILOnly & pe.IsMixedMode & pe.IsManaged & pe.IsManagedResourceOnly);

                if (isManaged)
                {
                    sb.Append("Binary was unexpectedly evaluated as both native and managed: " + file);
                }
            }
        }

        [Theory]
        [InlineData("Native_x64_VS2013_Default.dll")]
        [InlineData("Native_x86_VS2013_Default.exe")]
        [InlineData("DotNetCore_win-x64_VS2019_Default.exe")]
        [InlineData("Binskim.win-x64.dll")]
        public void ImageBytes_MatchesFileOnDisk(string fileName)
        {
            string filePath = Path.Combine(BaselineTestDataDirectory, fileName);
            byte[] expected = File.ReadAllBytes(filePath);

            using var pe = new PE(filePath);
            pe.IsPEFile.Should().BeTrue();
            pe.ImageBytes.Should().Equal(expected);
        }

        [Theory]
        [InlineData("Native_x64_VS2013_Default.dll")]
        [InlineData("Native_x86_VS2013_Default.exe")]
        [InlineData("DotNetCore_win-x64_VS2019_Default.exe")]
        [InlineData("Binskim.win-x64.dll")]
        public void SHA256Hash_MatchesStaticComputeFromFile(string fileName)
        {
            string filePath = Path.Combine(BaselineTestDataDirectory, fileName);

            using var pe = new PE(filePath);
            pe.IsPEFile.Should().BeTrue();

            // The instance property should produce the same result as the file-based static method.
            string expected = PE.ComputeSha256Hash(filePath);
            pe.SHA256Hash.Should().Be(expected);
        }

        [Theory]
        [InlineData("Native_x64_VS2013_Default.dll")]
        [InlineData("DotNetCore_win-x64_VS2019_Default.exe")]
        public void SHA1Hash_ProducesValidHash(string fileName)
        {
            string filePath = Path.Combine(BaselineTestDataDirectory, fileName);

            using var pe = new PE(filePath);
            pe.IsPEFile.Should().BeTrue();

            // Should be a 40-char uppercase hex string (160-bit hash).
            pe.SHA1Hash.Should().MatchRegex("^[0-9A-F]{40}$");

            // Should be stable across accesses.
            pe.SHA1Hash.Should().Be(pe.SHA1Hash);
        }

        [Theory]
        [InlineData("Native_x64_VS2013_Default.dll", true)]
        [InlineData("DotNetCore_win-x64_VS2019_Default.exe", true)]
        public void CanLoadBinary_DetectsPEFiles(string fileName, bool expectedResult)
        {
            string filePath = Path.Combine(BaselineTestDataDirectory, fileName);
            PEBinary.CanLoadBinary(new Uri(filePath)).Should().Be(expectedResult);
        }

        [Fact]
        public void CanLoadBinary_ReturnsFalseForNonPEFile()
        {
            string filePath = Path.Combine(BaselineTestDataDirectory, "Native_x64_VS2013_Default.pdb");
            // .pdb files return true via the PDB early-exit path, so use a non-PE, non-PDB file.
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "Not a PE file");
                PEBinary.CanLoadBinary(new Uri(tempFile)).Should().BeFalse();
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void CanLoadBinary_ReturnsFalseForEmptyFile()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                // Empty file (0 bytes).
                PEBinary.CanLoadBinary(new Uri(tempFile)).Should().BeFalse();
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void CanLoadBinary_ReturnsFalseForOneByteFile()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, new byte[] { (byte)'M' });
                PEBinary.CanLoadBinary(new Uri(tempFile)).Should().BeFalse();
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
