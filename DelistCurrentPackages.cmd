@echo on
call SetCurrentVersion.cmd

set VERSION=%MAJOR_PREVIOUS%.%MINOR_PREVIOUS%.%PATCH_PREVIOUS%%PRERELEASE_PREVIOUS%
set NUGET=.nuget\nuget.exe
set SOURCE=https://nuget.org

if exist ..\SetNugetSarifApiKey.cmd (
call ..\SetNugetSarifApiKey.cmd
call %NUGET% SetApiKey %API_KEY% -Source %SOURCE%
)
if "%ERRORLEVEL%" NEQ "0" (echo set api key of %API_KEY% to %SOURCE% FAILED && goto Exit)

@REM immediately unlist our package
set ID=Microsoft.CodeAnalysis.BinSkim
call %NUGET% delete %ID% %VERSION% -Source %SOURCE%
if "%ERRORLEVEL%" NEQ "0" (echo package delisting FAILED && goto Exit)

:Exit