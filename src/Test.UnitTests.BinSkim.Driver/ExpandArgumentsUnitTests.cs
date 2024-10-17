// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;

using FluentAssertions;

using Microsoft.CodeAnalysis.IL;

using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

using Moq;

using Xunit;


namespace Microsoft.CodeAnalysis.BinSkim.Rules
{
    public class ExpandArgumentsUnitTests
    {
        private static void SetupTestMocks(
            string responseFileName,
            string[] responseFileContents,
            out Mock<IFileSystem> fileSystemMock,
            out Mock<IEnvironmentVariables> environmentVariablesMock)
        {
            fileSystemMock = new Mock<IFileSystem>();
            environmentVariablesMock = new Mock<IEnvironmentVariables>();

            fileSystemMock.Setup(fs => fs.PathGetFullPath(responseFileName)).Returns(responseFileName);
            fileSystemMock.Setup(fs => fs.FileReadAllLines(responseFileName)).Returns(responseFileContents);
            environmentVariablesMock.Setup(ev => ev.ExpandEnvironmentVariables(responseFileName)).Returns(responseFileName);
        }

        [Fact]
        public void GenerateArguments_SucceedsWithEmptyArgumentList()
        {
            string[] result = ExpandArguments.GenerateArguments(Array.Empty<string>(), null, null);

            result.Should().BeEmpty();
        }

        [Fact]
        public void GenerateArguments_SucceedsWithNormalArguments()
        {
            string[] args = new[] { "/y:z", "/x" };

            string[] result = ExpandArguments.GenerateArguments(args, null, null);

            result.Length.Should().Be(2);
            result.Should().ContainInOrder(args);
        }

        [Fact]
        public void GenerateArguments_ExceptionIfResponseFileDoesNotExist()
        {
            string NonexistentResponseFile = Guid.NewGuid().ToString() + ".rsp";
            string[] args = new[] { "/a", "@" + NonexistentResponseFile, "/f" };

            Assert.Throws<FileNotFoundException>(
                () => ExpandArguments.GenerateArguments(args, new FileSystem(), new EnvironmentVariables())
            );
        }

        [Theory]
        [InlineData(new[] { "/b", "/c:val /d", "   /e   " }, new[] { "/a", "/b", "/c:val", "/d", "/e", "/f" })]
        public void GenerateArguments_ExpandsResponseFileContents(string[] rspContent, string[] expected)
        {
            const string ResponseFileName = "Mocked.rsp";
            string[] args = new[] { "/a", "@" + ResponseFileName, "/f" };

            SetupTestMocks(
                ResponseFileName,
                rspContent,
                out Mock<IFileSystem> fileSystemMock,
                out Mock<IEnvironmentVariables> environmentVariablesMock);

            IFileSystem fileSystem = fileSystemMock.Object;
            IEnvironmentVariables environmentVariables = environmentVariablesMock.Object;

            string[] result = ExpandArguments.GenerateArguments(args, fileSystem, environmentVariables);

            result.Should().ContainInOrder(expected);

            fileSystemMock.Verify(fs => fs.PathGetFullPath(ResponseFileName), Times.Once);
            fileSystemMock.Verify(fs => fs.FileReadAllLines(ResponseFileName), Times.Once);
            environmentVariablesMock.Verify(ev => ev.ExpandEnvironmentVariables(ResponseFileName), Times.Once);
        }

        [Theory]
        [InlineData(new[] { "/b", "/c:val /d", "# Random Comment", "   /e   " }, new[] { "/a", "/b", "/c:val", "/d", "/e", "/f" })]
        [InlineData(new[] { "/b", "/c:val /d#Another Comment", "   /e   " }, new[] { "/a", "/b", "/c:val", "/d", "/e", "/f" })]
        public void GenerateArguments_TrimCommentsFromResponseFileContents(string[] rspContent, string[] expected)
        {
            const string ResponseFileName = "Mocked.rsp";
            string[] args = new[] { "/a", "@" + ResponseFileName, "/f" };

            SetupTestMocks(
                ResponseFileName,
                rspContent,
                out Mock<IFileSystem> fileSystemMock,
                out Mock<IEnvironmentVariables> environmentVariablesMock);

            IFileSystem fileSystem = fileSystemMock.Object;
            IEnvironmentVariables environmentVariables = environmentVariablesMock.Object;

            string[] result = ExpandArguments.GenerateArguments(args, fileSystem, environmentVariables);

            result.Should().ContainInOrder(expected);

            fileSystemMock.Verify(fs => fs.PathGetFullPath(ResponseFileName), Times.Once);
            fileSystemMock.Verify(fs => fs.FileReadAllLines(ResponseFileName), Times.Once);
            environmentVariablesMock.Verify(ev => ev.ExpandEnvironmentVariables(ResponseFileName), Times.Once);
        }

        [Theory]
        [InlineData(new[] { "a \"one two\" b" }, new[] { "a", "one two", "b" })]
        public void GenerateArguments_StripsQuotesFromAroundArgsWithSpacesInResponseFiles(string[] rspContent, string[] expected)
        {
            const string ResponseFileName = "Mocked.rsp";
            string[] args = new[] { "@" + ResponseFileName };

            SetupTestMocks(
                ResponseFileName,
                rspContent,
                out Mock<IFileSystem> fileSystemMock,
                out Mock<IEnvironmentVariables> environmentVariablesMock);

            IFileSystem fileSystem = fileSystemMock.Object;
            IEnvironmentVariables environmentVariables = environmentVariablesMock.Object;

            string[] result = ExpandArguments.GenerateArguments(args, fileSystem, environmentVariables);

            result.Length.Should().Be(3);
            result.Should().ContainInOrder(expected);

            fileSystemMock.Verify(fs => fs.PathGetFullPath(ResponseFileName), Times.Once);
            fileSystemMock.Verify(fs => fs.FileReadAllLines(ResponseFileName), Times.Once);
            environmentVariablesMock.Verify(ev => ev.ExpandEnvironmentVariables(ResponseFileName), Times.Once);
        }

        [Theory]
        [InlineData(new[] { "a \"one two\" b" }, new[] { "a", "one two", "b" })]
        public void GenerateArguments_ExpandsEnvironmentVariablesInResponseFilePathName(string[] rspContent, string[] expected)
        {
            const string DirectoryVariableName = "InstallationDirectory";
            const string ResponseFileName = "Mocked.rsp";

            string responseFileNameArgument = string.Format(
                CultureInfo.InvariantCulture,
                @"%{0}%\{1}",
                DirectoryVariableName,
                ResponseFileName
            );

            string[] args = new[] { "@" + responseFileNameArgument };

            SetupTestMocks(
                responseFileNameArgument,
                rspContent,
                out Mock<IFileSystem> fileSystemMock,
                out Mock<IEnvironmentVariables> environmentVariablesMock);

            IFileSystem fileSystem = fileSystemMock.Object;
            IEnvironmentVariables environmentVariables = environmentVariablesMock.Object;

            string[] result = ExpandArguments.GenerateArguments(args, fileSystem, environmentVariables);

            result.Length.Should().Be(3);
            result.Should().ContainInOrder(expected);

            fileSystemMock.Verify(fs => fs.PathGetFullPath(responseFileNameArgument), Times.Once);
            fileSystemMock.Verify(fs => fs.FileReadAllLines(responseFileNameArgument), Times.Once);
            environmentVariablesMock.Verify(ev => ev.ExpandEnvironmentVariables(responseFileNameArgument), Times.Once);
        }
    }
}
