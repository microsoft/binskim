@echo off
setlocal
if "%VSCMD_ARG_TGT_ARCH%"=="x64" (
    set IncludeSymbol=_load_config_used
) else if "%VSCMD_ARG_TGT_ARCH%"=="x86" (
    set IncludeSymbol=__load_config_used
) else (
    echo Run from an x86 or x64 Visual Studio 2022 Native Tools command prompt.
    exit /b 1
)
pushd "%~dp0"
if not exist UnusedCookie mkdir UnusedCookie
cl /nologo /O1 /GS- UnusedSecurityCookie.c /FoUnusedCookie\UnusedSecurityCookie.obj /FeUnusedCookie\Native_%VSCMD_ARG_TGT_ARCH%_VS2022_UnusedSecurityCookie.exe /link /nodefaultlib /entry:main /subsystem:console /include:%IncludeSymbol% /Brepro
if errorlevel 1 goto :failed
del UnusedCookie\UnusedSecurityCookie.obj
popd
exit /b 0

:failed
popd
exit /b 1
