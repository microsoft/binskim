::Build NuGet packages step
@ECHO off
SETLOCAL

if "%Configuration%"=="" (
    set Configuration=Release
)

REM Get version from MSBuild properties (single source of truth: Directory.Build.props)
for /f %%v in ('dotnet msbuild %~dp0src\BinSkim.Driver\BinSkim.Driver.csproj --getProperty:Version --nologo 2^>nul') do set VERSION=%%v

if "%VERSION%"=="" (
    echo ERROR: Could not determine version from MSBuild properties.
    goto :ExitFailed
)

dotnet pack "%~dp0src\Nuget\BinSkim.Package.csproj" --configuration %Configuration% --output "%~dp0bld\bin\Nuget" --nologo --verbosity quiet || goto :ExitFailed
dotnet pack "%~dp0src\Nuget\BinaryParsers.Package.csproj" --configuration %Configuration% --output "%~dp0bld\bin\Nuget" --nologo --verbosity quiet || goto :ExitFailed

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0src\Nuget\ValidatePackages.ps1" -PackageDirectory "%~dp0bld\bin\Nuget" -Version "%VERSION%" || goto :ExitFailed

goto Exit

:ExitFailed
@echo.
@echo Build NuGet packages step failed.
exit /b 1

:Exit