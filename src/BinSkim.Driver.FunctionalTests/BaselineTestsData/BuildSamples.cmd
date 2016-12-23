
:: Test cvstres-compiled binaries
 echo. > English.rc
 RC /r /c1252 English.rc
 call :BuildLibrary x86
 call :BuildLibrary x64
 call :BuildLibrary ARM
 del /q *.obj
 del /q *.rc
 del /q *.res

goto :eof

:BuildLibrary
 CVTRES /OUT:Cvtres_%1.obj /MACHINE:%1 English.RES 
 LINK Cvtres_%1.obj /MACHINE:%1 /out:Native_%1_VS2015_CvtresResourceOnly.dll /NOENTRY /DLL
