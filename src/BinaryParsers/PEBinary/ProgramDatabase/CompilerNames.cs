// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase
{
    public static class CompilerNames
    {
        public const string MicrosoftC = "Microsoft (R) Optimizing Compiler";      // cl.exe / c1.dll / c2.dll
        public const string MicrosoftCxx = "Microsoft (R) Optimizing Compiler";    // cl.exe / c1xx.dll / c2.dll
        public const string MicrosoftLink = "Microsoft (R) LINK";                  // link.exe
        public const string MicrosoftCsharp = "test";
        public const string MicrosoftRc = "test";
        public const string MicrosoftCvtres = "Microsoft (R) CVTRES";              // rc.exe / cvtres.exe
        public const string MicrosoftMasm = "Microsoft (R) Macro Assembler";       // ml.exe | ml64.exe
        public const string MicrosoftARMasm = "Microsoft (R) ARM Macro Assembler"; // armasm.exe | armasm64.exe
    }
}
