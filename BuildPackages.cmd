::Build NuGet packages step
@ECHO off
SETLOCAL

call SetCurrentVersion.cmd
echo Build NuGet Package using NuGet pack. OutputDirectory is bld\bin\Nuget
%~dp0.nuget\NuGet.exe pack %~dp0src\Nuget\BinSkim.nuspec -Properties configuration=%Configuration%;version=%MAJOR%.%MINOR%.%PATCH%%PRERELEASE% -Verbosity Quiet -BasePath %~dp0 -OutputDirectory %~dp0bld\bin\Nuget || goto :ExitFailed
%~dp0.nuget\NuGet.exe pack %~dp0src\Nuget\BinaryParsers.nuspec -Properties configuration=%Configuration%;version=%MAJOR%.%MINOR%.%PATCH%%PRERELEASE% -Verbosity Quiet -BasePath %~dp0 -OutputDirectory %~dp0bld\bin\Nuget || goto :ExitFailed

echo Build DotNetTool NuGet Package using dotnet pack. OutputDirectory is bld\bin\DotNetToolNuget
dotnet pack %~dp0src\BinSkim.Driver\BinSkim.Driver.csproj --no-build --output %~dp0bld\bin\DotNetToolNuget || goto :ExitFailed


::dotnet pack %~dp0src\BinSkim.Driver\BinSkim.Driver.csproj --no-build --output %~dp0bld\bin\DotNetToolNuget --runtime win-x64 || goto :ExitFailed
::dotnet pack %~dp0src\BinSkim.Driver\BinSkim.Driver.csproj --no-build -p:NuspecFile=%~dp0src\Nuget\BinSkim.nuspec -p:NuspecBasePath=%~dp0 --output %~dp0bld\bin\DotNetToolNuget --runtime win-x64 || goto :ExitFailed
::dotnet pack %~dp0src\BinSkim.Driver\BinSkim.Driver.csproj --no-build -p:NuspecFile=%~dp0src\Nuget\BinSkimDotNetTool.nuspec -p:NuspecBasePath=%~dp0 --output %~dp0bld\bin\DotNetToolNuget  || goto :ExitFailed


goto Exit

:ExitFailed
@echo.
@echo Build NuGet packages step failed.
exit /b 1

:Exit