@echo off
SETLOCAL
@REM Uncomment this line to update nuget.exe
@REM Doing so can break SLN build (which uses nuget.exe to
@REM create a nuget package for binskim) so must opt-in
@REM %~dp0.nuget\NuGet.exe update -self

set Configuration=%1
set Platform=Any CPU

if "%Configuration%" EQU "" (
set Configuration=Release
)

@REM Remove existing build data
if exist bld (rd /s /q bld)

SET NuGetConfigFile=%~dp0src\NuGet.config
set NuGetPackageDir=.\src\packages
set NuGetOutputDirectory=..\..\bld\bin\nuget\

call SetCurrentVersion.cmd

set VERSION_CONSTANTS=src\BinaryParsers\VersionConstants.cs

@REM Rewrite VersionConstants.cs
echo // Copyright (c) Microsoft. All rights reserved. Licensed under the MIT         >  %VERSION_CONSTANTS%
echo // license. See LICENSE file in the project root for full license information.  >> %VERSION_CONSTANTS%
echo namespace Microsoft.CodeAnalysis.IL                                             >> %VERSION_CONSTANTS%
echo {                                                                               >> %VERSION_CONSTANTS%
echo     public static class VersionConstants                                        >> %VERSION_CONSTANTS%
echo     {                                                                           >> %VERSION_CONSTANTS%
echo         public const string Prerelease = "%PRERELEASE%";                        >> %VERSION_CONSTANTS%
echo         public const string AssemblyVersion = "%MAJOR%.%MINOR%.%PATCH%" + ".0"; >> %VERSION_CONSTANTS%
echo         public const string FileVersion = "%MAJOR%.%MINOR%.%PATCH%" + ".0";     >> %VERSION_CONSTANTS%
echo         public const string Version = AssemblyVersion + Prerelease;             >> %VERSION_CONSTANTS%
echo     }                                                                           >> %VERSION_CONSTANTS%
echo  }                                                                              >> %VERSION_CONSTANTS%

%~dp0.nuget\NuGet.exe restore src\BinSkim.sln -ConfigFile "%NuGetConfigFile%" -OutputDirectory "%NuGetPackageDir%"
 
msbuild /verbosity:minimal /target:rebuild src\BinSkim.sln /p:Configuration=%Configuration% /filelogger /fileloggerparameters:Verbosity=detailed || goto :ExitFailed

set Platform=AnyCPU

::Build NuGet package
echo BuildPackages.cmd
call BuildPackages.cmd || goto :ExitFailed

::Create layout directory of assemblies that need to be signed
echo CreateLayoutDirectory.cmd .\bld\bin %Configuration% %Platform%
call CreateLayoutDirectory.cmd .\bld\bin %Configuration% %Platform%

::Run unit tests
echo Run all multitargeting xunit tests
call :RunMultitargetingTests Driver Functional || goto :ExitFailed
call :RunMultitargetingTests Rules Functional  || goto :ExitFailed

goto :Exit

:RunMultitargetingTests
set TestProject=%1
set TestType=%2
pushd .\src\BinSkim.%TestProject%.%TestType%Tests && dotnet xunit -nobuild -configuration %Configuration% -fxversion 2.0.3 && popd
if "%ERRORLEVEL%" NEQ "0" (echo %TestProject% %TestType% tests execution FAILED.)
Exit /B %ERRORLEVEL%

:ExitFailed
@echo Build and test did not complete successfully.
Exit /B 1

:Exit