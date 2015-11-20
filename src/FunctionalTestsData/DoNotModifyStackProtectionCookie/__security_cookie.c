// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

//
// Compile with cl /GS /Z7 /W4 /DEBUG /Zc:inline /Fe__security_cookie __security_cookie.c /link /force
//

// This symbol breaks the loader GS entropy mechanism by overriding the
// compiler's built in __security_cookie symbol.

// Note that /force is required to the linker to even get this to compile...
// Do we actually need this as a check?
unsigned int __security_cookie = 0;

void main() {}
