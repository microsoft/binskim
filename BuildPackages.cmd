::Build NuGet packages step
@ECHO off
SETLOCAL

call SetCurrentVersion.cmd

.nuget\NuGet.exe pack .\src\Nuget\BinSkim.nuspec -Symbols -Properties id=Microsoft.CodeAnalysis.BinSkim;major=%MAJOR%;minor=%MINOR%;patch=%PATCH%;prerelease=%PRERELEASE% -Verbosity Quiet -BasePath .\bld\bin -OutputDirectory .\bld\bin\Nuget || goto :ExitFailed

goto Exit

:ExitFailed
@echo.
@echo Build NuGet packages step failed.
exit /b 1

:Exit