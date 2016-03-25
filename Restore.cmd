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

@REM NOTE: We restore individual projects instead of the entire .sln because our version of nuget.exe can't 
@REM handle roslyn projects which do their restore above.
for %%p in (
  BinaryParsers
  BinSkim.Driver 
  BinSkim.Rules
  BinSkim.Sdk 
  BinSkim.Driver.FunctionalTests 
  BinSkim.Rules.FunctionalTests
) do (
  %~dp0.nuget\NuGet.exe restore -ConfigFile src\NuGet.config -PackagesDirectory src\packages src\%%p\%%p.csproj || exit /b 1
)
