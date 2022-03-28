// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

using Dia2Lib;

using FluentAssertions;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Moq;

using Xunit;

namespace Microsoft.CodeAnalysis.BinaryParsers
{
    public class PdbTests
    {
        [Fact]
        public void PdbShouldReturnNullResultIfEnumTablesIsNull()
        {
            // given
            Mock<IDiaDataSource> mockDiaDataSource = new Mock<IDiaDataSource>();
            Mock<IDiaSession> mockDiaSession = new Mock<IDiaSession>();
            IDiaSession passableMockDiaSession = mockDiaSession.Object;
            mockDiaDataSource.Setup(source => source.openSession(out passableMockDiaSession));

            string fileName = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, "Native_x86_VS2017_15.5.4_PdbStripped.pdb");
            var testPdb = new Pdb(pdbPath: fileName, diaDataSource: mockDiaDataSource.Object);

            HashSet<uint> actualResultSet = testPdb.GenerateWritableSegmentSet();

            actualResultSet.Should().BeNullOrEmpty();
        }

        [Fact]
        public void PdbShouldCreateEmptySourceFileIteratorIfInObjectModuleIsNull()
        {

            string fileName = Path.Combine(PEBinaryTests.BaselineTestDataDirectory, "Native_x86_VS2017_15.5.4_PdbStripped.pdb");
            var testPdb = new Pdb(pdbPath: fileName);

            DisposableEnumerable<SourceFile> result = testPdb.CreateSourceFileIterator(null);

            result.Should().BeNullOrEmpty();
        }
    }
}
