// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis.BinaryParsers;
using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(Skimmer<BinaryAnalyzerContext>)), Export(typeof(ReportingDescriptor))]
    public class DoNotModifyStackProtectionCookie : PEBinarySkimmerBase
    {
        /// <summary>
        /// BA2012
        /// </summary>
        public override string Id => RuleIds.DoNotModifyStackProtectionCookie;

        /// <summary>
        /// Application code should not interfere with the stack protector. The stack
        /// protector (/GS) is a security feature of the compiler which makes it more
        /// difficult to exploit stack buffer overflow memory corruption
        /// vulnerabilities. The stack protector relies on a random number, called
        /// the "security cookie", to detect these buffer overflows. This 'cookie' is
        /// statically linked with your binary from a Visual C++ library in the form
        /// of the symbol __security_cookie. On recent Windows versions, the loader
        /// looks for the magic statically linked value of this cookie, and
        /// initializes the cookie with a far better source of entropy -- the
        /// system's secure random number generator -- rather than the limited random
        /// number generator available early in the C runtime startup code. When this
        /// symbol is not the default value, the additional entropy is not injected
        /// by the operating system, reducing the effectiveness of the stack
        /// protector. To resolve this issue, ensure that your code does not
        /// reference or create a symbol named __security_cookie or
        /// __security_cookie_complement.
        /// </summary>
        public override MultiformatMessageString FullDescription => new MultiformatMessageString { Text = RuleResources.BA2012_DoNotModifyStackProtectionCookie_Description };

        protected override IEnumerable<string> MessageResourceNames => new string[] {
                    nameof(RuleResources.BA2012_Pass),
                    nameof(RuleResources.BA2012_Pass_NoLoadConfig),
                    nameof(RuleResources.BA2012_Error),
                    nameof(RuleResources.BA2012_Error_CouldNotLocateCookie),
                    nameof(RuleResources.BA2012_Warning_InvalidSecurityCookieOffset),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };

        public override AnalysisApplicability CanAnalyzePE(PEBinary target, Sarif.PropertiesDictionary policy, out string reasonForNotAnalyzing)
        {
            return StackProtectionUtilities.CommonCanAnalyze(target, out reasonForNotAnalyzing);
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PEHeader peHeader = target.PE.PEHeaders.PEHeader;

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
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                        nameof(RuleResources.BA2012_Pass_NoLoadConfig),
                        context.TargetUri.GetFileName()));
                return;
            }

            if (target.PE.Is64Bit)
            {
                if (!this.Validate64BitImage(context))
                {
                    return;
                }
            }
            else if (!this.Validate32BitImage(context))
            {
                return;
            }

            // '{0}' is a C or C++ binary built with the buffer security feature 
            // that properly preserves the stack protecter cookie. This has the 
            // effect of enabling a significant increase in entropy provided by 
            // the operating system over that produced by the C runtime start-up 
            // code.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                    nameof(RuleResources.BA2012_Pass),
                    context.TargetUri.GetFileName()));
        }

        private bool Validate64BitImage(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();

            PEHeader peHeader = target.PE.PEHeaders.PEHeader;
            var sp = new SafePointer(target.PE.ImageBytes, peHeader.LoadConfigTableDirectory.RelativeVirtualAddress);
            SafePointer loadConfigVA = target.PE.RVA2VA(sp);
            var loadConfig = new ImageLoadConfigDirectory64(peHeader, loadConfigVA);

            ulong cookieVA = (ulong)loadConfig.GetField(ImageLoadConfigDirectory64.Fields.SecurityCookie);
            ulong baseAddress = peHeader.ImageBase;

            // we need to find the offset in the file based on the cookie's VA
            ulong sectionSize, sectionVA = 0;
            var ish = new SectionHeader();
            bool foundCookieSection = false;
            foreach (SectionHeader t in target.PE.PEHeaders.SectionHeaders)
            {
                sectionVA = (uint)t.VirtualAddress + baseAddress;
                sectionSize = (uint)t.VirtualSize;
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
                this.LogCouldNotLocateCookie(context);
                return false;
            }

            ulong fileCookieOffset = (cookieVA - baseAddress) - (sectionVA - baseAddress) + (uint)ish.PointerToRawData;
            SafePointer fileCookiePtr = loadConfigVA;
            fileCookiePtr.Address = (int)fileCookieOffset;

            SafePointer boundsCheck = fileCookiePtr + 8;
            if (!this.CookieOffsetValid(context, boundsCheck))
            {
                return false;
            }

            if (!boundsCheck.IsValid && target.PE.IsPacked)
            {
                this.LogInvalidCookieOffsetForKnownPackedFile(context);
                return false;
            }

            ulong cookie = BitConverter.ToUInt64(fileCookiePtr.GetBytes(8), 0);

            if (cookie != StackProtectionUtilities.DefaultCookieX64)
            {
                this.LogFailure(context, cookie.ToString("x"));
                return false;
            }
            return true;
        }

        private bool Validate32BitImage(BinaryAnalyzerContext context)
        {
            PEBinary target = context.PEBinary();
            PEHeader peHeader = target.PE.PEHeaders.PEHeader;
            var sp = new SafePointer(target.PE.ImageBytes, peHeader.LoadConfigTableDirectory.RelativeVirtualAddress);
            SafePointer loadConfigVA = target.PE.RVA2VA(sp);
            var loadConfig = new ImageLoadConfigDirectory32(peHeader, loadConfigVA);

            uint cookieVA = (uint)loadConfig.GetField(ImageLoadConfigDirectory32.Fields.SecurityCookie);
            uint baseAddress = (uint)peHeader.ImageBase;

            // we need to find the offset in the file based on the cookie's VA
            uint sectionSize, sectionVA = 0;
            var ish = new SectionHeader();
            bool foundCookieSection = false;
            foreach (SectionHeader t in target.PE.PEHeaders.SectionHeaders)
            {
                sectionVA = (uint)t.VirtualAddress + baseAddress;
                sectionSize = (uint)t.VirtualSize;
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
                this.LogCouldNotLocateCookie(context);
                return false;
            }

            ulong fileCookieOffset = (cookieVA - baseAddress) - (sectionVA - baseAddress) + (uint)ish.PointerToRawData;
            SafePointer fileCookiePtr = loadConfigVA;
            fileCookiePtr.Address = (int)fileCookieOffset;

            SafePointer boundsCheck = fileCookiePtr + 4;
            if (!this.CookieOffsetValid(context, boundsCheck))
            {
                return false;
            }

            uint cookie = BitConverter.ToUInt32(fileCookiePtr.GetBytes(4), 0);

            if (!StackProtectionUtilities.DefaultCookiesX86.Contains(cookie) && target.PE.Machine == Machine.I386)
            {
                this.LogFailure(context, cookie.ToString("x"));
                return false;
            }

            return true;
        }

        private bool CookieOffsetValid(BinaryAnalyzerContext context, SafePointer boundsCheck)
        {
            if (boundsCheck.IsValid) { return true; }

            PEBinary target = context.PEBinary();

            if (target.PE.IsPacked)
            {
                this.LogInvalidCookieOffsetForKnownPackedFile(context);
            }
            else
            {
                this.LogInvalidCookieOffset(context);
            }
            return false;
        }

        private void LogInvalidCookieOffset(BinaryAnalyzerContext context)
        {
            // The security cookie offset for '{0}' exceeds the size of the image.
            // The file may be corrupted or processed by an executable packer.
            // feature therefore could not be verified. The file was possibly packed by: {1}
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                    nameof(RuleResources.BA2012_Warning_InvalidSecurityCookieOffset),
                    context.TargetUri.GetFileName(),
                    context.PEBinary().PE.Packer.ToString()));
        }

        private void LogInvalidCookieOffsetForKnownPackedFile(BinaryAnalyzerContext context)
        {
            // '{0}' appears to be a packed C or C++ binary that reports a security cookie  
            // offset that exceeds the size of the packed file. Use of the stack protector (/GS)
            // feature therefore could not be verified. The file was possibly packed by: {1}
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Warning, context, null,
                    nameof(RuleResources.BA2012_Warning_InvalidSecurityCookieOffset),
                    context.TargetUri.GetFileName(),
                    context.PEBinary().PE.Packer.ToString()));
        }

        private void LogCouldNotLocateCookie(BinaryAnalyzerContext context)
        {
            // '{0}' is a C or C++ binary that enables the stack protection feature 
            // but the security cookie could not be located. The binary may be corrupted.
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                    nameof(RuleResources.BA2012_Error_CouldNotLocateCookie),
                    context.TargetUri.GetFileName()));
        }

        private void LogFailure(BinaryAnalyzerContext context, string cookie)
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
            context.Logger.Log(this,
                RuleUtilities.BuildResult(FailureLevel.Error, context, null,
                    nameof(RuleResources.BA2012_Error),
                    context.TargetUri.GetFileName(),
                    cookie));
        }
    }
}
