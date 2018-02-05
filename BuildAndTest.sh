#!/bin/bash

if [[  "$(uname)" == "Linux" ]]; then
  echo "Changing paths in BinSkim SLN to non-Windows paths due to msbuild issue #1957 (https://github.com/microsoft/msbuild/issues/1957)"
  sed 's#\\#/#g' src/BinSkim.sln > src/BinSkimLinux.sln
fi

dotnet build src/BinSkimLinux.sln

dotnet test src/BinSkim.Driver.FunctionalTests/BinSkim.Driver.FunctionalTests.csproj --no-build
dotnet test src/BinSkim.Rules.FunctionalTests/BinSkim.Rules.FunctionalTests.csproj --no-build
dotnet test src/Test.UnitTests.BinaryParsers/Test.UnitTests.BinaryParsers.csproj --no-build