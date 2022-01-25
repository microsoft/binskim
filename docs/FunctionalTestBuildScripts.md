# Build Scripts

This file records scripts used to compile the test files, in alphabetical order.
Test files are located in [BaselineTestsData](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Driver/BaselineTestsData) and [FunctionalTestsData](https://github.com/microsoft/binskim/tree/main/src/Test.FunctionalTests.BinSkim.Rules/FunctionalTestsData)

## clangcl.pe.c.codeview.exe

with clang 13.0.0
`clang-cl -o clangcl.pe.c.codeview.exe -fuse-ld=lld-link hello.c -m32 -Z7 -MTd`

## Native_x64_RustC_Rust_debuginfo2_v1.57.exe

with rustc 1.57.0
`rustc -g -Clink-arg=/DEBUG:FULL src\main.rs -o Native_x64_RustC_Rust_debuginfo2_v1.57.exe`
