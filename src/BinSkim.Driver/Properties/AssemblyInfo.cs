// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("BinSkim PE/MSIL Analysis Driver")]
[assembly: AssemblyDescription("A security and correctness analyzer for portable executable and MSIL formats.")]

[assembly: InternalsVisibleTo("Test.FunctionalTests.BinSkim.Driver")]
[assembly: InternalsVisibleTo("Test.FunctionalTests.BinSkim.Rules")]
[assembly: InternalsVisibleTo("Test.UnitTests.BinSkim.Driver")]
