@echo off
SETLOCAL
@REM Uncomment this line to update nuget.exe
@REM Doing so can break SLN build (which uses nuget.exe to
@REM create a nuget package for binskim) so must opt-in
@REM %~dp0.nuget\NuGet.exe update -self

echo Updating submodules...
git submodule update --init --recursive || exit /b 1

echo Restoring roslyn nuget dependencies...
call src\Roslyn\Restore.cmd || exit /b 1

echo Restoring binskim nuget dependencies...
%~dp0.nuget\NuGet.exe restore -PackagesDirectory src\packages src\BinaryParsers\BinaryParsers.csproj || exit /b 1
%~dp0.nuget\NuGet.exe restore -PackagesDirectory src\packages src\BinSkim.Driver\BinSkim.Driver.csproj || exit /b 1
%~dp0.nuget\NuGet.exe restore -PackagesDirectory src\packages src\BinSkim.Rules\BinSkim.Rules.csproj || exit /b 1
%~dp0.nuget\NuGet.exe restore -PackagesDirectory src\packages src\BinSkim.Sdk\BinSkim.Sdk.csproj || exit /b 1
%~dp0.nuget\NuGet.exe restore -PackagesDirectory src\packages src\BinSkim.Driver.FunctionalTests\BinSkim.Driver.FunctionalTests.csproj || exit /b 1
%~dp0.nuget\NuGet.exe restore -PackagesDirectory src\packages src\BinSkim.Rules.FunctionalTests\BinSkim.Rules.FunctionalTests.csproj || exit /b 1

