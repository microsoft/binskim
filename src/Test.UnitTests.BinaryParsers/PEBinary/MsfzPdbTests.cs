// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class MsfzPdbTests
    {
        public static IEnumerable<object[]> WindowsAndMsfzPdbPairs =>
            GetPdbPairs(PEBinaryTests.TestData)
                .Concat(GetPdbPairs(PEBinaryTests.BaselineTestDataDirectory));

        [Theory]
        [MemberData(nameof(WindowsAndMsfzPdbPairs))]
        public void Pdb_MsfzEncoding_PreservesDiaContent(string windowsPdbPath, string msfzPdbPath)
        {
            if (!PlatformSpecificHelpers.RunningOnWindows()) { return; }

            using var windowsPdb = new Pdb(windowsPdbPath, traceLoads: true);
            using var msfzPdb = new Pdb(msfzPdbPath, traceLoads: true);

            windowsPdb.FileType.Should().Be(PdbFileType.Windows);
            msfzPdb.FileType.Should().Be(PdbFileType.WindowsMSFZ);
            msfzPdb.GlobalScope.Name.Should().Be(windowsPdb.GlobalScope.Name);
            msfzPdb.IsStripped.Should().Be(windowsPdb.IsStripped);
            msfzPdb.GetCompilerNameFromCompilandDetails().Should().Be(windowsPdb.GetCompilerNameFromCompilandDetails());
            GetCompilandSummary(msfzPdb).Should().Equal(GetCompilandSummary(windowsPdb));
            GetSourceFileSummary(msfzPdb).Should().Equal(GetSourceFileSummary(windowsPdb));
            msfzPdb.WindowsPdbGetSourceLinkDocuments().Should().Equal(windowsPdb.WindowsPdbGetSourceLinkDocuments());
        }

        private static IEnumerable<string> GetCompilandSummary(Pdb pdb)
        {
            foreach (DisposableEnumerableView<Symbol> compilandView in pdb.CreateObjectModuleIterator())
            {
                Symbol compiland = compilandView.Value;
                ObjectModuleDetails details = compiland.GetObjectModuleDetails();
                yield return string.Join("|", compiland.Name, details.Library, details.Language);
            }
        }

        private static IEnumerable<string> GetSourceFileSummary(Pdb pdb)
        {
            foreach (DisposableEnumerableView<Symbol> compilandView in pdb.CreateObjectModuleIterator())
            {
                foreach (DisposableEnumerableView<SourceFile> sourceFileView in pdb.CreateSourceFileIterator(compilandView.Value))
                {
                    SourceFile sourceFile = sourceFileView.Value;
                    yield return string.Join("|", sourceFile.FileName, sourceFile.HashType, Convert.ToHexString(sourceFile.Hash));
                }
            }
        }

        private static IEnumerable<object[]> GetPdbPairs(string directory)
        {
            foreach (string msfzPdbPath in Directory.GetFiles(directory, "*.pdb", SearchOption.AllDirectories)
                .Where(path => string.Equals(
                    Path.GetFileName(Path.GetDirectoryName(path)),
                    "msfz",
                    StringComparison.OrdinalIgnoreCase)))
            {
                string windowsPdbPath = Path.Combine(
                    Directory.GetParent(Path.GetDirectoryName(msfzPdbPath)).FullName,
                    Path.GetFileName(msfzPdbPath));
                yield return new object[] { windowsPdbPath, msfzPdbPath };
            }
        }
    }
}
