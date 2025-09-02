#!/bin/bash

dotnet build src/BinSkim.sln --configuration Release

dotnet test bld/bin/Release/net9.0/Test.FunctionalTests.BinSkim.Driver.dll
dotnet test bld/bin/Release/net9.0/Test.FunctionalTests.BinSkim.Rules.dll
dotnet test bld/bin/Release/net9.0/Test.UnitTests.BinaryParsers.dll
dotnet test bld/bin/Release/net9.0/Test.UnitTests.BinSkim.Rules.dll