::Build NuGet packages step
@ECHO off
SETLOCAL

call SetCurrentVersion.cmd

xcopy /Y bld\bin\LayoutForSigning\BinSkim.exe       bld\bin\x86_Release\ || goto :ExitFailure
xcopy /Y bld\bin\LayoutForSigning\BinaryParsers.dll bld\bin\x86_Release\ || goto :ExitFailure
xcopy /Y bld\bin\LayoutForSigning\BinSkim.Rules.dll  bld\bin\x86_Release\ || goto :ExitFailure
xcopy /Y bld\bin\LayoutForSigning\BinSkim.Sdk.dll   bld\bin\x86_Release\ || goto :ExitFailure

.nuget\NuGet.exe pack .\src\Nuget\BinSkim.nuspec -Symbols -Properties id=Microsoft.CodeAnalysis.BinSkim;major=%MAJOR%;minor=%MINOR%;patch=%PATCH%;prerelease=%PRERELEASE% -Verbosity Quiet -BasePath .\bld\bin -OutputDirectory .\bld\bin\Nuget || goto :ExitFailed

goto Exit

:ExitFailed
@echo.
@echo Build NuGet packages step failed.
exit /b 1

:Exit