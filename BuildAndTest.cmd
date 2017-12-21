@echo off
SETLOCAL
@REM Uncomment this line to update nuget.exe
@REM Doing so can break SLN build (which uses nuget.exe to
@REM create a nuget package for binskim) so must opt-in
@REM %~dp0.nuget\NuGet.exe update -self

call SetCurrentVersion.cmd

set VERSION_CONSTANTS=src\BinaryParsers\VersionConstants.cs

rd /s /q bld

@REM Rewrite VersionConstants.cs
echo // Copyright (c) Microsoft. All rights reserved. Licensed under the MIT         > %VERSION_CONSTANTS%
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

%~dp0.nuget\NuGet.exe restore src\BinSkim.sln 
msbuild /verbosity:minimal /target:rebuild src\BinSkim.sln /p:Configuration=Release /p:"Platform=x64" /filelogger /fileloggerparameters:Verbosity=detailed || goto :ExitFailed
msbuild /verbosity:minimal /target:rebuild src\BinSkim.sln /p:Configuration=Release /p:"Platform=x86" /filelogger /fileloggerparameters:Verbosity=detailed || goto :ExitFailed

md bld\bin\nuget
md bld\bin\LayoutForSigning
md bld\bin\LayoutForSigning\x86
md bld\bin\LayoutForSigning\x64

xcopy /Y bld\bin\x86_Release\BinSkim.exe       bld\bin\LayoutForSigning\x86
xcopy /Y bld\bin\x86_Release\BinaryParsers.dll bld\bin\LayoutForSigning\x86
xcopy /Y bld\bin\x86_Release\BinSkim.Rules.dll bld\bin\LayoutForSigning\x86
xcopy /Y bld\bin\x86_Release\BinSkim.Sdk.dll   bld\bin\LayoutForSigning\x86

xcopy /Y bld\bin\x64_Release\BinSkim.exe       bld\bin\LayoutForSigning\x64
xcopy /Y bld\bin\x64_Release\BinaryParsers.dll bld\bin\LayoutForSigning\x64
xcopy /Y bld\bin\x64_Release\BinSkim.Rules.dll bld\bin\LayoutForSigning\x64
xcopy /Y bld\bin\x64_Release\BinSkim.Sdk.dll   bld\bin\LayoutForSigning\x64

call BuildPackages.cmd || goto :ExitFailure

src\packages\xunit.runner.console.2.1.0\tools\xunit.console.x86.exe bld\bin\x86_Release\BinSkim.Rules.FunctionalTests.dll  || goto :ExitFailed
src\packages\xunit.runner.console.2.1.0\tools\xunit.console.x86.exe bld\bin\x86_Release\BinSkim.Driver.FunctionalTests.dll || goto :ExitFailed

goto :Exit

:ExitFailed
@echo Build and test did not complete successfully.
Exit /B 1

:Exit