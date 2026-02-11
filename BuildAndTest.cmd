@echo off
SETLOCAL
@REM Uncomment this line to update nuget.exe
@REM Doing so can break SLN build (which uses nuget.exe to
@REM create a nuget package for binskim) so must opt-in
@REM %~dp0.nuget\NuGet.exe update -self

set Configuration=%1

if "%Configuration%" EQU "" (
set Configuration=Release
)

@REM Remove existing build data
if exist bld (rd /s /q bld)

set NuGetOutputDirectory=%~dp0bld\bin\nuget\

call SetCurrentVersion.cmd

set VERSION_CONSTANTS=%~dp0src\BinaryParsers\VersionConstants.cs

REM Conditionally set DOT if PRERELEASE is not empty
set DOT=
if not "%PRERELEASE%"=="" set PRERELEASE=.%PRERELEASE%

@REM Rewrite VersionConstants.cs

echo // Copyright (c) Microsoft. All rights reserved. Licensed under the MIT>  %VERSION_CONSTANTS%
echo // license. See LICENSE file in the project root for full license information.>> %VERSION_CONSTANTS%
echo namespace Microsoft.CodeAnalysis.IL>> %VERSION_CONSTANTS%
echo {>> %VERSION_CONSTANTS%
echo     public static class VersionConstants>> %VERSION_CONSTANTS%
echo     {>> %VERSION_CONSTANTS%
echo         public const string Prerelease = "%DOT%%PRERELEASE%";>> %VERSION_CONSTANTS%
echo         public const string AssemblyVersion = "%MAJOR%.%MINOR%.%PATCH%";>> %VERSION_CONSTANTS%
echo         public const string FileVersion = "%MAJOR%.%MINOR%.%PATCH%";>> %VERSION_CONSTANTS%
echo         public const string Version = AssemblyVersion + Prerelease;>> %VERSION_CONSTANTS%
echo     }>> %VERSION_CONSTANTS%
echo }>> %VERSION_CONSTANTS%
echo Current Version: %MAJOR%.%MINOR%.%PATCH%%DOT%%PRERELEASE%


::Restore packages
echo Restoring packages...
dotnet restore %~dp0src\BinSkim.sln

:: Build the solution 
echo Building solution...
dotnet build --no-restore /verbosity:minimal %~dp0src\BinSkim.sln /p:Configuration=%Configuration% /filelogger /fileloggerparameters:Verbosity=detailed || goto :ExitFailed

::Run unit tests 
echo Run unit tests
call :RunTests || goto :ExitFailed

::Create the BinSkim platform specific publish packages
echo Creating Platform Specific BinSkim 'Publish' Packages
call :CreatePublishPackage net9.0 win-x64 || goto :ExitFailed
call :CreatePublishPackage net9.0 linux-x64 || goto :ExitFailed
call :CreatePublishPackage net9.0 linux-arm64 || goto :ExitFailed
call :CreatePublishPackage net9.0 osx-x64 || goto :ExitFailed

::Build NuGet package
echo BuildPackages.cmd
call BuildPackages.cmd || goto :ExitFailed

::Update BinSkimRules.md to cover any xml changes
echo Exporting any BinSkim rules
.\bld\bin\BinSkim.Driver\release\BinSkim.exe export-rules .\docs\BinSkimRules.md

echo Fixing markdown angle brackets
powershell -Command "$content = [System.IO.File]::ReadAllText('.\docs\BinSkimRules.md'); $content = $content -replace '<', '&lt;' -replace '>', '&gt;'; [System.IO.File]::WriteAllText('.\docs\BinSkimRules.md', $content)"

goto :Exit

:RunTests
dotnet test src\BinSkim.sln --no-build -c %Configuration%
if "%ERRORLEVEL%" NEQ "0" (echo Tests execution FAILED.)
Exit /B %ERRORLEVEL%

:CreatePublishPackage
set Framework=%~1
set RuntimeArg=%~2
dotnet publish %~dp0src\BinSkim.Driver\BinSkim.Driver.csproj --no-restore -c %Configuration% -f %Framework% --runtime %RuntimeArg% --self-contained true
Exit /B %ERRORLEVEL%

:ExitFailed
@echo Build and test did not complete successfully.
Exit /B 1

:Exit