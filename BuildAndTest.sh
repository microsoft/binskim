#!/bin/bash

echo $(uname)
if [[  "$(uname)" == "Linux" || "$(uname)" == "Darwin" ]]; then
  echo "Changing paths in BinSkim SLN to non-Windows paths due to msbuild issue #1957 (https://github.com/microsoft/msbuild/issues/1957)"
  sed 's#\\#/#g' src/BinSkim.sln > src/BinSkimUnix.sln
fi

dotnet build src/BinSkimUnix.sln --configuration Release

dotnet test src/Test.FunctionalTests.BinSkim.Driver/Test.FunctionalTests.BinSkim.Driver.csproj --no-build --configuration Release /p:Platform="x64"
if [ $? != 0 ]
then
  >&2 echo "Test(s) failed in project: Test.FunctionalTests.BinSkim.Driver"
  exit $?
fi

dotnet test src/Test.FunctionalTests.BinSkim.Rules/Test.FunctionalTests.BinSkim.Rules.csproj --no-build --configuration Release /p:Platform="x64"
if [ $? != 0 ]
then
  >&2 echo "Test(s) failed in project: Test.FunctionalTests.BinSkim.Rules"
  exit $?
fi

dotnet test src/Test.UnitTests.BinaryParsers/Test.UnitTests.BinaryParsers.csproj --no-build --configuration Release /p:Platform="x64"
if [ $? != 0 ]
then
  >&2 echo "Test(s) failed in project: Test.UnitTests.BinaryParsers"
  exit $?
fi

dotnet test src/Test.UnitTests.BinSkim.Rules/Test.UnitTests.BinSkim.Rules.csproj --no-build --configuration Release /p:Platform="x64"
if [ $? != 0 ]
then
  >&2 echo "Test(s) failed in project: Test.UnitTests.BinSkim.Rules"
  exit $?
fi