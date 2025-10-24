// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// CS0436: The type 'TestHelpers' in 'X' conflicts with the imported type
// 'TestHelpers' in 'Y'. Using the type defined in 'X'.
//
// This type is injected into all test assemblies and this warning therefore
// occurs when test assemblies reference each other. In that case, each test
// assembly will prefer its own copy, which is fine.
#pragma warning disable CS0436

global using static Microsoft.CodeAnalysis.BinSkim.TestHelpers;

using System.IO;
using System.Reflection;

namespace Microsoft.CodeAnalysis.BinSkim
{
    internal static class TestHelpers
    {
        internal static string TestData = GetTestDirectory("Test.UnitTests.BinaryParsers", "TestData");

        internal static string BaselineTestDataDirectory = GetTestDirectory("Test.FunctionalTests.BinSkim.Driver", "BaselineTestData");

        internal static string GetTestDirectory(params string[] relativeDirectories)
        {
            string relativeDirectory = Path.Combine(relativeDirectories);
            string codeBasePath = Assembly.GetExecutingAssembly().Location;
            string dirPath = Path.GetDirectoryName(codeBasePath);
            dirPath = Path.Combine(dirPath, "..", "..", "..", "..", "src");
            dirPath = Path.GetFullPath(dirPath);
            return Path.Combine(dirPath, relativeDirectory);
        }
    }
}
