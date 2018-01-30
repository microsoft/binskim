#!/bin/bash

# Note--test binary build currently targets linux and expects GCC to be installed
# and on the test path

OUTPUT_DIR="./bin"
INTERMEDIATE_DIR="./obj"
C_COMPILERS=( "gcc" "clang" )

# Make output directory if it doesn't exist
if [ ! -d "$OUTPUT_DIR" ]; then
    echo "$OUTPUT_DIR does not exist, creating it."
    mkdir "$OUTPUT_DIR"
fi

# Make intermediate dir if it doesn't exist
if [ ! -d "$INTERMEDIATE_DIR" ]; then
    echo "$INTERMEDIATE_DIR does not exist, creating it."
    mkdir "$INTERMEDIATE_DIR"
fi
# Delete old artifacts
echo "Cleaning old files from $OUTPUT_DIR"
rm -f $OUTPUT_DIR/*
echo "Cleaning old files from $INTERMEDIATE_DIR"
rm -f $INTERMEDIATE_DIR/*

for C_COMPILER in "${C_COMPILERS[@]}"
do
    echo "Building samples for $C_COMPILER"

    if ! [ -x "$(command -v $C_COMPILER)" ]; then
        echo "$C_COMPILER is not installed, please install it."
        exit 1
    fi

    # Default compilation
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.default_compilation

    #Object File
    $C_COMPILER -c empty.c -o $OUTPUT_DIR/$C_COMPILER.object_file.o

    # PIE/No PIE:
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.non_pie_executable
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.pie_executable -fPIE -pie

    #Shared Library
    $C_COMPILER -c -fpic library.c -o $INTERMEDIATE_DIR/$C_COMPILER.library_object.o 
    $C_COMPILER -shared -o $OUTPUT_DIR/$C_COMPILER.shared_library.so $INTERMEDIATE_DIR/$C_COMPILER.library_object.o

    # Stack Protector
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.no_stack_protector -fno-stack-protector
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.stack_protector -fstack-protector-strong

    $C_COMPILER -c -fpic library.c -o $INTERMEDIATE_DIR/$C_COMPILER.library_object.o  -fstack-protector-strong
    $C_COMPILER -shared -o $OUTPUT_DIR/$C_COMPILER.stack_protector.so $INTERMEDIATE_DIR/$C_COMPILER.library_object.o
    ar rcs $OUTPUT_DIR/$C_COMPILER.stack_protector.a $INTERMEDIATE_DIR/$C_COMPILER.library_object.o

    # Relro
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.relocationsrw -Wl,-z,norelro
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.relocationsro -Wl,-z,relro

    # No Exec Stack
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.noexecstack -z noexecstack
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.execstack -z execstack

    $C_COMPILER -c -fpic library.c -o $INTERMEDIATE_DIR/$C_COMPILER.library_object.o
    $C_COMPILER -shared -o $OUTPUT_DIR/$C_COMPILER.noexecstack.so $INTERMEDIATE_DIR/$C_COMPILER.library_object.o -z noexecstack

    $C_COMPILER -c -fpic library.c -o $INTERMEDIATE_DIR/$C_COMPILER.est_library_object.o 
    $C_COMPILER -shared -o $OUTPUT_DIR/$C_COMPILER.execstack.so $INTERMEDIATE_DIR/$C_COMPILER.est_library_object.o -z execstack

    # Immediate binding
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.immediate_binding -Wl,-z,relro,-z,now
    $C_COMPILER empty.c -o $OUTPUT_DIR/$C_COMPILER.no_immediate_binding
done

echo "Building GCC Only flags"
# gcc specific.
# Fortify Functions
# Get Fortifiable Functions (note--platform dependent):
objdump -T /lib/x86_64-linux-gnu/libc.so.6 | grep -o "__[a-z]*_chk" | sort > $OUTPUT_DIR/glibc_chk_functions.txt
# Build FORTIFY_SOURCE checks:
gcc empty.c -o $OUTPUT_DIR/gcc.unfortified
gcc empty.c -o $OUTPUT_DIR/gcc.fortified -DFORTIFY_SOURCE=2 -O2
gcc no_fortify_func.c -o $OUTPUT_DIR/gcc.no_fortification_required -DFORTIFY_SOURCE=2 -O2

echo "Building invalid ELF file"
# Invalid ELF file from haskell compiler
# This bug is fixed in the main version of the compiler, but hasn't made it into the Ubuntu downstream yet.
# See:  https://ghc.haskell.org/trac/ghc/ticket/11022
# GHC version 7.10.3 definitely still produces invalid ELF binaries.
if ! [ -x "$(command -v ghc)" ]; then
    echo "ghc (haskell compiler) is not installed, please install it."
    exit 1
fi

ghc basic_haskell.hs -o $OUTPUT_DIR/ghc.invalid_elf -outputdir $INTERMEDIATE_DIR