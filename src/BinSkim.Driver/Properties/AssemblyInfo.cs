// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("BinSkim PE/MSIL Analysis Driver")]
[assembly: AssemblyDescription("A security and correctness analyzer for portable executable and MSIL formats.")]

[assembly: InternalsVisibleTo("BinSkim.Driver.FunctionalTests")]
[assembly: InternalsVisibleTo("BinSkim.Rules.FunctionalTests")]