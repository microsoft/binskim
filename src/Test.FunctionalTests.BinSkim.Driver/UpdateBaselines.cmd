@echo off
powershell -ExecutionPolicy RemoteSigned -File %~dp0\UpdateBaselines.ps1 %*