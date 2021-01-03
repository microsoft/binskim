// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyVersion(Microsoft.CodeAnalysis.IL.VersionConstants.AssemblyVersion)]
[assembly: AssemblyFileVersion(Microsoft.CodeAnalysis.IL.VersionConstants.FileVersion)]

[assembly: AssemblyProduct("BinSkim Portable Executable Analyzer")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Microsoft")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

[assembly: InternalsVisibleTo("BinSkim")]
[assembly: InternalsVisibleTo("UnitTests.BinSkim.Rules")]
[assembly: InternalsVisibleTo("Test.UnitTests.BinaryParsers")]
[assembly: InternalsVisibleTo("Test.FunctionalTests.BinSkim.Driver")]
[assembly: InternalsVisibleTo("BinSkim.Driver.FunctionalTests")]
[assembly: InternalsVisibleTo("BinSkim.Rules.FunctionalTests")]

