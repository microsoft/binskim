::This cmd script is depracated and can be deleted - before deletion make sure you update both releasing pipelines to call BuildAndTest.cmd instead of this script.
:: https://dev.azure.com/mseng/1ES/_releaseDefinition?definitionId=392&_a=definition-tasks&environmentId=741
:: https://dev.azure.com/mseng/1ES/_release?view=all&_a=releases&definitionId=150
::Build NuGet packages step
@ECHO off
SETLOCAL

set BinaryOutputDirectory=%1
set Configuration=%1
set Platform=%2

if "%BinaryOutputDirectory%" EQU "" (
set BinaryOutputDirectory=.\bld\bin\
)

if "%Configuration%" EQU "" (
set Configuration=Release
)

if "%Platform%" EQU "" (
set Platform=x64
)

call SetCurrentVersion.cmd
set Version=%MAJOR%.%MINOR%.%PATCH%%PRERELEASE%
set NuGetOutputDirectory=..\..\bld\bin\nuget\
call BuildPackages.cmd %Configuration% %Platform% %NuGetOutputDirectory% %Version% || goto :ExitFailed

goto :Exit

:ExitFailed
@echo.
@echo Build NuGet packages from layout directory step failed.
exit /b 1

:Exit