@echo off
set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
set DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=0
powershell -ExecutionPolicy RemoteSigned -File %~dp0\UpdateBaselines.ps1 %*