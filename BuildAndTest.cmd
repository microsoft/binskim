@echo off
SETLOCAL
@REM Uncomment this line to update nuget.exe
@REM Doing so can break SLN build (which uses nuget.exe to
@REM create a nuget package for binskim) so must opt-in
@REM %~dp0.nuget\NuGet.exe update -self

call SetCurrentVersion.cmd

set VERSION_CONSTANTS=src\BinaryParsers\VersionConstants.cs

if exist bld ( rd /s /q bld || exit /b 1 )

@REM Rewrite VersionConstants.cs
echo Rewriting VersionConstants.cs...
echo // Copyright (c) Microsoft. All rights reserved. Licensed under the MIT        > %VERSION_CONSTANTS%
echo // license. See LICENSE file in the project root for full license information. >> %VERSION_CONSTANTS%
echo namespace Microsoft.CodeAnalysis.IL                                            >> %VERSION_CONSTANTS%
echo {                                                                              >> %VERSION_CONSTANTS%
echo     public static class VersionConstants                                       >> %VERSION_CONSTANTS%
echo     {                                                                          >> %VERSION_CONSTANTS%
echo         public const string Prerelease = "%PRERELEASE%";                       >> %VERSION_CONSTANTS%
echo         public const string AssemblyVersion = "%MAJOR%.%MINOR%.%PATCH%" + ".0"; >> %VERSION_CONSTANTS%
echo         public const string FileVersion = "%MAJOR%.%MINOR%.%PATCH%" + ".0";    >> %VERSION_CONSTANTS%
echo         public const string Version = AssemblyVersion + Prerelease;            >> %VERSION_CONSTANTS%
echo     }                                                                          >> %VERSION_CONSTANTS%
echo  }                                                                            >> %VERSION_CONSTANTS%

call Restore.cmd || exit /b 1

echo Building x64...
msbuild /verbosity:minimal /target:rebuild src\BinSkim.sln /p:Configuration=Release /p:"Platform=x64" /filelogger /fileloggerparameters:Verbosity=detailed || exit /b 1

echo Building x86...
msbuild /verbosity:minimal /target:rebuild src\BinSkim.sln /p:Configuration=Release /p:"Platform=x86" /filelogger /fileloggerparameters:Verbosity=detailed || exit /b 1

echo Making nuget package...
md bld\bin\nuget
.nuget\NuGet.exe pack .\src\Nuget\BinSkim.nuspec -Symbols -Properties id=Microsoft.CodeAnalysis.BinSkim;major=%MAJOR%;minor=%MINOR%;patch=%PATCH%;prerelease=%PRERELEASE% -Verbosity Quiet -BasePath .\bld\bin\BinSkim.Driver -OutputDirectory .\bld\bin\Nuget || exit /b 1

echo Running unit tests...
src\packages\xunit.runner.console.2.1.0\tools\xunit.console.x86.exe bld\bin\BinSkim.Rules.FunctionalTests\x86_Release\BinSkim.Rules.FunctionalTests.dll
src\packages\xunit.runner.console.2.1.0\tools\xunit.console.x86.exe bld\bin\BinSkim.Driver.FunctionalTests\x86_Release\BinSkim.Driver.FunctionalTests.dll
