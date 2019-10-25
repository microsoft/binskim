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
            string fileName = Path.Combine(PEBinaryTests.BaselineTestsDataDirectory, "Wix_3.11.1_VS2017_Bootstrapper.exe");
            PEBinary peBinary = new PEBinary(new Uri(fileName));
            peBinary.Pdb.Should().BeNull();
            peBinary.PE.IsWixBinary.Should().BeTrue();

            // Verify a random other exe to ensure it is properly reporting as not a WIX bootstrapper
            fileName = Path.Combine(PEBinaryTests.BaselineTestsDataDirectory, "MixedMode_x64_VS2015_Default.exe");
            peBinary = new PEBinary(new Uri(fileName));
            peBinary.PE.IsWixBinary.Should().BeFalse();
        }

        [Fact]
        public void PE_ComputePortableExecutableMetadata()
        {
            string[] filters = new[] { "*.dll", "*.exe" };
            string testsDataDirectory = PEBinaryTests.BaselineTestsDataDirectory;

            var sb = new StringBuilder();

            foreach (string filter in filters)
            {
                foreach (string file in Directory.GetFiles(testsDataDirectory, filter))
                {
                    ExaminePEMetadata(file, sb);
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
    }
}
