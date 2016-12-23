set LINKER=14_0
:: Test /Brepro, which prevents cmd-line information emit
 set NAME=Native_I386_WindowsGui_Link_%LINKER%_W4_Brepro
 set C_FILE=%NAME%.c 
 echo. > %C_FILE%
 cl /Brepro /W4 /LD /Z7 %C_FILE% /link /nod /incremental /noentry  
 move %NAME%.dll .\EnableCriticalCompilerWarnings\Pass
 move %NAME%.pdb .\EnableCriticalCompilerWarnings\Pass

:: Test debug:fast, which requires update DIA to retrieve relevant data
 set NAME=Native_I386_WindowsGui_Link_%LINKER%_W4_DebugFast
 set C_FILE=%NAME%.c 
 echo. > %C_FILE%
 cl /debug:fast /W4 /LD /Z7 %C_FILE% /link /nod /incremental /noentry  
 move %NAME%.dll .\EnableCriticalCompilerWarnings\Pass
 move %NAME%.pdb .\EnableCriticalCompilerWarnings\Pass

 set NAME=Native_I386_WindowsGui_Link_%LINKER%_W2_DebugFast
 set C_FILE=%NAME%.c 
 echo. > %C_FILE%
 cl /debug:fast /W2 /LD /Z7 %C_FILE% /link /nod /incremental /noentry  
 move %NAME%.dll .\EnableCriticalCompilerWarnings\Fail
 move %NAME%.pdb .\EnableCriticalCompilerWarnings\Fail

del /q *.c
del /q *.ilk
del /q *.obj