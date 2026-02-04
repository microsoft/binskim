::Build NuGet packages step
@ECHO off
SETLOCAL


call SetCurrentVersion.cmd

REM Build version string, only add PRERELEASE if not empty
set VERSION=%MAJOR%.%MINOR%.%PATCH%
if not "%PRERELEASE%"=="" set VERSION=%VERSION%.%PRERELEASE%

%~dp0.nuget\NuGet.exe pack %~dp0src\Nuget\BinSkim.nuspec -Properties configuration=%Configuration%;version=%VERSION% -Verbosity Quiet -BasePath %~dp0 -OutputDirectory %~dp0bld\bin\Nuget || goto :ExitFailed
%~dp0.nuget\NuGet.exe pack %~dp0src\Nuget\BinaryParsers.nuspec -Properties configuration=%Configuration%;version=%VERSION% -Verbosity Quiet -BasePath %~dp0 -OutputDirectory %~dp0bld\bin\Nuget || goto :ExitFailed

goto Exit

:ExitFailed
@echo.
@echo Build NuGet packages step failed.
exit /b 1

:Exit