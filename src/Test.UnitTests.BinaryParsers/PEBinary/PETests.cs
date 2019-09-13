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
        }

        private void ExaminePEMetadata(string file, StringBuilder sb)
        {
            Console.WriteLine(file);
        }
    }
}
