# Rules

## Rule `BA3003.EnableStackProtector`

### Description

The stack protector ensures that all functions that use buffers over a certain size will use a stack cookie (and check it) to prevent stack based buffer overflows, exiting if stack smashing is detected. Use '--fstack-protector-strong' (all buffers of 4 bytes or more) or '--fstack-protector-all' (all functions) to enable this.

### Messages

#### `Pass`: Pass

Stack protector was found on '{0}'.  However, if you are not compiling with '--stack-protector-strong', it may provide additional protections.

#### `Error`: Error

The stack protector was not found in '{0}'. This may be because '--stack-protector-strong' was not used, or because it was explicitly disabled by '-fno-stack-protectors'.
Modules did not meet the criteria: {1}

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA3005.EnableStackClashProtection`

### Description

This check ensures that stack clash protection is enabled. Each program running on a computer uses a special memory region called the stack. This memory region is special because it grows automatically when the program needs more stack memory. But if it grows too much and gets too close to another memory region, the program may confuse the stack with the other memory region. An attacker can exploit this confusion to overwrite the stack with the other memory region, or the other way around. Use the compiler flags '-fstack-clash-protection' to enable this.

### Messages

#### `Pass`: Pass

The Stack Clash Protection was present, so '{0}' is protected.

#### `Error`: Error

The Stack Clash Protection is missing from this binary, so the stack from '{0}' can clash/colide with another memory region. Ensure you are compiling with the compiler flags '-fstack-clash-protection' to address this.
Modules did not meet the criteria: {1}

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA4002.ReportElfOrMachoCompilerData`

### Description

This rule emits CSV data to the console for every compiler/language/version combination that's observed.

### Messages

---

## Rule `BA3001.EnablePositionIndependentExecutable`

### Description

A Position Independent Executable (PIE) relocates all of its sections at load time, including the code section, if ASLR is enabled in the Linux kernel (instead of just the stack/heap). This makes ROP-style attacks more difficult. This can be enabled by passing '-f pie' to clang/gcc.

### Messages

#### `Executable`: Pass

PIE enabled on executable '{0}'.

#### `Library`: Pass

'{0}' is a shared object library rather than an executable, and is automatically position independent.

#### `Error`: Error

PIE disabled on executable '{0}'.  This means the code section will always be loaded to the same address, even if ASLR is enabled in the Linux kernel.  To address this, ensure you are compiling with '-fpie' when using clang/gcc.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA3002.DoNotMarkStackAsExecutable`

### Description

This checks if a binary has an executable stack; an executable stack allows attackers to redirect code flow into stack memory, which is an easy place for an attacker to store shellcode. Ensure you are compiling with '-z noexecstack' to mark the stack as non-executable.

### Messages

#### `Pass`: Pass

GNU_STACK segment marked as non-executable on '{0}'.

#### `StackExec`: Error

Stack on '{0}' is executable, which means that an attacker could use it as a place to store attack shellcode.  Ensure you are compiling with '-z noexecstack' to mark the stack as non-executable.

#### `NoStackSeg`: Error

GNU_STACK segment on '{0}' is missing, which means the stack will likely be loaded as executable.  Ensure you are using an up to date compiler and passing '-z noexecstack' to the compiler.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA3004.GenerateRequiredSymbolFormat`

### Description

This check ensures that debugging dwarf version used is 5. The dwarf version 5 contains more information and should be used. Use the compiler flags '-gdwarf-5' to enable this.

### Messages

#### `Pass`: Pass

The version of the debugging dwarf format is '{0}' for the file '{1}'

#### `Error`: Error

'{0}' is using debugging dwarf version '{1}'. The dwarf version 5 contains more information and should be used. To enable the debugging version 5 use '-gdwarf-5'.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA3006.EnableNonExecutableStack`

### Description

This check ensures that non-executable stack is enabled. A common type of exploit is the stack buffer overflow. An application receives, from an attacker, more data than it is prepared for and stores this information on its stack, writing beyond the space reserved for it. This can be designed to cause execution of the data written on the stack. One mechanism to mitigate this vulnerability is for the system to not allow the execution of instructions in sections of memory identified as part of the stack. Use the compiler flags '-z noexecstack' to enable this.

### Messages

#### `Pass`: Pass

The non-executable stack flag was present, so '{0}' is protected.

#### `Error`: Error

The non-executable stack is not enabled for this binary, so '{0}' can have a vulnerability of execution of the data written on the stack. Ensure you are compiling with the flag '-z noexecstack' to address this.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA3010.EnableReadOnlyRelocations`

### Description

This check ensures that some relocation data is marked as read only after the executable is loaded, and moved below the '.data' section in memory. This prevents them from being overwritten, which can redirect control flow. Use the compiler flags '-Wl,-z,relro' to enable this.

### Messages

#### `Pass`: Pass

The GNU_RELRO segment was present, so '{0}' is protected.

#### `Error`: Error

The GNU_RELRO segment is missing from this binary, so relocation sections in '{0}' will not be marked as read only after the binary is loaded.  An attacker can overwrite these to redirect control flow.  Ensure you are compiling with the compiler flags '-Wl,-z,relro' to address this.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA3011.EnableBindNow`

### Description

This check ensures that some relocation data is marked as read only after the executable is loaded, and moved below the '.data' section in memory. This prevents them from being overwritten, which can redirect control flow. Use the compiler flags '-Wl,-z,now' to enable this.

### Messages

#### `Pass`: Pass

The BIND_NOW flag was present, so '{0}' is protected.

#### `Error`: Error

The BIND_NOW flag is missing from this binary, so relocation sections in '{0}' will not be marked as read only after the binary is loaded.  An attacker can overwrite these to redirect control flow.  Ensure you are compiling with the compiler flags '-Wl,-z,now' to address this.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA3030.UseGccCheckedFunctions`

### Description

GCC can automatically replace unsafe functions with checked variants when it can statically determine the length of a buffer or string. In the case of an overflow, the checked version will safely exit the program (rather than potentially allowing an exploit). This feature can be enabled by passing '-D_FORTIFY_SOURCE=2' when optimization level 2 is enabled ('-O2').

### Messages

#### `AllFunctionsChecked`: Pass

All functions that can be checked in '{0}' are using the checked versions, so this binary is protected from overflows caused by those function's use.

#### `SomeFunctionsChecked`: Pass

Some checked functions were found in '{0}'; however, there were also some unchecked functions, which can occur when the compiler cannot statically determine the length of a buffer/string.  We recommend reviewing your usage of functions like memcpy or strcpy.

#### `NoCheckableFunctions`: Pass

No unsafe functions which can be replaced with checked versions are used in '{0}'.

#### `Error`: Error

No checked functions are present/used when compiling '{0}', and it was compiled with GCC--and it uses functions that can be checked. The Fortify Source flag replaces some unsafe functions with checked versions when a static length can be determined, and can be enabled by passing '-D_FORTIFY_SOURCE=2' when optimization level 2 ('-O2') is enabled.  It is possible that the flag was passed, but that the compiler could not statically determine the length of any buffers/strings.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA3031.EnableClangSafeStack`

### Description

The SafeStack instrumentation pass protects programs by implementing two separate program stacks, one for return addresses and local variables, and the other for everything else. To enable SafeStack, pass '-fsanitize=safe-stack' flag to both compile and link command lines.

### Messages

#### `Pass`: Pass

'{0}' was compiled using Clang and with the SafeStack instrumentation pass, which mitigates the risk of stack-based buffer overflows.

#### `Error`: Error

'{0}' was compiled using Clang but without the SafeStack instrumentation pass, which should be used to mitigate the risk of stack-based buffer overflows. To enable SafeStack, pass '-fsanitize=safe-stack' flag to both compile and link command lines.

#### `ClangVersionMayNeedUpgrade`: Error

'{0}' was compiled using Clang but without the SafeStack instrumentation pass, which should be used to mitigate the risk of stack-based buffer overflows. To enable SafeStack, pass '-fsanitize=safe-stack' flag to both compile and link command lines. You might need to update your version of Clang to enable it.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA5001.EnablePositionIndependentExecutableMachO`

### Description

A Position Independent Executable (PIE) relocates all of its sections at load time, including the code section, if ASLR is enabled in the Linux kernel (instead of just the stack/heap). This makes ROP-style attacks more difficult. This can be enabled by passing '-f pie' to clang/gcc.

### Messages

#### `Pass`: Pass

PIE enabled on executable '{0}'.

#### `Error`: Error

PIE disabled on executable '{0}'.  This means the code section will always be loaded to the same address, even if ASLR is enabled in the Linux kernel.  To address this, ensure you are compiling with '-fpie' when using clang/gcc.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA5002.DoNotAllowExecutableStack`

### Description

This checks if a binary has an executable stack; an executable stack allows attackers to redirect code flow into stack memory, which is an easy place for an attacker to store shellcode. Ensure do not enable flag "--allow_stack_execute".

### Messages

#### `Pass`: Pass

Executable stack is not allowed on executable '{0}'.

#### `Error`: Error

Stack on '{0}' is executable, which means that an attacker could use it as a place to store attack shellcode.  Ensure do not compile with flag "--allow_stack_execute" to mark the stack as non-executable.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2001.LoadImageAboveFourGigabyteAddress`

### Description

64-bit images should have a preferred base address above the 4GB boundary to prevent triggering an Address Space Layout Randomization (ASLR) compatibility mode that decreases security. ASLR compatibility mode reduces the number of locations to which ASLR may relocate the binary, reducing its effectiveness at mitigating memory corruption vulnerabilities. To resolve this issue, either use the default preferred base address by removing any uses of /baseaddress from compiler command lines, or /BASE from linker command lines (recommended), or configure your program to start at a base address above 4GB when compiled for 64 bit platforms (by changing the constant passed to /baseaddress or /BASE). Note that if you choose to continue using a custom preferred base address, you will need to make this modification only for 64-bit builds, as base addresses above 4GB are not valid for 32-bit binaries.

### Messages

#### `Pass`: Pass

'{0}' is a 64-bit image with a base address that is >= 4 gigabytes, increasing the effectiveness of Address Space Layout Randomization (which helps prevent attackers from executing security-sensitive code in well-known locations).

#### `Error`: Error

'{0}' is a 64-bit image with a preferred base address below the 4GB boundary. Having a preferred base address below this boundary triggers a compatibility mode in Address Space Layout Randomization (ASLR) on recent versions of Windows that reduces the number of locations to which ASLR may relocate the binary. This reduces the effectiveness of ASLR at mitigating memory corruption vulnerabilities. To resolve this issue, either use the default preferred base address by removing any uses of /baseaddress from compiler command lines, or /BASE from linker command lines (recommended), or configure your program to start at a base address above 4GB when compiled for 64 bit platforms (by changing the constant passed to /baseaddress or /BASE). Note that if you choose to continue using a custom preferred base address, you will need to make this modification only for 64-bit builds, as base addresses above 4GB are not valid for 32-bit binaries.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2002.DoNotIncorporateVulnerableDependencies`

### Description

Binaries should not take dependencies on code with known security vulnerabilities.

### Messages

#### `Pass`: Pass

'{0}' does not incorporate any known vulnerable dependencies, as configured by current policy.

#### `Error`: Error

'{0}' was built with a version of {1} which is subject to the following issues: {2}. To resolve this, {3}. The source files that triggered this were: {4}

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2004.EnableSecureSourceCodeHashing`

### Description

Compilers can generate and store checksums of source files in order to provide linkage between binaries, their PDBs, and associated source code. This information is typically used to resolve source file when debugging but it can also be used to verify that a specific body of source code is, in fact, the code that was used to produce a specific set of binaries and PDBs. This validation is helpful in verifying supply chain integrity. Due to this security focus, it is important that the hashing algorithm used to produce checksums is secure. Legacy hashing algorithms, such as MD5 and SHA-1, have been demonstrated to be broken by modern hardware (that is, it is computationally feasible to force hash collisions, in which a common hash is generated from distinct files). Using a secure hashing algorithm, such as SHA-256, prevents the possibility of collision attacks, in which the checksum of a malicious file is used to produce a hash that satisfies the system that it is, in fact, the original file processed by the compiler. For managed binaries, pass '-checksumalgorithm:SHA256' on the csc.exe command-line or populate the '<ChecksumAlgorithm>' project property with 'SHA256' to enable secure source code hashing. For native binaries, pass '/ZH:SHA_256' on the cl.exe command-line to enable secure source code hashing.

### Messages

#### `Pass`: Pass

'{0}' is a {1} binary which was compiled with a secure (SHA-256) source code hashing algorithm.

#### `NativeWithInsecureStaticLibraryCompilands`: Warning

'{0}' is a native binary that links one or more static libraries that include object files which were hashed using an insecure checksum algorithm. Insecure checksum algorithms are subject to collision attacks and its use can compromise supply chain integrity. Pass '/ZH:SHA_256' on the cl.exe command-line to enable secure source code hashing. The following modules are out of policy:
{1}

#### `Managed`: Error

'{0}' is a managed binary compiled with an insecure ({1}) source code hashing algorithm. {1} is subject to collision attacks and its use can compromise supply chain integrity. Pass '-checksumalgorithm:SHA256' on the csc.exe command-line or populate the project <ChecksumAlgorithm> property with 'SHA256' to enable secure source code hashing.

#### `NativeWithInsecureDirectCompilands`: Error

'{0}' is a native binary that directly compiles and links one or more object files which were hashed using an insecure checksum algorithm. Insecure checksum algorithms are subject to collision attacks and its use can compromise supply chain integrity. Pass '/ZH:SHA_256' on the cl.exe command-line to enable secure source code hashing. The following modules are out of policy:
{1}

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2005.DoNotShipVulnerableBinaries`

### Description

Do not ship obsolete libraries for which there are known security vulnerabilities.

### Messages

#### `Pass`: Pass

'{0}' is not known to be an obsolete binary that is vulnerable to one or more security problems.

#### `Error`: Error

'{0}' appears to be an obsolete library (version {1}) for which there are known security vulnerabilities. To resolve this issue, obtain a version of {0} that is newer than version {2}. If this binary is not in fact {0}, ignore this warning.

#### `CouldNotParseVersion`: Error

Version information for '{0}' could not be parsed. The binary therefore could not be verified not to be an obsolete binary that is known to be vulnerable to one or more security problems.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2006.BuildWithSecureTools`

### Description

Application code should be compiled with the most up-to-date tool sets possible to take advantage of the most current compile-time security features. Among other things, these features provide address space layout randomization, help prevent arbitrary code execution, and enable code generation that can help prevent speculative execution side-channel attacks.

### Messages

#### `Error`: Error

'{0}' was compiled with one or more modules which were not built using minimum required tool versions (compiler version {1}). More recent toolchains contain mitigations that make it more difficult for an attacker to exploit vulnerabilities in programs they produce. To resolve this issue, compile and/or link your binary with more recent tools. If you are servicing a product where the tool chain cannot be modified (e.g. producing a hotfix for an already shipped version) ignore this warning. Modules built outside of policy: 
{2}

#### `BadModule`: Error

built with {0} compiler version {1} (Front end version {2})

#### `Pass`: Pass

All linked modules of '{0}' satisfy configured policy (observed compilers: {1}).

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2007.EnableCriticalCompilerWarnings`

### Description

Binaries should be compiled with a warning level that enables all critical security-relevant checks. Enabling at least warning level 3 enables important static analysis in the compiler that can identify bugs with a potential to provoke memory corruption, information disclosure, or double-free vulnerabilities. To resolve this issue, compile at warning level 3 or higher by supplying /W3, /W4, or /Wall to the compiler, and resolve the warnings emitted.

### Messages

#### `Pass`: Pass

'{0}' was compiled at a secure warning level ({1}) and does not include any modules that disable specific warnings that are required by policy. As a result, it is less likely that memory corruption, information disclosure, double-free and other security-related vulnerabilities exist in code.

#### `WarningsDisabled`: Error

'{0}' disables compiler warning(s) which are required by policy. A compiler warning is typically required if it has a high likelihood of flagging memory corruption, information disclosure, or double-free vulnerabilities. To resolve this issue, enable the indicated warning(s) by removing /Wxxxx switches (where xxxx is a warning id indicated here) from your command line, and resolve any warnings subsequently raised during compilation. An example compiler command line triggering this check was: {1}
Modules triggering this check were:
{2}

#### `InsufficientWarningLevel`: Error

'{0}' was compiled at too low a warning level (effective warning level {1} for one or more modules). Warning level 3 enables important static analysis in the compiler to flag bugs that can lead to memory corruption, information disclosure, or double-free vulnerabilities. To resolve this issue, compile at warning level 3 or higher by supplying /W3, /W4, or /Wall to the compiler, and resolve the warnings emitted. An example compiler command line triggering this check: {2}
Modules triggering this check: {3}

#### `UnknownModuleLanguage`: Error

'{0}' contains code from an unknown language, preventing a comprehensive analysis of the compiler warning settings. The language could not be identified for the following modules: {1}

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2008.EnableControlFlowGuard`

### Description

Binaries should enable the compiler control guard feature (CFG) at build time to prevent attackers from redirecting execution to unexpected, unsafe locations. CFG analyzes and discovers all indirect-call instructions at compilation and link time. It also injects a check that precedes every indirect call in code that ensures the target is an expected, safe location.  If that check fails at runtime, the operating system will close the program.

### Messages

#### `Pass`: Pass

'{0}' enables the control flow guard mitigation. As a result, the operating system will force an application to close if an attacker is able to redirect execution in the component to an unexpected location.

#### `Error`: Error

'{0}' does not enable the control flow guard (CFG) mitigation. To resolve this issue, pass /guard:cf on both the compiler and linker command lines. Binaries also require the /DYNAMICBASE linker option in order to enable CFG.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

#### `UnsupportedKernelModeVersion`: NotApplicable

'{0}' is a kernel mode portable executable compiled for a version of Windows that does not support the control flow guard feature for kernel mode binaries.

---

## Rule `BA2009.EnableAddressSpaceLayoutRandomization`

### Description

Binaries should linked as DYNAMICBASE to be eligible for relocation by Address Space Layout Randomization (ASLR). ASLR is an important mitigation that makes it more difficult for an attacker to exploit memory corruption vulnerabilities. Configure your tools to build with this feature enabled. For C and C++ binaries, add /DYNAMICBASE to your linker command line. For .NET applications, use a compiler shipping with Visual Studio 2008 or later.

### Messages

#### `Pass`: Pass

'{0}' is properly compiled to enable Address Space Layout Randomization, reducing an attacker's ability to exploit code in well-known locations.

#### `NotDynamicBase`: Error

'{0}' is not marked as DYNAMICBASE. This means that the binary is not eligible for relocation by Address Space Layout Randomization (ASLR). ASLR is an important mitigation that makes it more difficult for an attacker to exploit memory corruption vulnerabilities. To resolve this issue, configure your tools to build with this feature enabled. For C and C++ binaries, add /DYNAMICBASE to your linker command line. For .NET applications, use a compiler shipping with Visual Studio 2008 or later.

#### `RelocsStripped`: Error

'{0}' is marked as DYNAMICBASE but relocation data has been stripped from the image, preventing address space layout randomization. 

#### `WinCENoRelocationSection`: Error

'{0}' is a Windows CE image but does not contain any relocation data, preventing Address Space Layout Randomization.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2010.DoNotMarkImportsSectionAsExecutable`

### Description

PE sections should not be marked as both writable and executable. This condition makes it easier for an attacker to exploit memory corruption vulnerabilities, as it may provide an attacker executable location(s) to inject shellcode. Because the loader will always mark the imports section as writable, it is therefore important to mark this section as non-executable. To resolve this issue, ensure that your program does not mark the imports section executable. Look for uses of /SECTION or /MERGE on the linker command line, or #pragma segment in source code, which change the imports section to be executable, or which merge the ".rdata" segment into an executable section.

### Messages

#### `Pass`: Pass

'{0}' does not have an imports section that is marked as executable, helping to prevent the exploitation of code vulnerabilities.

#### `Error`: Error

'{0}' has the imports section marked executable. Because the loader will always mark the imports section as writable, it is important to mark this section as non-executable, so that an attacker cannot place shellcode here. To resolve this issue, ensure that your program does not mark the imports section as executable. Look for uses of /SECTION or /MERGE on the linker command line, or #pragma segment in source code, which change the imports section to be executable, or which merge the ".rdata" segment into an executable section.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2011.EnableStackProtection`

### Description

Binaries should be built with the stack protector buffer security feature (/GS) enabled to increase the difficulty of exploiting stack buffer overflow memory corruption vulnerabilities. To resolve this issue, ensure that all modules compiled into the binary are compiled with the stack protector enabled by supplying /GS on the Visual C++ compiler command line.

### Messages

#### `Pass`: Pass

'{0}' is a C or C++ binary built with the stack protector buffer security feature enabled for all modules, making it more difficult for an attacker to exploit stack buffer overflow memory corruption vulnerabilities. 

#### `Error`: Error

'{0}' is a C or C++ binary built with the stack protector buffer security feature disabled in one or more modules. The stack protector (/GS) is a security feature of the compiler which makes it more difficult to exploit stack buffer overflow memory corruption vulnerabilities. To resolve this issue, ensure that your code is compiled with the stack protector enabled by supplying /GS on the Visual C++ compiler command line. The affected modules were: {1}

#### `UnknownModuleLanguage`: Error

'{0}' contains code from an unknown language, preventing a comprehensive analysis of the stack protector buffer security features. The language could not be identified for the following modules: {1}.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2012.DoNotModifyStackProtectionCookie`

### Description

Application code should not interfere with the stack protector. The stack protector (/GS) is a security feature of the compiler which makes it more difficult to exploit stack buffer overflow memory corruption vulnerabilities. The stack protector relies on a random number, called the "security cookie", to detect these buffer overflows. This 'cookie' is statically linked with your binary from a Visual C++ library in the form of the symbol __security_cookie. On recent Windows versions, the loader looks for the statically linked value of this cookie, and initializes the cookie with a far better source of entropy -- the system's secure random number generator -- rather than the limited random number generator available early in the C runtime startup code. When this symbol is not the default value, the additional entropy is not injected by the operating system, reducing the effectiveness of the stack protector. To resolve this issue, ensure that your code does not reference or create a symbol named __security_cookie or __security_cookie_complement.

### Messages

#### `Pass`: Pass

'{0}' is a C or C++ binary built with the buffer security feature that properly preserves the stack protecter cookie. This has the effect of enabling a significant increase in entropy provided by the operating system over that produced by the C runtime start-up code.

#### `NoLoadConfig`: Pass

'{0}' is  C or C++binary that does not contain a load config table, which indicates either that it was compiled and linked with a version of the compiler that precedes stack protection features or is a binary (such as an ngen'ed assembly) that is not subject to relevant security issues.

#### `Error`: Error

'{0}' is a C or C++ binary that interferes with the stack protector. The stack protector (/GS) is a security feature of the compiler which makes it more difficult to exploit stack buffer overflow memory corruption vulnerabilities. The stack protector relies on a random number, called the "security cookie", to detect these buffer overflows. This 'cookie' is statically linked with your binary from a Visual C++ library in the form of the symbol __security_cookie. On recent Windows versions, the loader looks for the magic statically linked value of this cookie, and initializes the cookie with a far better source of entropy -- the system's secure random number generator -- rather than the limited random number generator available early in the C runtime startup code. When this symbol is not the default value, the additional entropy is not injected by the operating system, reducing the effectiveness of the stack protector. To resolve this issue, ensure that your code does not reference or create a symbol named __security_cookie or __security_cookie_complement. NOTE: the modified cookie value detected was: {1}

#### `CouldNotLocateCookie`: Error

'{0}' is a C or C++binary that enables the stack protection feature but the security cookie could not be located. The binary may be corrupted.

#### `InvalidSecurityCookieOffset`: Warning

'{0}' appears to be a packed C or C++ binary that reports a security cookie offset that exceeds the size of the packed file. Use of the stack protector (/GS) feature therefore could not be verified. The file was possibly packed by: {1}.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2013.InitializeStackProtection`

### Description

Binaries should properly initialize the stack protector (/GS) in order to increase the difficulty of exploiting stack buffer overflow memory corruption vulnerabilities. The stack protector requires access to entropy in order to be effective, which means a binary must initialize a random number generator at startup, by calling __security_init_cookie() as close to the binary's entry point as possible. Failing to do so will result in spurious buffer overflow detections on the part of the stack protector. To resolve this issue, use the default entry point provided by the C runtime, which will make this call for you, or call __security_init_cookie() manually in your custom entry point.

### Messages

#### `Pass`: Pass

'{0}' is a C or C++ binary built with the buffer security feature that properly initializes the stack protecter. This has the effect of increasing the effectiveness of the feature and reducing spurious detections.

#### `NoCode`: Pass

'{0}' is a C or C++ binary that is not required to initialize the stack protection, as it does not contain executable code.

#### `FeatureNotEnabled`: NotApplicable

'{0}' is a C or C++ binary that does not enable the stack protection buffer security feature. It is therefore not required to initialize the stack protector.

#### `Error`: Error

'{0}' is a C or C++ binary that does not initialize the stack protector. The stack protector (/GS) is a security feature of the compiler which makes it more difficult to exploit stack buffer overflow memory corruption vulnerabilities. The stack protector requires access to entropy in order to be effective, which means a binary must initialize a random number generator at startup, by calling __security_init_cookie() as close to the binary's entry point as possible. Failing to do so will result in spurious buffer overflow detections on the part of the stack protector. To resolve this issue, use the default entry point provided by the C runtime, which will make this call for you, or call __security_init_cookie() manually in your custom entry point.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2014.DoNotDisableStackProtectionForFunctions`

### Description

Application code should not disable stack protection for individual functions. The stack protector (/GS) is a security feature of the Windows native compiler which makes it more difficult to exploit stack buffer overflow memory corruption vulnerabilities. Disabling the stack protector, even on a function-by-function basis, can compromise the security of code. To resolve this issue, remove occurrences of __declspec(safebuffers) from your code. If the additional code inserted by the stack protector has been shown in profiling to cause a significant performance problem for your application, attempt to move stack buffer modifications out of the hot path of execution to allow the compiler to avoid inserting stack protector checks in these locations rather than disabling the stack protector altogether.

### Messages

#### `Pass`: Pass

'{0}' is a C or C++ binary built with the stack protector buffer security feature enabled which does not disable protection for any individual functions (via __declspec(safebuffers), making it more difficult for an attacker to exploit stack buffer overflow memory corruption vulnerabilities.

#### `Error`: Error

'{0}' is a C or C++ binary built with function(s) ({1}) that disable the stack protector. The stack protector (/GS) is a security feature of the compiler which makes it more difficult to exploit stack buffer overflow memory corruption vulnerabilities. Disabling the stack protector, even on a function-by-function basis, is disallowed by SDL policy. To resolve this issue, remove occurrences of __declspec(safebuffers) from your code. If the additional code inserted by the stack protector has been shown in profiling to cause a significant performance problem for your application, attempt to move stack buffer modifications out of the hot path of execution to allow the compiler to avoid inserting stack protector checks in these locations rather than disabling the stack protector altogether.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2015.EnableHighEntropyVirtualAddresses`

### Description

Binaries should be marked as high entropy Address Space Layout Randomization (ASLR) compatible. High entropy allows ASLR to be more effective in mitigating memory corruption vulnerabilities. To resolve this issue, configure your tool chain to mark the program high entropy compatible; e.g. by supplying /HIGHENTROPYVA to the C or C++ linker command line. Binaries must also be compiled as /LARGEADDRESSAWARE in order to enable high entropy ASLR.

### Messages

#### `Pass`: Pass

'{0}' is high entropy ASLR compatible, reducing an attacker's ability to exploit code in well-known locations.

#### `NoHighEntropyVA`: Error

'{0}' does not declare itself as high entropy ASLR compatible. High entropy makes Address Space Layout Randomization more effective in mitigating memory corruption vulnerabilities. To resolve this issue, configure your tools to mark the program high entropy compatible; e.g. by supplying /HIGHENTROPYVA to the C or C++ linker command line. (This image was determined to have been properly compiled as /LARGEADDRESSAWARE.)

#### `NoLargeAddressAware`: Error

'{0}' does not declare itself as high entropy ASLR compatible. High entropy makes Address Space Layout Randomization more effective in mitigating memory corruption vulnerabilities. To resolve this issue, configure your tools to mark the program high entropy compatible by supplying /LARGEADDRESSAWARE to the C or C++ linker command line. (This image was determined to have been properly compiled as /HIGHENTROPYVA.)

#### `NeitherHighEntropyVANorLargeAddressAware`: Error

'{0}' does not declare itself as high entropy ASLR compatible. High entropy makes Address Space Layout Randomization more effective in mitigating memory corruption vulnerabilities. To resolve this issue, configure your tools to mark the program high entropy compatible; e.g. by supplying /HIGHENTROPYVA as well as /LARGEADDRESSAWARE to the C or C++ linker command line.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2016.MarkImageAsNXCompatible`

### Description

Binaries should be marked as NX compatible to help prevent execution of untrusted data as code. The NXCompat bit, also known as "Data Execution Prevention" (DEP) or "Execute Disable" (XD), triggers a processor security feature that allows a program to mark a piece of memory as non-executable. This helps mitigate memory corruption vulnerabilities by preventing an attacker from supplying direct shellcode in their exploit (because the exploit comes in the form of input data to the exploited program on a data segment, rather than on an executable code segment). Ensure that your tools are configured to mark your binaries as NX compatible, e.g. by passing /NXCOMPAT to the C/C++ linker.

### Messages

#### `Pass`: Pass

'{0}' is marked as NX compatible, helping to prevent attackers from executing code that is injected into data segments.

#### `Error`: Error

'{0}' is not marked NX compatible. The NXCompat bit, also known as "Data Execution Prevention" (DEP) or "Execute Disable" (XD), is a processor feature that allows a program to mark a piece of memory as non-executable. This helps mitigate memory corruption vulnerabilities by preventing an attacker from supplying direct shellcode in their exploit, because the exploit comes in the form of input data to the exploited program on a data segment, rather than on an executable code segment. To resolve this issue, ensure that your tools are configured to mark your binaries as NX compatible, e.g. by passing /NXCOMPAT to the C/C++ linker.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2018.EnableSafeSEH`

### Description

X86 binaries should enable the SafeSEH mitigation to minimize exploitable memory corruption issues. SafeSEH makes it more difficult to exploit vulnerabilities that permit overwriting SEH control blocks on the stack, by verifying that the location to which a thrown SEH exception would jump is indeed defined as an exception handler in the source program (and not shellcode). To resolve this issue, supply the /SafeSEH flag on the linker command line. Note that you will need to configure your build system to supply this flag for x86 builds only, as the /SafeSEH flag is invalid when linking for ARM and x64.

### Messages

#### `Pass`: Pass

'{0}' is an x86 binary that enables SafeSEH, a mitigation that verifies SEH exception jump targets are defined as exception handlers in the program (and not shellcode).

#### `NoSEH`: Pass

'{0}' is an x86 binary that does not use SEH, making it an invalid target for exploits that attempt to replace SEH jump targets with attacker-controlled shellcode.

#### `Error`: Error

'{0}' is an x86 binary which {1}, indicating that it does not enable the SafeSEH mitigation. SafeSEH makes it more difficult to exploit memory corruption vulnerabilities that can overwrite SEH control blocks on the stack, by verifying that the location to which a thrown SEH exception would jump is indeed defined as an exception handler in the source program (and not shellcode). To resolve this issue, supply the /SafeSEH flag on the linker command line. Note that you will need to configure your build system to supply this flag for x86 builds only, as the /SafeSEH flag is invalid when linking for ARM and x64.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2019.DoNotMarkWritableSectionsAsShared`

### Description

Code or data sections should not be marked as both shared and writable. Because these sections are shared across processes, this condition might permit a process with low privilege to alter memory in a higher privilege process. If you do not actually require that a section be both writable and shared, remove one or both of these attributes (by modifying your .DEF file, the appropriate linker /section switch arguments, etc.). If you must share common data across processes (for inter-process communication (IPC) or other purposes) use CreateFileMapping with proper security attributes or an actual IPC mechanism instead (COM, named pipes, LPC, etc.).

### Messages

#### `Pass`: Pass

'{0}' contains no data or code sections marked as both shared and writable, helping to prevent the exploitation of code vulnerabilities.

#### `Error`: Error

'{0}' contains one or more code or data sections ({1}) which are marked as both shared and writable. Because these sections are shared across processes, this condition might permit a process with low privilege to alter memory in a higher privilege process. If you do not actually require that a section be both writable and shared, remove one or both of these attributes (by modifying your .DEF file, the appropriate linker /section switch arguments, etc.). If you must share common data across processes (for inter-process communication (IPC) or other purposes) use CreateFileMapping with proper security attributes or an actual IPC mechanism instead (COM, named pipes, LPC, etc.).

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2021.DoNotMarkWritableSectionsAsExecutable`

### Description

PE sections should not be marked as both writable and executable. This condition makes it easier for an attacker to exploit memory corruption vulnerabilities, as it may provide an attacker executable location(s) to inject shellcode. To resolve this issue, configure your tools to not emit memory sections that are writable and executable. For example, look for uses of /SECTION on the linker command line for C and C++ programs, or #pragma section in C and C++ source code, which mark a section with both attributes. Be sure to disable incremental linking in release builds, as this feature creates a writable and executable section named '.textbss' in order to function.

### Messages

#### `Pass`: Pass

'{0}' contains no data or code sections marked as both shared and executable, helping to prevent the exploitation of code vulnerabilities.

#### `Error`: Error

'{0}' contains PE section(s) ({1}) that are both writable and executable. Writable and executable memory segments make it easier for an attacker to exploit memory corruption vulnerabilities, because it may provide an attacker executable location(s) to inject shellcode. To resolve this issue, configure your tools to not emit memory sections that are writable and executable. For example, look for uses of /SECTION on the linker command line for C and C++ programs, or #pragma section in C and C++ source code, which mark a section with both attributes. Enabling incremental linking via the /INCREMENTAL argument (the default for Microsoft Visual Studio debug build) can also result in a writable and executable section named 'textbss'. For this case, disable incremental linking (or analyze an alternate build configuration that disables this feature) to resolve the problem.

#### `UnexpectedSectionAligment`: Error

'{0}' has a section alignment ({1}) that is smaller than its page size ({2}).

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2022.SignSecurely`

### Description

Images should be correctly signed by trusted publishers using cryptographically secure signature algorithms. This rule invokes WinTrustVerify to validate that binary hash, signing and public key algorithms are secure and, where configurable, that key sizes meet acceptable size thresholds.

### Messages

#### `Pass`: Pass

'{0}' appears to be signed with secure cryptographic algorithms. WinTrustVerify successfully validated the binary but did not attempt to validate certificate chaining or that the root certificate is trusted. The following digitial signature algorithms were detected: {1}

#### `BadSigningAlgorithm`: Error

'{0}' was signed exclusively with algorithms that WinTrustVerify has flagged as insecure. {1}

#### `DidNotVerify`: Error

'{0}' signing was flagged as insecure by WinTrustVerify with error code '{1}' ({2})

#### `WinTrustVerifyApiError`: Error

'{0}' signing could not be completely verified because '{1}' failed with error code: '{2}'.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2024.EnableSpectreMitigations`

### Description

Application code should be compiled with the Spectre mitigations switch (/Qspectre cl.exe command-line argument or <SpectreMitigation>Spectre</SpectreMitigation> build property). Spectre attacks can compromise hardware-based isolation, allowing non-privileged users to retrieve potentially sensitive data from the CPU cache. To resolve this issue, provide the /Qspectre switch on the compiler command-line (or specify <SpectreMitigation>Spectre</SpectreMitigation> in build properties), or pass /d2guardspecload in cases where your compiler supports this switch and it is not possible to update to a toolset that supports /Qspectre. This warning should be addressed for code that operates on data that crosses a trust boundary and that can affect execution, such as parsing untrusted file inputs or processing query strings of a web request. You may need to install the 'C++ spectre-mitigated libs' component from the Visual Studio installer if you observe violations against C runtime libraries such as libcmt.lib, libvcruntime.lib, etc.

### Messages

#### `Warning`: Warning

'{0}' was compiled with one or more modules that do not enable code generation mitigations for speculative execution side-channel attack (Spectre) vulnerabilities. Spectre attacks can compromise hardware-based isolation, allowing non-privileged users to retrieve potentially sensitive data from the CPU cache. To resolve the issue, provide the /Qspectre switch on the compiler command-line (or specify <SpectreMitigation>Spectre</SpectreMitigation> in build properties), or pass /d2guardspecload in cases where your compiler supports this switch and it is not possible to update to a toolset that supports /Qspectre. This warning should be addressed for code that operates on data that crosses a trust boundary and that can affect execution, such as parsing untrusted file inputs or processing query strings of a web request.
{1}

#### `OptimizationsDisabled`: Warning

The following modules were compiled with optimizations disabled (/Od), a condition that disables Spectre mitigations:
{0}

#### `SpectreMitigationNotEnabled`: Warning

The following modules were compiled with a toolset that supports /Qspectre but the switch was not enabled on the command-line:
{0}

#### `SpectreMitigationExplicitlyDisabled`: Warning

The following modules were compiled with Spectre mitigations explicitly disabled:
{0}

#### `Pass`: Pass

All linked modules '{0}' were compiled with mitigations enabled that help prevent Spectre (speculative execution side-channel attack) vulnerabilities.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2025.EnableShadowStack`

### Description

Control-flow Enforcement Technology (CET) Shadow Stack is a computer processor feature that provides capabilities to defend against return-oriented programming (ROP) based malware attacks. Note: older versions of .NET are not compatible with CET/shadow stack technology. If your native process loads older managed assemblies (.NET 6 or earlier), unhandled exceptions in those components may not be handled properly and may cause your process to crash.

### Messages

#### `Pass`: Pass

'{0}' enables the Control-flow Enforcement Technology (CET) Shadow Stack mitigation.

#### `Warning`: Warning

'{0}' does not enable the Control-flow Enforcement Technology (CET) Shadow Stack mitigation. To resolve this issue, pass /CETCOMPAT on the linker command lines.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2026.EnableMicrosoftCompilerSdlSwitch`

### Description

/sdl enables a superset of the baseline security checks provided by /GS and overrides /GS-. By default, /sdl is off. /sdl- disables the additional security checks.

### Messages

#### `Pass`: Pass

'{0}' is a Windows PE that was compiled with recommended Security Development Lifecycle (SDL) checks. These checks change security-relevant warnings into errors, and set additional secure code-generation features.

#### `Warning`: Warning

'{0}' is a Windows PE that wasn't compiled with recommended Security Development Lifecycle (SDL) checks. As a result some critical compile-time and runtime checks may be disabled, increasing the possibility of an exploitable runtime issue. To resolve this problem, pass '/sdl' on the cl.exe command-line, set the 'SDL checks' property in the 'C/C++ -> General' Configuration property page, or explicitly set the 'SDLCheck' property in the project file (nested within a 'CLCompile' element) to 'true'.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA2027.EnableSourceLink`

### Description

SourceLink information should be present in the PDB. This applies to binaries built with the C# and MSVC compilers. When enabled, SourceLink information is added to the PDB. That information includes the repository URLs and commit IDs for all source files fed to the compiler. The PDB should also be uploaded to a symbol server so that it can be discovered by a debugger such as Visual Studio. Developers can then step into the matching source code. Frictionless source-driven debugging provides a good user experience for consumers and also accelerates security response in the event of supply-chain compromise. See https://aka.ms/sourcelink for more information.

### Messages

#### `Pass`: Pass

The PDB for '{0}' contains SourceLink information, maximizing engineering and security response efficiency when source code is required for debugging and other critical analysis.

#### `Warning`: Warning

The PDB for '{0}' does not contain SourceLink information, compromising frictionless source-driven debugging and increasing latency of security response. Enable SourceLink by configuring necessary project properties and adding a package reference for your source control provider. See https://aka.ms/sourcelink for more information.

---

## Rule `BA4001.ReportPECompilerData`

### Description

This rule emits CSV data to the console for every compiler/language/version combination that's observed in any PDB-linked compiland.

### Messages

---

## Rule `BA6001.DisableIncrementalLinkingInReleaseBuilds`

### Description

Incremental linking support increases binary size and can reduce runtime performance. The support for incremental linking adds padding and other overhead to support the ability to modify a binary without a full link.  The use of incrementally linked binaries may reduce the level of determinism because previous compilations will have lingering effects on subsequent compilations.  Fully optimized release builds should not specify incremental linking.

### Messages

#### `Pass`: Pass

'{0}' was compiled with incremental linking disabled.

#### `Warning`: Warning

'{0}' appears to be compiled as release but enables incremental linking, increasing binary size and further compromising runtime performance by preventing enabling maximal code optimization.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA6002.EliminateDuplicateStrings`

### Description

The /GF compiler option, also known as Eliminate Duplicate Strings or String Pooling, will combine identical strings in a program to a single readonly copy. This can significantly reduce binary size for programs with many string resources.

### Messages

#### `Pass`: Pass

'{0}' was compiled with Eliminate Duplicate Strings (/GF) enabled.

#### `Warning`: Warning

'{0}' was compiled without Eliminate Duplicate Strings (/GF) enabled, increasing binary size.  The following modules do not specify that policy: {1}.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA6004.EnableComdatFolding`

### Description

COMDAT folding can significantly reduce binary size by combining functions which generate identical machine code into a single copy in the final binary.

### Messages

#### `Pass`: Pass

'{0}' was compiled with COMDAT folding (/OPT:ICF) enabled

#### `EnabledForDebug`: Warning

'{0}' appears to be a Debug build which was compiled with COMDAT folding (/OPT:ICF) enabled. That may make debugging more difficult.

#### `DisabledForRelease`: Warning

'{0}' was compiled with COMDAT folding (/OPT:ICF) disabled, increasing binary size.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA6005.EnableOptimizeReferences`

### Description

Optimize References can significantly reduce binary size because it instructs the linker to remove unreferenced functions and data from the final binary.

### Messages

#### `Pass`: Pass

'{0}' was compiled with Optimize References (/OPT:REF) enabled

#### `Warning`: Warning

'{0}' was compiled with Optimize References (/OPT:REF) disabled, increasing binary size.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

## Rule `BA6006.EnableLinkTimeCodeGeneration`

### Description

Enabling Link Time Code Generation (LTCG) performs whole-program optimization, which is able to better optimize code across translation units. LTCG is also a prerequisite for Profile-Guided Optimization (PGO) which can further improve performance.

### Messages

#### `Pass`: Pass

'{0}' was compiled with LinkTimeCodeGeneration (/LTCG) enabled.

#### `Warning`: Warning

'{0}' was compiled without Link Time Code Generation (/LTCG). Enabling LTCG can improve optimizations and performance.

#### `InvalidMetadata`: NotApplicable

'{0}' was not evaluated for check '{1}' as the analysis is not relevant based on observed metadata: {2}.

---

