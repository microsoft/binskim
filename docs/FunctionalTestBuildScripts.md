# Build Scripts

This file records scripts used to compile the test files, in alphabetical order.
Base scenario is a simple hello world program built with different parameters for testing purpose.
Test files are located in [BaselineTestData](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Driver/BaselineTestData) and [FunctionalTestData](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestData).

## ARM_CETShadowStack_NotApplicable.exe

A simple C++ hellow world program, cross compiled using CMake with the `cl.exe` compiler and `Ninja` generator.
`CMakePresets.json` should be configured with a `configurePresets` as below:

```json
{
    "name": "arm-release",
    "displayName": "ARM Release",
    "inherits": "windows-base",
    "architecture": {
        "value": "arm",
        "strategy": "external"
    },
    "cacheVariables": {
        "CMAKE_BUILD_TYPE": "RelWithDebInfo"
    }
},
```

## ARM64_CETShadowStack_NotApplicable.exe

A simple C++ hellow world program, cross compiled using CMake using the `cl.exe` compiler and `Ninja` generator.
`CMakePresets.json` should be configured with a `configurePresets` as below:

```json
{
    "name": "arm64-release",
    "displayName": "ARM64 Release", 
    "inherits": "windows-base",
    "architecture": {
        "value": "arm64",
        "strategy": "external"
    },
    "cacheVariables": {
        "CMAKE_BUILD_TYPE": "RelWithDebInfo"
    }
},
```

## clang.[version].elf.[c,cpp].[no_]safe_stack

A simple hello world C/C++ program, compiled with different `clang [version]` that generates a executable file. Script to reproduce:  
`clang++-14 -Wall hellocpp.cpp -O2 -g -gdwarf-5 -o clang.14.elf.cpp.no_safe_stack`  
`clang-7 -Wall helloc.c -O2 -g -gdwarf-5 -o clang.7.elf.c.safe_stack -fsanitize=safe-stack`

## clang.object_file.dwarf.3.o

A simple hello world program, compiled with `clang 10.0.0` that generates a .o object file. Script to reproduce:  
`clang-10 -Wall -c helloc.c -O2 -g -gdwarf-3 -o clang.object_file.dwarf.3.o`

## clangcl.pe.c.codeview.exe

A simple hello world program, compiled with `clang 13.0.0` that generates a .exe and associated .pdb file. Script to reproduce:  
`clang-cl -o clangcl.pe.c.codeview.exe -fuse-ld=lld-link hello.c -m32 -Z7 -MTd`

## clangcl.14.pe.c.codeview.pdbpagesize_[size].exe

A simple hello world program, compiled with `clang 14.0.0` that generates a .exe and associated .pdb file. Script to reproduce:  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_4096.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:4096 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_4096.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_8192.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:8192 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_8192.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_16384.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:16384 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_16384.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_32768.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:32768 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_32768.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_default.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PDB:clangcl.14.pe.c.codeview.pdbpagesize_default.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_8192_pdbmissing.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:8192 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_8192_pdbmissing.exe.pdb (then delete the pdb)`

## gcc.object_file.dwarf.3.o

A simple hello world program, compiled with `gcc 9.3.0` that generates a .o object file. Script to reproduce:  
`gcc-9 -Wall -c helloc.c -O2 -g -gdwarf-3 -o gcc.object_file.dwarf.3.o`

## Native_x64_RustC_Rust_debuginfo2_v1.58.1.exe

A simple hello world program, compiled with `rustc 1.58.1` that generates a .exe and associated .pdb file. Script to reproduce:  
`rustc -g -Clink-arg=/DEBUG:FULL src\main.rs -o Native_x64_RustC_Rust_debuginfo2_v1.58.1.exe`

## Native_x64_VS2019_CPlusPlus_GsDriverEntry_And_UserFnUseSafeBuffers.exe

A simple `Windows Kernel Mode Driver` program, created with `Visual Studio 2019` that generates a .exe and associated .pdb file. Code to reproduce:  
Use `NTSTATUS GsDriverEntry(_In_ PDRIVER_OBJECT DriverObject, _In_ PUNICODE_STRING RegistryPath)` as entry point and decorated with `__declspec(safebuffers)`.
Also create two user functions `userfn_use_safebuffers_1()` and `userfn_use_safebuffers_2()` decorated with `__declspec(safebuffers)`.

## Native_x64_VS2019_CPlusPlus_GsDriverEntry_Only.exe

A simple `Windows Kernel Mode Driver` program, created with `Visual Studio 2019` that generates a .exe and associated .pdb file. Code to reproduce:  
Use `NTSTATUS GsDriverEntry(_In_ PDRIVER_OBJECT DriverObject, _In_ PUNICODE_STRING RegistryPath)` as entry point and decorated with `__declspec(safebuffers)`.
No user functions decorated with `__declspec(safebuffers)`.

## Native_x64_VS2019_CPlusPlus_UserFnUseSafeBuffers_Only.exe

A simple `Windows Kernel Mode Driver` program, created with `Visual Studio 2019` that generates a .exe and associated .pdb file. Code to reproduce:  
Use `NTSTATUS GsDriverEntry(_In_ PDRIVER_OBJECT DriverObject, _In_ PUNICODE_STRING RegistryPath)` as entry point and do not decorated with `__declspec(safebuffers)`.
Also create two user functions `userfn_use_safebuffers_1()` and `userfn_use_safebuffers_2()` decorated with `__declspec(safebuffers)`.

## Native_x64_VS2019_CPlusPlus_UseSafeBuffers_None.exe

A simple `Windows Kernel Mode Driver` program, created with `Visual Studio 2019` that generates a .exe and associated .pdb file. Code to reproduce:  
Use `NTSTATUS GsDriverEntry(_In_ PDRIVER_OBJECT DriverObject, _In_ PUNICODE_STRING RegistryPath)` as entry point and do not decorated with `__declspec(safebuffers)`.
No user functions decorated with `__declspec(safebuffers)`.

## Native_x64_VS2022_Debug.exe

The Visual Studio 2022 "empty console application" template with no changes, compiled as Debug|x64

## Native_x64_VS2022_ImplicitEnableOverruled.exe

The Visual Studio 2022 "empty console application" template with no changes, compiled as Release|x64.  The `/debug` option enables incremental linking implicitly.  The default `/opt:ref` switch turns it back off again.

## Native_x64_VS2022_ImplicitEnable.exe

The Visual Studio 2022 "empty console application" template, compiled as Release|x64.  The `/debug` option enables incremental linking implicitly.  `/opt:ref` and `/opt:icf` are not set.

## Native_x64_VS2022_ExplicitEnable.exe

The Visual Studio 2022 "empty console application" template, compiled as Release|x64.  The `/incremental` option enables incremental linking explicitly.  The `/ltcg` and `/gl` options are disabled explicitly.

## Native_x64_VS2022_ExplicitDisable.exe

The Visual Studio 2022 "empty console application" template, compiled as Release|x64.  The `/incremental:no` option disables incremental linking explicitly.  The `/ltcg` and `/gl` options are disabled explicitly.

## Native_x64_VS2022_PDBPageSize_8192.exe

The Visual Studio 2022 "empty console application" template, compiled as Debug|x64.  The `/PDBPageSize:8192` linker option set page size to 8192.
