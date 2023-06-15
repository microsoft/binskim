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

## clangcl.pe.[c/cpp].codeview.*.exe

A simple hello world program, compiled with `clang 13.0.0` that generates a .exe and associated .pdb file. Script to reproduce:  
`clang-cl -o clangcl.pe.[c/cpp].codeview.*.exe -fuse-ld=lld-link hello.c -m32 -Z7`
_release: `-MT`
_debug: `-MTd`

## clangcl.14.pe.c.codeview.pdbpagesize_[size].exe

A simple hello world program, compiled with `clang 14.0.0` that generates a .exe and associated .pdb file. Script to reproduce:  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_4096.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:4096 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_4096.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_8192.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:8192 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_8192.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_16384.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:16384 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_16384.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_32768.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:32768 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_32768.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_default.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PDB:clangcl.14.pe.c.codeview.pdbpagesize_default.exe.pdb`  
`clang-cl -o clangcl.14.pe.c.codeview.pdbpagesize_8192_pdbmissing.exe -fuse-ld=lld-link helloc.c -m32 -Z7 -MTd /link /CETCOMPAT /guard:cf /PdbPageSize:8192 /PDB:clangcl.14.pe.c.codeview.pdbpagesize_8192_pdbmissing.exe.pdb (then delete the pdb)`

## gcc.example1.fnostackprotector.nodwarf

A simple hello world program, compiled with `gcc 9.4.0` that generates an executable file. Script to reproduce:  
`gcc example1.c -o gcc.example1.fnostackprotector.nodwarf -fno-stack-protector -fPIC -fstack-clash-protection`

## gcc.example1.fstackprotectorall.nodwarf

A simple hello world program, compiled with `gcc 9.4.0` that generates an executable file. Script to reproduce:  
`gcc example1.c -o gcc.example1.fstackprotectorall.nodwarf -fstack-protector-all -fPIC -fstack-clash-protection`

## gcc11.example1.fnostackprotector.dynamic

A simple hello world program, compiled with `gcc 11.1.0` that generates an executable file. Script to reproduce:  
`gcc-11 -Wall -O2 -g -gdwarf-5 -fPIE -pie -Wl,-z,now -D_FORTIFY_SOURCE=2 helloworld.c -o gcc11.example1.fnostackprotector.dynamic -fno-stack-protector`

## gcc11.example1.fnostackprotector.static

A simple hello world program, compiled with `gcc 11.1.0` that generates an executable file. Script to reproduce:  
`gcc-11 -Wall -O2 -g -gdwarf-5 -fPIE -pie -Wl,-z,now -D_FORTIFY_SOURCE=2 helloworld.c -o gcc11.example1.fnostackprotector.static -fno-stack-protector -static-pie`

## gcc11.example1.fstackprotectorall.dynamic

A simple hello world program, compiled with `gcc 11.1.0` that generates an executable file. Script to reproduce:  
`gcc-11 -Wall -O2 -g -gdwarf-5 -fPIE -pie -Wl,-z,now -D_FORTIFY_SOURCE=2 helloworld.c -o gcc11.example1.fstackprotectorall.dynamic -fstack-protector-all`

## gcc11.example1.fstackprotectorall.static

A simple hello world program, compiled with `gcc 11.1.0` that generates an executable file. Script to reproduce:  
`gcc-11 -Wall -O2 -g -gdwarf-5 -fPIE -pie -Wl,-z,now -D_FORTIFY_SOURCE=2 helloworld.c -o gcc11.example1.fstackprotectorall.static -fstack-protector-all -static-pie`

## gcc.object_file.dwarf.3.o

A simple hello world program, compiled with `gcc 9.3.0` that generates a .o object file. Script to reproduce:  
`gcc-9 -Wall -c helloc.c -O2 -g -gdwarf-3 -o gcc.object_file.dwarf.3.o`

## go1.13.8.elf.helloworld.dynamic

A simple hello world program, compiled with `go 1.13.8` that generates a dynamically linked object file. Script to reproduce:  
`go build -buildmode=pie -ldflags "-linkmode=external -extldflags '-Wl,-z,now,-z,relro,-z,defs'" -o go1.13.8.elf.helloworld.dynamic -v hellogo.go`

## go1.13.8.elf.helloworld.static

A simple hello world program, compiled with `go 1.13.8` that generates a statically linked object file. Script to reproduce:  
`go build -buildmode=pie -tags "cgo netgo osusergo static_build" -ldflags "-linkmode=external -extldflags '-static-pie -Wl,-z,now,-z,relro,-z,defs'" -o go1.13.8.elf.helloworld.static -v hellogo.go`

## Managed_x64_VS[Version]_CSharp_[NetVersion]_Default_[variant].exe

A default .NET C# console program created with VS with the specific .NET version, without changing anything and built with default settings.
_ReadyToRun: Change publish setting and enable ReadyToRun compilation.  
_SelfContained_SingleFile: Change publish setting and enable both Self Contained and Single File compilation.  
_AOT: In project setting add `<PublishAot>true</PublishAot>`  
_Native: In project setting, build tab, enable compile with .Net native toolchain.  

## Managed_x64_VS2022_NetCore6_CSharp_HighEntropyVA_[True,False].dll

A default .NET Core 6 C# program created with VS 2022, in csproj file add `<HighEntropyVA>True/False</HighEntropyVA>` and built with default AnyCPU. Code used:

```CSharp
internal class Program
{
    [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool wow64Process);

    private static void Main(string[] args)
    {
        Console.WriteLine("Environment.Is64BitProcess: " + Environment.Is64BitProcess);
        IsWow64Process(Process.GetCurrentProcess().Handle, out bool ret_val);
        Console.WriteLine("IsWow64Process: " + ret_val);
        if (IntPtr.Size == 8)
        {
            Console.WriteLine("IntPtr.Size: 64 bit machine");
        }
        else if (IntPtr.Size == 4)
        {
            Console.WriteLine("IntPtr.Size: 32 bit machine");
        }
    }
}
```

## Native_x64_RustC_Rust_debuginfo2_[version].exe

A simple hello world program, compiled with `rustc` that generates a .exe and associated .pdb file. Script to reproduce:  
Install the specific version of Rust,
`rustc -g -Clink-arg=/DEBUG:FULL src\main.rs -o Native_x64_RustC_Rust_debuginfo2_[version].exe`

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

## Native_x64_VS2022_[Debug/Release]_Qspectre_With_Lib_[Debug/Release]_Qspectre.exe

The Visual Studio 2022 "empty console application" template with no changes, compiled as Debug/Release, built with or without Qspectre.
With_Lib_[Debug/Release]_Qspectre: Include static Libary built with Debug/Release and with or without Qspectre.

## Native_x64_VS2022_ImplicitEnableOverruled.exe

The Visual Studio 2022 "empty console application" template with no changes, compiled as Release|x64.  The `/debug` option enables incremental linking implicitly.  The default `/opt:ref` switch turns it back off again.

## Native_x64_VS2022_ImplicitEnable.exe

The Visual Studio 2022 "empty console application" template, compiled as Release|x64.  The `/debug` option enables incremental linking implicitly.  `/opt:ref` and `/opt:icf` are not set.

## Native_x64_VS2022_ExplicitEnable.exe

The Visual Studio 2022 "empty console application" template, compiled as Release|x64.  The `/incremental` option enables incremental linking explicitly.  The `/ltcg` and `/gl` options are disabled explicitly.

## Native_x64_VS2022_ExplicitDisable.exe

The Visual Studio 2022 "empty console application" template, compiled as Release|x64.  The `/incremental:no` option disables incremental linking explicitly.  The `/ltcg` and `/gl` options are disabled explicitly.

## Native_x64_VS2022_[Console/KernelModeDriver/UserModeDriver]_IntegrityCheck_[variant].[exe/sys/dll]

The Visual Studio 2022 "C++ console application" template, compiled as Release|x64. In linker command line,  
_Yes: `/INTEGRITYCHECK`  
_Default: without the flag  
_Yes_Manual_FORCE_INTEGRITY: `/INTEGRITYCHECK` and then use tool to Manually set `IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY` flag to true

## Native_x64_VS2022_PDBPageSize_8192.exe

The Visual Studio 2022 "empty console application" template, compiled as Debug|x64.  The `/PDBPageSize:8192` linker option set page size to 8192.

## Sha256SignedUntrustedRoot.exe

The Visual Studio 2022 default executable template, in project property signing tab enable sign the assembly with a test certificate with sha256RSA.
