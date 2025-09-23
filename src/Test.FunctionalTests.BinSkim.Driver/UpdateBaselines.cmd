@echo off
set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
powershell -ExecutionPolicy RemoteSigned -File %~dp0\UpdateBaselines.ps1 %*