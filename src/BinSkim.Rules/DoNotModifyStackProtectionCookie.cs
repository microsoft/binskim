// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Composition;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(IBinarySkimmer))]
    public class DoNotModifyStackProtectionCookie : IBinarySkimmer, IRuleContext
    {
        public string Id { get { return RuleConstants.DoNotModifyStackProtectionCookieId; } }

        public string Name { get { return nameof(DoNotModifyStackProtectionCookie); } }

        public void Initialize(BinaryAnalyzerContext context) { return; }

        public AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            return StackProtectionUtilities.CommonCanAnalyze(context, out reasonForNotAnalyzing);
        }

        public void Analyze(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;

            if (peHeader.LoadConfigTableDirectory.RelativeVirtualAddress == 0)
            {
                // LOAD_CONFIG block absent. This can occur in 2 cases:
                // 1. The user has C or C++ code linked with a linker older than Dev11 (VS2010)
                // 2. The code is not C or C++ code at all.
                // 
                // In the first case we expect CompilerVersionCheck to fire on this code. In the
                // second case we don't want to warn because the code is likely safe; 
                // e.g. .NET ngen'd images fall into this bucket.

                //'{0}' is  C or C++binary that does not contain a load config table, which 
                // indicates either that it was compiled and linked with a version of the 
                // compiler that precedes stack protection features or is a binary (such as 
                // an ngen'ed assembly) that is not subject to relevant security issues.
                context.Logger.Log(MessageKind.Pass, context,
                    RuleUtilities.BuildMessage(context,
                        RulesResources.DoNotModifyStackProtectionCookie_NoLoadConfig_Pass));
                return;
            }

            if (context.PE.Is64Bit)
            {
                if (!Validate64BitImage(context))
                {
                    return;
                }
            }
            else if (!Validate32BitImage(context))
            {
                return;
            }

            // '{0}' is a C or C++ binary built with the buffer security feature 
            // that properly preserves the stack protecter cookie. This has the 
            // effect of enabling a significant increase in entropy provided by 
            // the operating system over that produced by the C runtime start-up 
            // code.
            context.Logger.Log(MessageKind.Pass, context,
                RuleUtilities.BuildMessage(context,
                    RulesResources.DoNotModifyStackProtectionCookie_Pass));
        }
        private bool Validate64BitImage(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;
            SafePointer sp = new SafePointer(context.PE.ImageBytes, peHeader.LoadConfigTableDirectory.RelativeVirtualAddress);
            SafePointer loadConfigVA = context.PE.RVA2VA(sp);
            ImageLoadConfigDirectory64 loadConfig = new ImageLoadConfigDirectory64(peHeader, loadConfigVA);

            UInt64 cookieVA = (UInt64)loadConfig.GetField(ImageLoadConfigDirectory64.Fields.SecurityCookie);
            UInt64 baseAddress = peHeader.ImageBase;

            // we need to find the offset in the file based on the cookie's VA
            UInt64 sectionSize, sectionVA = 0;
            SectionHeader ish = new SectionHeader();
            bool foundCookieSection = false;
            foreach (SectionHeader t in context.PE.PEHeaders.SectionHeaders)
            {
                sectionVA = (UInt64)(UInt32)t.VirtualAddress + baseAddress;
                sectionSize = (UInt32)t.VirtualSize;
                if ((cookieVA >= sectionVA) &&
                    (cookieVA < sectionVA + sectionSize))
                {
                    ish = t;
                    foundCookieSection = true;
                    break;
                }
            }

            if (!foundCookieSection)
            {
                // '{0}' is a C or C++binary that enables the stack protection feature but the security cookie could not be located. The binary may be corrupted.
                context.Logger.Log(MessageKind.Fail, context,
                        RuleUtilities.BuildMessage(context,
                            RulesResources.DoNotModifyStackProtectionCookie_CouldNotLocateCookie_Fail));
                return false;
            }

            UInt64 fileCookieOffset = (cookieVA - baseAddress) - (sectionVA - baseAddress) + (UInt32)ish.PointerToRawData;
            SafePointer fileCookiePtr = loadConfigVA;
            fileCookiePtr.Address = (int)fileCookieOffset;
            UInt64 cookie = BitConverter.ToUInt64(fileCookiePtr.GetBytes(8), 0);

            if (cookie != StackProtectionUtilities.DefaultCookieX64)
            {
                // '{0}' is a C or C++ binary that interferes with the stack protector. The 
                // stack protector (/GS) is a security feature of the compiler which makes 
                // it more difficult to exploit stack buffer overflow memory corruption 
                // vulnerabilities. The stack protector relies on a random number, called 
                // the "security cookie", to detect these buffer overflows. This 'cookie' 
                // is statically linked with your binary from a Visual C++ library in the 
                // form of the symbol __security_cookie. On recent Windows versions, the 
                // loader looks for the magic statically linked value of this cookie, and 
                // initializes the cookie with a far better source of entropy -- the system's 
                // secure random number generator -- rather than the limited random number 
                // generator available early in the C runtime startup code. When this symbol 
                // is not the default value, the additional entropy is not injected by the 
                // operating system, reducing the effectiveness of the stack protector. To 
                // resolve this issue, ensure that your code does not reference or create a 
                // symbol named __security_cookie or __security_cookie_complement. NOTE: 
                // the modified cookie value detected was: {1}
                context.Logger.Log(MessageKind.Fail, context,
                    RuleUtilities.BuildMessage(context,
                    RulesResources.DoNotModifyStackProtectionCookie_Fail, cookie.ToString("x")));
                return false;
            }
            return true;
        }


        private bool Validate32BitImage(BinaryAnalyzerContext context)
        {
            PEHeader peHeader = context.PE.PEHeaders.PEHeader;
            SafePointer sp = new SafePointer(context.PE.ImageBytes, peHeader.LoadConfigTableDirectory.RelativeVirtualAddress);
            SafePointer loadConfigVA = context.PE.RVA2VA(sp);
            ImageLoadConfigDirectory32 loadConfig = new ImageLoadConfigDirectory32(peHeader, loadConfigVA);

            UInt32 cookieVA = (UInt32)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.SecurityCookie);
            UInt32 baseAddress = (UInt32)peHeader.ImageBase;

            // we need to find the offset in the file based on the cookie's VA
            UInt32 sectionSize, sectionVA = 0;
            SectionHeader ish = new SectionHeader();
            bool foundCookieSection = false;
            foreach (SectionHeader t in context.PE.PEHeaders.SectionHeaders)
            {
                sectionVA = (UInt32)t.VirtualAddress + baseAddress;
                sectionSize = (UInt32)t.VirtualSize;
                if ((cookieVA >= sectionVA) &&
                    (cookieVA < sectionVA + sectionSize))
                {
                    ish = t;
                    foundCookieSection = true;
                    break;
                }
            }

            if (!foundCookieSection)
            {
                // '{0}' is a C or C++binary that enables the stack protection feature but the security cookie could not be located. The binary may be corrupted.
                context.Logger.Log(MessageKind.Fail, context,
                        RuleUtilities.BuildMessage(context,
                            RulesResources.DoNotModifyStackProtectionCookie_CouldNotLocateCookie_Fail));
                return false;
            }

            UInt64 fileCookieOffset = (cookieVA - baseAddress) - (sectionVA - baseAddress) + (UInt32)ish.PointerToRawData;
            SafePointer fileCookiePtr = loadConfigVA;
            fileCookiePtr.Address = (int)fileCookieOffset;
            UInt32 cookie = BitConverter.ToUInt32(fileCookiePtr.GetBytes(8), 0);

            if (!StackProtectionUtilities.DefaultCookiesX86.Contains(cookie) && context.PE.Machine == Machine.I386)
            {
                // '{0}' is a C or C++ binary that interferes with the stack protector. The 
                // stack protector (/GS) is a security feature of the compiler which makes 
                // it more difficult to exploit stack buffer overflow memory corruption 
                // vulnerabilities. The stack protector relies on a random number, called 
                // the "security cookie", to detect these buffer overflows. This 'cookie' 
                // is statically linked with your binary from a Visual C++ library in the 
                // form of the symbol __security_cookie. On recent Windows versions, the 
                // loader looks for the magic statically linked value of this cookie, and 
                // initializes the cookie with a far better source of entropy -- the system's 
                // secure random number generator -- rather than the limited random number 
                // generator available early in the C runtime startup code. When this symbol 
                // is not the default value, the additional entropy is not injected by the 
                // operating system, reducing the effectiveness of the stack protector. To 
                // resolve this issue, ensure that your code does not reference or create a 
                // symbol named __security_cookie or __security_cookie_complement. NOTE: 
                // the modified cookie value detected was: {1}
                context.Logger.Log(MessageKind.Fail, context,
                    RuleUtilities.BuildMessage(context,
                    RulesResources.DoNotModifyStackProtectionCookie_Fail, cookie.ToString("x")));
                return false;
            }

            return true;
        }
    }
}
