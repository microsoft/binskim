::Build NuGet packages step
@ECHO off
SETLOCAL

REM Get version from MSBuild properties (single source of truth: Directory.Build.props)
for /f %%v in ('dotnet msbuild %~dp0src\BinSkim.Driver\BinSkim.Driver.csproj --getProperty:Version --nologo 2^>nul') do set VERSION=%%v

if "%VERSION%"=="" (
    echo ERROR: Could not determine version from MSBuild properties.
    goto :ExitFailed
)

%~dp0.nuget\NuGet.exe pack %~dp0src\Nuget\BinSkim.nuspec -Properties configuration=%Configuration%;version=%VERSION% -Verbosity Quiet -BasePath %~dp0 -OutputDirectory %~dp0bld\bin\Nuget || goto :ExitFailed
%~dp0.nuget\NuGet.exe pack %~dp0src\Nuget\BinaryParsers.nuspec -Properties configuration=%Configuration%;version=%VERSION% -Verbosity Quiet -BasePath %~dp0 -OutputDirectory %~dp0bld\bin\Nuget || goto :ExitFailed

goto Exit

:ExitFailed
@echo.
@echo Build NuGet packages step failed.
exit /b 1

:Exit