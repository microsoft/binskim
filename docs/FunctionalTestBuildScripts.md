# Build Scripts

This file records scripts used to compile the test files, in alphabetical order.
Base scenario is a simple hello world program built with different parameters for testing purpose.
Test files are located in [BaselineTestsData](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Driver/BaselineTestsData) and [FunctionalTestsData](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestsData).

## clangcl.pe.c.codeview.exe

A simple hello world program, compiled with `clang 13.0.0` that generates a .exe and associated .pdb file. Script to reproduce:  
`clang-cl -o clangcl.pe.c.codeview.exe -fuse-ld=lld-link hello.c -m32 -Z7 -MTd`

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
