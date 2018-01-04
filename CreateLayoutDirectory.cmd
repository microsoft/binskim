::Build NuGet packages step
@ECHO off
SETLOCAL

set BinaryOutputDirectory=%1
set Configuration=%2
set Platform=%3

if "%BinaryOutputDirectory%" EQU "" (
set BinaryOutputDirectory=.\bld\bin\
)

if "%Configuration%" EQU "" (
set Configuration=Release
)

if "%Platform%" EQU "" (
set Platform=AnyCPU
)

set BinaryOutputDirectory=%BinaryOutputDirectory%\%Platform%_%Configuration%
set LayoutForSigningDirectory=%BinaryOutputDirectory%\..\LayoutForSigning

if not exist %LayoutForSigningDirectory% (md %LayoutForSigningDirectory%)
if not exist %LayoutForSigningDirectory%\net46 (md %LayoutForSigningDirectory%\net46)
if not exist %LayoutForSigningDirectory%\netcoreapp2.0 (md %LayoutForSigningDirectory%\netcoreapp2.0)
if not exist %LayoutForSigningDirectory%\netstandard2.0 (md %LayoutForSigningDirectory%\netstandard2.0)

call :CopyFilesForMultitargeting BinSkim.exe       || goto :ExitFailed
call :CopyFilesForMultitargeting BinaryParsers.dll || goto :ExitFailed
call :CopyFilesForMultitargeting BinSkim.Rules.dll || goto :ExitFailed
call :CopyFilesForMultitargeting BinSkim.Sdk.dll   || goto :ExitFailed

goto :Exit

:CopyFilesForMultitargeting
xcopy /Y %BinaryOutputDirectory%\net46\%1 %LayoutForSigningDirectory%\net46

:: For .NET core, .exes are renamed to .dlls due to packaging conventions
xcopy /Y %BinaryOutputDirectory%\netcoreapp2.0\%~n1.dll  %LayoutForSigningDirectory%\netcoreapp2.0 
xcopy /Y %BinaryOutputDirectory%\netstandard2.0\%~n1.dll %LayoutForSigningDirectory%\netstandard2.0

if "%ERRORLEVEL%" NEQ "0" (echo %1 assembly copy failed.)
Exit /B %ERRORLEVEL%

:ExitFailed
@echo.
@echo Create layout directory step failed.
exit /b 1

:Exit