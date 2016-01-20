// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis.BinaryParsers.PortableExecutable;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver.Sdk;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRuleDescriptor))]
    public class SignSecurely : BinarySkimmerBase
    {
        /// <summary>
        /// BA2022
        /// </summary>
        public override string Id { get { return RuleIds.SignCorrectly; } }

        /// <summary>
        /// Images should be correctly signed using a cryptographically secure
        /// signature algorithm. This rule verifies a signed binary using the
        /// WinTrustVerify Authenticode policy provider. This check excludes 
        /// the certificate chain root (preventing execution across the 
        /// network). After retrieving the certificate chain information, the
        /// rule ensures that the binary was not signed with a SHA1 certificate
        /// (as SHA1 is currently deprecated by several companies including
        /// Microsoft and Google). Optionally, this rule can enforce that all
        /// binaries under analysis are actually signed (otherwise, analysis
        /// reports a 'not applicable' message for unsigned code). The check
        /// can also be configured to enforce that all analysis targets are
        /// signed with a recognized certificate (driven by a signature hash
        /// allow list).
        /// </summary>

        public override string FullDescription
        {
            get { return RuleResources.BA2022_SignCorrectly_Description; }
        }

        protected override IEnumerable<string> FormatSpecifierIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2022_Pass),
                    nameof(RuleResources.BA2022_Fail_Sha1Signature),
                    nameof(RuleResources.BA2022_Fail_VerifyActionFailed),
                    nameof(RuleResources.BA2022_Fail_WinTrustVerifyApiError),
                    nameof(RuleResources.BA2022_InvalidSignatureOrFileOpenError)};
            }
        }

        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            Native.CERT_INFO certInfo;
            Native.WINTRUST_DATA winTrustData;
            string filePath = context.PE.FileName;

            winTrustData = new Native.WINTRUST_DATA();

            try
            {
                if (InvokeVerifyAction(context, filePath, out winTrustData) &&
                    ValidateSignatureAlgorithm(context, winTrustData, out certInfo) &&
                    VerifyCodeIsSignedByApprovedCertificate(certInfo))
                {

                    // '{0}' appears to be signed securely by a trusted publisher with
                    // no verification or time stamp errors. Revocation checking was
                    // performed on the entire certificate chain, excluding the root
                    // certificate. The image was signed with '{1}', a
                    // cryptographically strong algorithm.
                    context.Logger.Log(this,
                        RuleUtilities.BuildResult(ResultKind.Pass, context, null,
                            nameof(RuleResources.BA2022_Pass)));
                }
            }
            finally
            {
                if (winTrustData.hWVTStateData != IntPtr.Zero)
                {
                    InvokeCloseAction(winTrustData);
                }
            }
        }

        private bool InvokeVerifyAction(BinaryAnalyzerContext context, string filePath, out Native.WINTRUST_DATA winTrustData)
        {
            Guid action;
            bool continueProcessing;
            ValidationResult validationResult;

            continueProcessing = true;
            winTrustData = InitializeWinTrustDataStruct(filePath);

            // First, we will invoke the basic verification. Note that currently this code path
            // does not reach across the network to perform its function. We could optionally
            // enable this (which would require altering the code that initializes our
            // WINTRUST_DATA instance).
            action = Native.ActionGenericVerifyV2;

            validationResult = WinVerifyTrustHelper(Native.INVALID_HANDLE_VALUE, ref winTrustData);

            switch (validationResult)
            {
                case ValidationResult.ERROR_SUCCESS:
                {
                    // Hash that represents the subject is trusted.
                    // Trusted publisher with no verification errors.
                    // No publisher or time stamp errors.
                    // This verification excludes root chain info.
                    break;
                }

                case ValidationResult.TRUST_E_NOSIGNATURE:
                {
                    validationResult = (ValidationResult)Marshal.GetLastWin32Error();

                    // File was not signed
                    if (validationResult == ValidationResult.TRUST_E_NOSIGNATURE ||
                        validationResult == ValidationResult.TRUST_E_PROVIDER_UNKNOWN ||
                        validationResult == ValidationResult.TRUST_E_SUBJECT_FORM_UNKNOWN)
                    {
                        Notes.LogNotApplicableToSpecifiedTarget(context, MetadataConditions.ImageIsNotSigned);
                    }
                    else
                    {

                        // The signature of '{0}' was invalid or there was an error opening the file.
                        context.Logger.Log(this, RuleUtilities.BuildResult(ResultKind.Error, context, null,
                            nameof(RuleResources.BA2022_InvalidSignatureOrFileOpenError)));

                    }
                    continueProcessing = false;
                    break;
                }

                default:
                {
                    // '{0}' signing verification failed with WinTrustVerify error: '{1}'
                    context.Logger.Log(this, RuleUtilities.BuildResult(ResultKind.Error, context, null,
                        nameof(RuleResources.BA2022_Fail_VerifyActionFailed),
                        validationResult.ToString()));
                    break;
                }
            }

            return continueProcessing;
        }

        private bool ValidateSignatureAlgorithm(BinaryAnalyzerContext context, Native.WINTRUST_DATA winTrustData, out Native.CERT_INFO certInfo)
        {
            string failedApiName;
            bool continueProcessing;
            ValidationResult validationResult;

            continueProcessing = false;
            validationResult = GetCertInfo(winTrustData.hWVTStateData, out certInfo, out failedApiName);

            if (validationResult != 0)
            {
                // '{0}' signing could not be completely verified because
                // '{1}' failed with error code: '{2}'.
                context.Logger.Log(this, RuleUtilities.BuildResult(ResultKind.Error, context, null,
                    nameof(RuleResources.BA2022_Fail_WinTrustVerifyApiError),
                    failedApiName,
                    validationResult.ToString()));
                return continueProcessing;
            }

            string algorithmId;
            bool hasWeakSignatureAlgorithm = IsWeakSignatureAlgorithm(certInfo.SignatureAlgorithm.pszObjId, out algorithmId);

            if (hasWeakSignatureAlgorithm)
            {
                // '{0}' is signed with a weak cryptographic algorithm '{1}'. '{1}' is
                // or is shortly expected to be vulnerable to collision attacks. Sign 
                // this binary with a stronger cryptographic algorithm such as SHA256.
                context.Logger.Log(this, RuleUtilities.BuildResult(ResultKind.Error, context, null,
                    nameof(RuleResources.BA2022_Fail_Sha1Signature),
                    algorithmId));
            }

            continueProcessing = !hasWeakSignatureAlgorithm;
            return continueProcessing;
        }

        private bool IsWeakSignatureAlgorithm(string pszObjId, out string algorithmId)
        {
            algorithmId = null;

            AlgorithmData algorithmData;
            if (!s_algorithmData.TryGetValue(pszObjId, out algorithmData))
            {
                // NOTE: this rule by design currently raises an exception on encountering an 
                // unrecognized algorithm id. This will have the effect of shutting down this rule
                // entirely when the engine catches the exception (resulting in an internal error). 
                // This should accelerate the process of uncovering new ids. Alternately, we could
                // burn the ids into rule configuration (which would allow users to produce a new
                // configuration file that extends this data).
                throw new ArgumentException("Unrecognized algorithm id: " + pszObjId, nameof(pszObjId));
            }

            algorithmId = algorithmData.Name;
            return algorithmData.Weak;
        }

        private bool VerifyCodeIsSignedByApprovedCertificate(Native.CERT_INFO certInfo)
        {
            // TODO. This code could to ensure that retrieved certificate matches
            // against an allow list. This functionality will require adding
            // appropriate options to this rule
#if NOT_DEFINED
            // #define SHA256_HASH_LEN 32
            uint cbKeyId = 32;
            byte[] rgbKeyId = new byte[cbKeyId];
            string key;

            if (!Native.CryptHashPublicKeyInfo(
                IntPtr.Zero,
                Native.CALG_SHA_256,
                0,
                Native.X509_ASN_ENCODING,
                ref certInfo.SubjectPublicKeyInfo,
                rgbKeyId,
                ref cbKeyId
                ))
            {
                validationResult = (ValidationResult)Marshal.GetLastWin32Error();
                return;
            }

            // Strip hyphens to match output as provided by tools such as certutil.exe 
            key = BitConverter.ToString(rgbKeyId).Replace('-', ' ');

            // This call to HashSet.Contains is case-insensitive due to initializing the hashset with an appropriate comparer
            validationResult = (trustedKeys.Contains(key) ? ValidationResult.Valid : ValidationResult.UnknownCertificateKey);
#endif
            return true;
        }

        private void InvokeCloseAction(Native.WINTRUST_DATA winTrustData)
        {
            IntPtr pWinTrustData = IntPtr.Zero;
            Guid action = Native.ActionGenericVerifyV2;
            winTrustData.StateAction = Native.StateAction.WTD_STATEACTION_CLOSE;

            try
            {
                pWinTrustData = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.WINTRUST_DATA)));
                Marshal.StructureToPtr(winTrustData, pWinTrustData, false);

                Native.WinVerifyTrust(Native.INVALID_HANDLE_VALUE, ref action, pWinTrustData);
            }
            finally
            {
                if (pWinTrustData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pWinTrustData);
                }
                Debug.Assert(winTrustData.File == IntPtr.Zero);
            }
        }

        private ValidationResult WinVerifyTrustHelper(IntPtr handle, ref Native.WINTRUST_DATA winTrustData)
        {
            Guid action = Native.ActionGenericVerifyV2;

            ValidationResult validationResult;
            IntPtr pWinTrustData = IntPtr.Zero;

            try
            {
                pWinTrustData = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.WINTRUST_DATA)));
                Marshal.StructureToPtr(winTrustData, pWinTrustData, false);

                validationResult = (ValidationResult)Native.WinVerifyTrust(handle, ref action, pWinTrustData);

                winTrustData = (Native.WINTRUST_DATA)Marshal.PtrToStructure(pWinTrustData, typeof(Native.WINTRUST_DATA));
            }
            finally
            {
                if (pWinTrustData != IntPtr.Zero)
                { 
                    Marshal.FreeHGlobal(pWinTrustData);
                }

                if (winTrustData.File != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(winTrustData.File);
                    winTrustData.File = IntPtr.Zero;
                }
            }
            return validationResult;
        }

        private static Dictionary<string, AlgorithmData> s_algorithmData = BuildAlgorithmData();

        private static Dictionary<string, AlgorithmData> BuildAlgorithmData()
        {
            var result = new Dictionary<string, AlgorithmData>
            {
                // PRESUMED weak

                { "1.2.840.113549.1.1.2", new AlgorithmData() { Name = "md2RSA",    Weak = true }},
                { "1.2.840.113549.1.1.4", new AlgorithmData() { Name = "md5RSA",    Weak = true }},
                { "1.2.840.113549.2.5",   new AlgorithmData() { Name = "md5NoSign", Weak = true }},
                { "1.3.14.3.2.2",         new AlgorithmData() { Name = "md4RSA",    Weak = true }},
                { "1.3.14.3.2.3",         new AlgorithmData() { Name = "md5RSA",    Weak = true }},
                { "1.3.14.7.2.3.1",       new AlgorithmData() { Name = "md2RSA",    Weak = true }},
                { "1.3.14.3.2.4",         new AlgorithmData() { Name = "md4RSA",    Weak = true }},

                { "1.2.840.10040.4.3",    new AlgorithmData() { Name = "sha1DSA",    Weak = true }},
                { "1.2.840.10045.4.1",    new AlgorithmData() { Name = "sha1ECDSA",  Weak = true }},
                { "1.2.840.113549.1.1.3", new AlgorithmData() { Name = "md4RSA",     Weak = true }},
                { "1.2.840.113549.1.1.5", new AlgorithmData() { Name = "sha1RSA",    Weak = true }},
                { "1.3.14.3.2.15",        new AlgorithmData() { Name = "shaRSA",     Weak = true }},
                { "1.3.14.3.2.13",        new AlgorithmData() { Name = "sha1DSA",    Weak = true }},
                { "1.3.14.3.2.26",        new AlgorithmData() { Name = "sha1NoSign", Weak = true }},
                { "1.3.14.3.2.27",        new AlgorithmData() { Name = "dsaSHA1",    Weak = true }},
                { "1.3.14.3.2.29",        new AlgorithmData() { Name = "sha1RSA",    Weak = true }},

                // Are these weak or broken algorithms? shut down results related to them until we know
                { "1.2.840.10045.4.3",       new AlgorithmData() { Name = "specifiedECDSA",   Weak = false }},
                { "1.2.840.113549.1.1.10",   new AlgorithmData() { Name = "RSASSA - PSS",     Weak = false }},
                { "2.16.840.1.101.2.1.1.19", new AlgorithmData() { Name = "mosaicUpdatedSig", Weak = false }},

                // PRESUMED strong but what about this NoSign designation?
                { "1.2.840.10045.4.3.2",    new AlgorithmData() { Name = "sha256ECDSA",  Weak = false }},
                { "1.2.840.10045.4.3.3",    new AlgorithmData() { Name = "sha384ECDSA",  Weak = false }},
                { "1.2.840.10045.4.3.4",    new AlgorithmData() { Name = "sha512ECDSA",  Weak = false }},
                { "1.2.840.113549.1.1.11",  new AlgorithmData() { Name = "sha256RSA",    Weak = false }},
                { "1.2.840.113549.1.1.12",  new AlgorithmData() { Name = "sha384RSA",    Weak = false }},
                { "1.2.840.113549.1.1.13",  new AlgorithmData() { Name = "sha512RSA",    Weak = false }},
                { "2.16.840.1.101.3.4.2.1", new AlgorithmData() { Name = "sha256NoSign", Weak = false }},
                { "2.16.840.1.101.3.4.2.2", new AlgorithmData() { Name = "sha384NoSign", Weak = false }},
                { "2.16.840.1.101.3.4.2.3", new AlgorithmData() { Name = "sha512NoSign", Weak = false }},
            };

            return result;
        }

        struct AlgorithmData
        {
            public string Name;
            public bool Weak;
        }

        private ValidationResult GetCertInfo(IntPtr hWVTStateData, out Native.CERT_INFO certInfo, out string failedApiName)
        {
            failedApiName = null;
            certInfo = new Native.CERT_INFO();

            IntPtr providerData = Native.WTHelperProvDataFromStateData(hWVTStateData);
            if (providerData == IntPtr.Zero)
            {
                failedApiName = "WTHelperProvDataFromStateData";
                return (ValidationResult)Marshal.GetLastWin32Error();
            }

            IntPtr providerSigner = Native.WTHelperGetProvSignerFromChain(providerData, 0, false, 0);
            if (providerSigner == IntPtr.Zero)
            {
                failedApiName = "WTHelperGetProvSignerFromChain";
                return (ValidationResult)Marshal.GetLastWin32Error();
            }

            var cryptProviderSigner = (Native.CRYPT_PROVIDER_SGNR)Marshal.PtrToStructure(providerSigner, typeof(Native.CRYPT_PROVIDER_SGNR));
            var chainContext = (Native.CERT_CHAIN_CONTEXT)Marshal.PtrToStructure(cryptProviderSigner.pChainContext, typeof(Native.CERT_CHAIN_CONTEXT));

            IntPtr[] simpleChains = new IntPtr[chainContext.cChain];
            Marshal.Copy(chainContext.rgpChain, simpleChains, 0, simpleChains.Length);
            var certChain = (Native.CERT_SIMPLE_CHAIN)Marshal.PtrToStructure(simpleChains[0], typeof(Native.CERT_SIMPLE_CHAIN));

            IntPtr[] chainElements = new IntPtr[certChain.cElement];
            Marshal.Copy(certChain.rgpElement, chainElements, 0, chainElements.Length);
            var certElement = (Native.CERT_CHAIN_ELEMENT)Marshal.PtrToStructure(chainElements[0], typeof(Native.CERT_CHAIN_ELEMENT));

            var certContext = (Native.CERT_CONTEXT)Marshal.PtrToStructure(certElement.pCertContext, typeof(Native.CERT_CONTEXT));
            certInfo = (Native.CERT_INFO)Marshal.PtrToStructure(certContext.pCertInfo, typeof(Native.CERT_INFO));

            return ValidationResult.ERROR_SUCCESS;
        }

        private Native.WINTRUST_DATA InitializeWinTrustDataStruct(string filePath)
        {
            // See https://msdn.microsoft.com/en-us/library/windows/desktop/aa382384(v=vs.85).aspx
            // which was used to drive data initialization, API use and comments in this code.

            var winTrustData = new Native.WINTRUST_DATA();

            var fileInfo = new Native.WINTRUST_FILE_INFO();
            fileInfo.pcwszFilePath = filePath;   // Path to the signed file
            fileInfo.cbStruct = (uint)Marshal.SizeOf(typeof(Native.WINTRUST_FILE_INFO));

            winTrustData.cbStruct = (uint)Marshal.SizeOf(typeof(Native.WINTRUST_DATA));

            winTrustData.pPolicyCallbackData = IntPtr.Zero;                                           // Use default code signing EKU
            winTrustData.pSIPClientData = IntPtr.Zero;                                                // No data to pass to SIP
            winTrustData.UIChoice = Native.UIChoice.WTD_UI_NONE;                                      // Disable all UI on execution
            winTrustData.UIContext = 0;
            winTrustData.UIContext = Native.UIContext.WTD_UICONTEXT_EXECUTE;                          // File is intended to be executed
            winTrustData.UnionChoice = Native.UnionChoice.WTD_CHOICE_FILE;                            // We're verifying a file
            winTrustData.RevocationChecks = Native.RevocationChecks.WTD_REVOKE_WHOLECHAIN;            // Revocation checking on whole chain.
            winTrustData.dwProvFlags = Native.ProviderFlags.WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT;  // Don't reach across the network
            winTrustData.dwProvFlags |= Native.ProviderFlags.WTD_CACHE_ONLY_URL_RETRIEVAL;
            winTrustData.pwszURLReference = null;                                                     // Reserved for future use

            winTrustData.StateAction = Native.StateAction.WTD_STATEACTION_VERIFY;
            winTrustData.hWVTStateData = IntPtr.Zero; // This value set by API call

            winTrustData.File = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.WINTRUST_FILE_INFO)));
            Marshal.StructureToPtr(fileInfo, winTrustData.File, false);

            return winTrustData;
        }
    }

    /// <summary>
    /// This class defines a range of execution conditions that identify various results
    /// in code, both for primary function of application and negative conditions
    /// </summary>
    public enum ValidationResult : uint
    {
        // Possible DOS/Win32 error codes
        ERROR_SUCCESS = 0,
        ERROR_MORE_DATA = 234, // e.g., insufficient buffer storage provided for key extraction

        // HRESULT error codes
        CRYPT_E_FILE_ERROR = 0x80092003,
        TRUST_E_NOSIGNATURE = 0x800B0100,
        TRUST_E_PROVIDER_UNKNOWN = 0x800B0001,
        TRUST_E_SUBJECT_FORM_UNKNOWN = 0x800B0003,
        TRUST_E_SUBJECT_NOT_TRUSTED = 0x800B0004,
        TRUST_E_FAIL = 0x800B010B,
        TRUST_E_EXPLICIT_DISTRUST = 0x800B0111,
        CERT_E_EXPIRED = 0x800B0101,
        CERT_E_VALIDITYPERIODNESTING = 0x800B0102,
        CERT_E_ROLE = 0x800B0103,
        CERT_E_PATHLENCONST = 0x800B0104,
        CERT_E_CRITICAL = 0x800B0105,
        CERT_E_PURPOSE = 0x800B0106,
        CERT_E_ISSUERCHAINING = 0x800B0107,
        CERT_E_MALFORMED = 0x800B0108,
        CERT_E_UNTRUSTEDROOT = 0x800B0109,
        CERT_E_CHAINING = 0x800B010A,
        CERT_E_REVOKED = 0x800B010C,
        CERT_E_UNTRUSTEDTESTROOT = 0x800B010D,
        CERT_E_REVOCATION_FAILURE = 0x800B010E,
        CERT_E_CN_NO_MATCH = 0x800B010F,
        CERT_E_WRONG_USAGE = 0x800B0110,
        CERT_E_UNTRUSTEDCA = 0x800B0112,
        CERT_E_INVALID_POLICY = 0x800B0113,
        CERT_E_INVALID_NAME = 0x800B0114,
        CRYPT_E_SECURITY_SETTINGS = 0x80092026
    }
}
