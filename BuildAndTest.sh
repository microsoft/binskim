#!/bin/bash

echo $(uname)
if [[  "$(uname)" == "Linux" || "$(uname)" == "Darwin" ]]; then
  echo "Changing paths in BinSkim SLN to non-Windows paths due to msbuild issue #1957 (https://github.com/microsoft/msbuild/issues/1957)"
  sed 's#\\#/#g' src/BinSkim.sln > src/BinSkimUnix.sln
fi


dotnet build src/BinSkimUnix.sln --configuration Release /p:Platform="x64"

dotnet test bld/bin/x64_Release/net8.0/Test.FunctionalTests.BinSkim.Driver.dll
dotnet test bld/bin/x64_Release/net8.0/Test.FunctionalTests.BinSkim.Rules.dll
dotnet test bld/bin/x64_Release/net8.0/Test.UnitTests.BinaryParsers.dll
dotnet test bld/bin/x64_Release/net8.0/Test.UnitTests.BinSkim.Rules.dll