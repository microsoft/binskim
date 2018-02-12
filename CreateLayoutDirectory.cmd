::Build NuGet packages step
@ECHO off
SETLOCAL

set BinaryOutputDirectory=%1
set Configuration=%2
set Platform=%3

if "%BinaryOutputDirectory%" EQU "" (
set BinaryOutputDirectory=%~dp0bld\bin
)

if "%Configuration%" EQU "" (
set Configuration=Release
)

if "%Platform%" EQU "" (
set Platform=AnyCPU
)

set BinaryOutputDirectory=%BinaryOutputDirectory%\%Platform%_%Configuration%\Publish
set LayoutForSigningDirectory=%BinaryOutputDirectory%\..\LayoutForSigning

call :CreateDirIfNotExist %LayoutForSigningDirectory%
call :CreateDirIfNotExist %LayoutForSigningDirectory%\net461
call :CreateDirIfNotExist %LayoutForSigningDirectory%\net461\win-x86\
call :CreateDirIfNotExist %LayoutForSigningDirectory%\net461\win-x64\
call :CreateDirIfNotExist %LayoutForSigningDirectory%\netcoreapp2.0
call :CreateDirIfNotExist %LayoutForSigningDirectory%\netcoreapp2.0\win-x86\
call :CreateDirIfNotExist %LayoutForSigningDirectory%\netcoreapp2.0\win-x64\
call :CreateDirIfNotExist %LayoutForSigningDirectory%\netcoreapp2.0\linux-x64\

call :CopyFilesForMultitargeting BinSkim.exe       || goto :ExitFailed
call :CopyFilesForMultitargeting BinaryParsers.dll || goto :ExitFailed
call :CopyFilesForMultitargeting BinSkim.Rules.dll || goto :ExitFailed
call :CopyFilesForMultitargeting BinSkim.Sdk.dll   || goto :ExitFailed

goto :Exit

:CopyFilesForMultitargeting
xcopy /Y %BinaryOutputDirectory%\net461\win-x86\%1 %LayoutForSigningDirectory%\net461\win-x86\
xcopy /Y %BinaryOutputDirectory%\net461\win-x64\%1 %LayoutForSigningDirectory%\net461\win-x64\
:: For .NET core, .exes are renamed to .dlls due to packaging conventions
xcopy /Y %BinaryOutputDirectory%\netcoreapp2.0\win-x86\%~n1.dll  %LayoutForSigningDirectory%\netcoreapp2.0\win-x86\
xcopy /Y %BinaryOutputDirectory%\netcoreapp2.0\win-x64\%~n1.dll  %LayoutForSigningDirectory%\netcoreapp2.0\win-x64\
xcopy /Y %BinaryOutputDirectory%\netcoreapp2.0\linux-x64\%~n1.dll  %LayoutForSigningDirectory%\netcoreapp2.0\linux-x64\

if "%ERRORLEVEL%" NEQ "0" (echo %1 assembly copy failed.)
Exit /B %ERRORLEVEL%

:CreateDirIfNotExist
set dir=%~1
if not exist %dir% (md %dir%)
Exit /B %ERRORLEVEL%

:ExitFailed
@echo.
@echo Create layout directory step failed.
exit /b 1

:Exit