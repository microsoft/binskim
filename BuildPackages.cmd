::Build NuGet packages step
@ECHO off
SETLOCAL

call SetCurrentVersion.cmd

%~dp0.nuget\NuGet.exe pack %~dp0src\Nuget\BinSkim.nuspec -Symbols -Properties configuration=%Configuration%;version=%MAJOR%.%MINOR%.%PATCH%%PRERELEASE% -Verbosity Quiet -BasePath %~dp0 -OutputDirectory %~dp0bld\bin\Nuget || goto :ExitFailed

goto Exit

:ExitFailed
@echo.
@echo Build NuGet packages step failed.
exit /b 1

:Exit