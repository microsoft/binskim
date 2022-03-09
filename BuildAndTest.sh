#!/bin/bash

if [[  "$(uname)" == "Linux" ]]; then
  echo "Changing paths in BinSkim SLN to non-Windows paths due to msbuild issue #1957 (https://github.com/microsoft/msbuild/issues/1957)"
  sed 's#\\#/#g' src/BinSkim.sln > src/BinSkimLinux.sln
fi

dotnet build src/BinSkimLinux.sln --configuration Release

dotnet test src/Test.FunctionalTests.BinSkim.Driver/Test.FunctionalTests.BinSkim.Driver.csproj --no-build --configuration Release /p:Platform="x64"
dotnet test src/Test.FunctionalTests.BinSkim.Rules/Test.FunctionalTests.BinSkim.Rules.csproj --no-build --configuration Release /p:Platform="x64"
dotnet test src/Test.UnitTests.BinaryParsers/Test.UnitTests.BinaryParsers.csproj --no-build --configuration Release /p:Platform="x64"
dotnet test src/Test.UnitTests.BinSkim.Rules/Test.UnitTests.BinSkim.Rules.csproj --no-build --configuration Release /p:Platform="x64"