# Build Scripts

This file records scripts used to compile the test files, in alphabetical order.
Base scenario is a simple hello world program built with different parameters for testing purpose.
Test files are located in [BaselineTestData](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Driver/BaselineTestData) and [FunctionalTestData](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestData).

## clang.object_file.dwarf.3.o

A simple hello world program, compiled with `clang 10.0.0` that generates a .o object file. Script to reproduce:  
`clang-10 -Wall -c helloc.c -O2 -g -gdwarf-3 -o clang.object_file.dwarf.3.o`

## clangcl.pe.c.codeview.exe

A simple hello world program, compiled with `clang 13.0.0` that generates a .exe and associated .pdb file. Script to reproduce:  
`clang-cl -o clangcl.pe.c.codeview.exe -fuse-ld=lld-link hello.c -m32 -Z7 -MTd`

## gcc.object_file.dwarf.3.o

A simple hello world program, compiled with `gcc 9.3.0` that generates a .o object file. Script to reproduce:  
`gcc-9 -Wall -c helloc.c -O2 -g -gdwarf-3 -o gcc.object_file.dwarf.3.o`

## Native_x64_RustC_Rust_debuginfo2_v1.58.1.exe

A simple hello world program, compiled with `rustc 1.58.1` that generates a .exe and associated .pdb file. Script to reproduce:  
`rustc -g -Clink-arg=/DEBUG:FULL src\main.rs -o Native_x64_RustC_Rust_debuginfo2_v1.58.1.exe`
