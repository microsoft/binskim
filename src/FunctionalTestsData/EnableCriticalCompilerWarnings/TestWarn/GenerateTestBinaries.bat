@echo off
REM This generates some test binaries for the BinSkim CompilerWarnings check
REM The output filename indicates whether we expect the BinSkim compiler warning check to pass or to fail.
REM Failures are due either to an overall warning compilation level that is less than level 3 or level 4,
REM or to explicit disabling of a SDL-required warning on the command line. If disabling an SDL-required
REM warning is deemed necessary, then an exception should be sought, and "pragma warning" used in source 
REM files to limit the scope of the disabling to the necessary functions.
REM
REM Notes:
REM -Wall on its own corresponds to level 4 so is good.
REM - no W switch at all corresponds to level 0 (no warnings) so is bad.
REM In the tests below we use 4018 as an example SDL required warning.
REM 4999/703 are bogus values: they are just there to test that 
REM they are not interpreted as an SDL-required warning ID.
@echo on
pushd "%~dp0"

cl testwarn.c /nologo /Z7 /Fe:..\black\testwarn_noWswitch_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W3 /Fe:..\white\testwarn_W3_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W3 /W2 /Fe:..\black\testwarn_W3_W2_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W3 /W4 /Fe:..\white\testwarn_W3_W4_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W1 /W4 /Fe:..\white\testwarn_W1_W4_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /wd4018 /W4 /Fe:..\black\testwarn_wd4018_W4_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /wd4999 /W4 /Fe:..\white\testwarn_wd4999_W4_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W4 /wd4999 /Fe:..\white\testwarn_W4_wd4999_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W4 /wd703 /Fe:..\white\testwarn_W4_wd703_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W4 /wd4018 /Fe:..\black\testwarn_W4_wd4018_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /Wall /Fe:..\white\testwarn_Wall_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /Wall /W2 /Fe:..\black\testwarn_Wall_W2_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /Wall /wd4018 /we4018 /Fe:..\white\testwarn_Wall_wd4018_we4018_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj

REM Same again with Link Time Code Generation
cl testwarn.c /nologo /O2 /GL /Z7 /Fe..\black\testwarn_noWswitch_LTCG_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W3 /O2 /GL /Fe:..\white\testwarn_W3_LTCG_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /Z7 /W3 /W2 /O2 /GL /Fe:..\black\testwarn_W3_W2_LTCG_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /W3 /W4 /Fe:..\white\testwarn_W3_W4_LTCG_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /W1 /W4 /Fe:..\white\testwarn_W1_W4_LTCG_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /wd4018 /W4 /Fe:..\black\testwarn_wd4018_W4_LTCG_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /wd4999 /W4 /Fe:..\white\testwarn_wd4999_W4_LTCG_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /W4 /wd4999 /Fe:..\white\testwarn_W4_wd4999_LTCG_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /W4 /wd703 /Fe:..\white\testwarn_W4_wd703_LTCG_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /W4 /wd4018 /Fe:..\black\testwarn_W4_wd4018_LTCG_FAIL.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /Wall /Fe:..\white\testwarn_Wall_LTCG_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /Wall /W2 /Fe:..\black\testwarn_Wall_W2_LTCG_FAIL.exe /link /INCREMENTAL:NO 
del testwarn.obj
cl testwarn.c /nologo /O2 /GL /Z7 /Wall /wd4018 /we4018 /Fe:..\white\testwarn_Wall_wd4018_we4018_LTCG_PASS.exe /link /INCREMENTAL:NO
del testwarn.obj

popd
