::Build NuGet packages step
@ECHO off
SETLOCAL

call SetCurrentVersion.cmd

.nuget\NuGet.exe pack .\src\Nuget\BinSkim.nuspec -Symbols -Properties configuration=%Configuration%;version=%MAJOR%.%MINOR%.%PATCH%%PRERELEASE% -Verbosity Quiet -BasePath .\ -OutputDirectory .\bld\bin\Nuget || goto :ExitFailed

goto Exit

:ExitFailed
@echo.
@echo Build NuGet packages step failed.
exit /b 1

:Exit