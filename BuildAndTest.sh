#!/bin/bash

dotnet build src/BinSkim.sln --configuration Release

dotnet test bld/bin/Test.FunctionalTests.BinSkim.Driver/release/Test.FunctionalTests.BinSkim.Driver.dll
dotnet test bld/bin/Test.FunctionalTests.BinSkim.Rules/release/Test.FunctionalTests.BinSkim.Rules.dll
dotnet test bld/bin/Test.UnitTests.BinaryParsers/release/Test.UnitTests.BinaryParsers.dll
dotnet test bld/bin/Test.UnitTests.BinSkim.Rules/release/Test.UnitTests.BinSkim.Rules.dll