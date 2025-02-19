@echo on
call SetCurrentVersion.cmd

if "%PRERELEASE%" EQU "-developer" (
echo Attempt to push working bits. Fix prerelease value and rebuild && goto Exit)
)

set VERSION=%MAJOR%.%MINOR%.%PATCH%.%REV%%PRERELEASE%
set NUGET=.nuget\nuget.exe
set SOURCE=https://nuget.org

if exist ..\SetNugetSarifKey.cmd (
call ..\SetNugetSarifKey.cmd
call %NUGET% SetApiKey %API_KEY% -Source %SOURCE%
)
if "%ERRORLEVEL%" NEQ "0" (echo set api key of %API_KEY% to %SOURCE% FAILED && goto Exit)

@REM Publish BinSkim
set ID=Microsoft.CodeAnalysis.BinSkim
set PACKAGE_ROOT=bld\bin\nuget\%ID%.%VERSION%

call %NUGET% push %PACKAGE_ROOT%.nupkg -Source %SOURCE%
if "%ERRORLEVEL%" NEQ "0" (echo push to %SOURCE% FAILED && goto Exit)

:Exit
