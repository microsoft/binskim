// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis.IL.Sdk;
using Microsoft.CodeAnalysis.Sarif;
using Microsoft.CodeAnalysis.Sarif.Driver;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    [Export(typeof(ISkimmer<BinaryAnalyzerContext>)), Export(typeof(IRule))]
    public class SignSecurely : BinarySkimmerBase
    {
        /// <summary>
        /// BA2022
        /// </summary>
        public override string Id { get { return RuleIds.SignCorrectly; } }

        /// <summary>
        /// Images should be correctly signed by trusted publishers using 
        /// cryptographically secure signature algorithms. This rule 
        /// invokes WinTrustVerify to validate that binary hash, signing
        /// and public key algorithms are secure and, where configurable,
        /// that key sizes meet acceptable size thresholds.
        /// </summary>

        public override string FullDescription
        {
            get { return RuleResources.BA2022_SignCorrectly_Description; }
        }

        protected override IEnumerable<string> FormatIds
        {
            get
            {
                return new string[] {
                    nameof(RuleResources.BA2022_Pass),
                    nameof(RuleResources.BA2022_Error_BadSigningAlgorithm),
                    nameof(RuleResources.BA2022_Error_DidNotVerify),
                    nameof(RuleResources.BA2022_Error_WinTrustVerifyApiError),
                    nameof(RuleResources.NotApplicable_InvalidMetadata)
                };
            }
        }

        public override AnalysisApplicability CanAnalyze(BinaryAnalyzerContext context, out string reasonForNotAnalyzing)
        {
            reasonForNotAnalyzing = null;
            return AnalysisApplicability.ApplicableToSpecifiedTarget;
        }

        public override void Analyze(BinaryAnalyzerContext context)
        {
            Native.WINTRUST_DATA winTrustData;
            string algorithmName, filePath;

            filePath = context.PE.FileName;

            winTrustData = new Native.WINTRUST_DATA();

            if (InvokeVerifyAction(context, filePath, out winTrustData, out algorithmName))
            {
                // '{0}' appears to be signed securely by a trusted publisher with no 
                // verification or time stamp errors. Revocation checking was performed
                // on the entire certificate chain, excluding the root certificate. 
                // The following digitial signature algorithms were detected: {1}
                context.Logger.Log(this,
                    RuleUtilities.BuildResult(ResultLevel.Pass, context, null,
                        nameof(RuleResources.BA2022_Pass),
                        context.TargetUri.GetFileName(),
                        algorithmName));
            }
        }

        private bool InvokeVerifyAction(
            BinaryAnalyzerContext context,
            string filePath,
            out Native.WINTRUST_DATA winTrustData,
            out string algorithmsText)
        {
            Guid action;
            CryptoError cryptoError;
            var badAlgorithms  = new List<Tuple<string, string>>();
            var goodAlgorithms = new List<Tuple<string, string>>();

            algorithmsText = null;
            action = Native.ActionGenericVerifyV2;

            uint signatureCount = 1;

            // First, we retrieve the signature count
            winTrustData = InitializeWinTrustDataStruct(filePath, WinTrustDataKind.SignatureCount);
            Native.WinVerifyTrust(Native.INVALID_HANDLE_VALUE, ref action, ref winTrustData);

            if (winTrustData.pSignatureSettings != IntPtr.Zero)
            {
                var signatureSettings = Marshal.PtrToStructure<Native.WINTRUST_SIGNATURE_SETTINGS>(winTrustData.pSignatureSettings);
                signatureCount = signatureSettings.cSecondarySigs + 1; // Total count primary + cSecondary
            }

            InvokeCloseAction(winTrustData);

            // First, we will invoke the basic verification on all returned
            // signatures. Note that currently this code path does not reach across
            // the network to perform its function. We could optionally
            // enable this (which would require altering the code that initializes
            // our WINTRUST_DATA instance).

            for (uint i = 0; i < signatureCount; i++)
            {
                string hashAlgorithm, hashEncryptionAlgorithm;
                winTrustData = InitializeWinTrustDataStruct(filePath, WinTrustDataKind.EnforcePolicy, i);

                cryptoError = (CryptoError)Native.WinVerifyTrust(Native.INVALID_HANDLE_VALUE, ref action, ref winTrustData);

                switch (cryptoError)
                {
                    // The SignSecurely check mostly validates signing algorithm strength. The
                    // error conditions are expected in some scan contexts, for example, an 
                    // isolated build environment which hasn't been configured to trust the
                    // signing root. Providing a more complex signing validation would require
                    // BinSkim to be significantly more configurable to provide information on
                    // the scan environment as well as the scan targets.
                    case CryptoError.CERT_E_UNTRUSTEDROOT:
                    case CryptoError.CERT_E_CHAINING:
                    case CryptoError.ERROR_SUCCESS:
                    {
                        // Hash that represents the subject is trusted.
                        // Trusted publisher with no verification errors.
                        // No publisher or time stamp errors.
                        // This verification excludes root chain info.
                        if (GetSignerHashAlgorithms(context, winTrustData, out hashAlgorithm, out hashEncryptionAlgorithm))
                        {
                            goodAlgorithms.Add(new Tuple<string, string>(hashAlgorithm, hashEncryptionAlgorithm));
                        }

                        InvokeCloseAction(winTrustData);
                        break;
                    }

                    case CryptoError.NTE_BAD_ALGID:
                    {
                        InvokeCloseAction(winTrustData);

                        // We cannot retrieve algorithm id and cert info for images that fail
                        // the stringent WinTrustVerify security check. We therefore start
                        // a new call chain with looser validation criteria.
                        winTrustData = InitializeWinTrustDataStruct(filePath, WinTrustDataKind.Normal);
                        Native.WinVerifyTrust(Native.INVALID_HANDLE_VALUE, ref action, ref winTrustData);

                        if (GetSignerHashAlgorithms(context, winTrustData, out hashAlgorithm, out hashEncryptionAlgorithm))
                        {
                            badAlgorithms.Add(new Tuple<string, string>(hashAlgorithm, hashEncryptionAlgorithm));
                        }

                        InvokeCloseAction(winTrustData);
                        break;
                    }

                    case CryptoError.TRUST_E_NOSIGNATURE:
                    {
                        Notes.LogNotApplicableToSpecifiedTarget(context, MetadataConditions.ImageIsNotSigned);
                        return false;
                    }

                    default:
                    {
                        string cryptoErrorDescription = cryptoError.GetErrorDescription();
                        // '{0}' signing was flagged as insecure by WinTrustVerify with error code: '{1}' ({2})
                        context.Logger.Log(this, RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                            nameof(RuleResources.BA2022_Error_DidNotVerify),
                            context.TargetUri.GetFileName(),                        
                            cryptoError.ToString(),
                            cryptoErrorDescription));
                        InvokeCloseAction(winTrustData);
                        return false;
                    }
                }
            }

            algorithmsText = BuildAlgorithmsText(badAlgorithms, goodAlgorithms);

            if (goodAlgorithms.Count == 0)
            {
                // '{0}' was signed exclusively with algorithms that WinTrustVerify has flagged as insecure. {1}
                context.Logger.Log(this, RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                    nameof(RuleResources.BA2022_Error_BadSigningAlgorithm),
                    context.TargetUri.GetFileName(),
                    algorithmsText));
            }

            return goodAlgorithms.Count > 0;
        }

        private string BuildAlgorithmsText(List<Tuple<string, string>> badAlgorithms, List<Tuple<string, string>> goodAlgorithms)
        {
            int count;
            var sb = new StringBuilder();

            if (badAlgorithms.Count > 0)
            {
                count = 0;
                sb.Append("Cryptographically weak signatures: ");

                foreach(Tuple<string, string> tuple in badAlgorithms)
                {
                    sb.Append((count > 0 ? "," : String.Empty) + "[digest algorithm: '" + tuple.Item1 + "' + digest encryption algorithm: '" + tuple.Item2 + "']");
                    count++;
                }
            }

            if (goodAlgorithms.Count > 0)
            {
                count = 0;
                sb.Append((sb.Length > 0 ? " " : String.Empty) + "Cryptographically strong signatures: ");

                foreach (Tuple<string, string> tuple in goodAlgorithms)
                {
                    sb.Append((count > 0 ? "," : String.Empty) + "[digest algorithm: '" + tuple.Item1 + "' + digest encryption algorithm: '" + tuple.Item2 + "']");
                    count++;
                }
            }
            return sb.ToString();
        }

        private bool GetSignerHashAlgorithms(
            BinaryAnalyzerContext context, 
            Native.WINTRUST_DATA winTrustData,
            out string hashAlgorithm,
            out string hashEncryptionAlgorithm)
        {
            string failedApiName;
            CryptoError cryptoError;

            cryptoError = GetSignerHashAlgorithms(
                winTrustData.hWVTStateData,
                out hashAlgorithm, 
                out hashEncryptionAlgorithm, 
                out failedApiName);

            if (cryptoError != CryptoError.ERROR_SUCCESS)
            {
                // '{0}' signing could not be completely verified because
                // '{1}' failed with error code: '{2}'.
                context.Logger.Log(this, RuleUtilities.BuildResult(ResultLevel.Error, context, null,
                    nameof(RuleResources.BA2022_Error_WinTrustVerifyApiError),
                    context.TargetUri.GetFileName(),
                    failedApiName,
                    cryptoError.ToString()));
                return false;
            }

            return true;
        }

        private string GetAlgorithmName(string pszObjId)
        {
            string algorithmName;
            if (!s_idToAlgorithmMap.TryGetValue(pszObjId, out algorithmName))
            {
                // NOTE: this rule by design currently raises an exception on encountering an 
                // unrecognized algorithm id. This will have the effect of shutting down this rule
                // entirely when the engine catches the exception (resulting in an internal error). 
                // This should accelerate the process of uncovering new ids. Alternately, we could
                // burn the ids into rule configuration (which would allow users to produce a new
                // configuration file that extends this data).
                throw new ArgumentException("Unrecognized algorithm id: " + pszObjId, nameof(pszObjId));
            }
            return algorithmName;
        }

        private void InvokeCloseAction(Native.WINTRUST_DATA winTrustData)
        {
            Guid action = Native.ActionGenericVerifyV2;
            winTrustData.StateAction = Native.StateAction.WTD_STATEACTION_CLOSE;
            Native.WinVerifyTrust(Native.INVALID_HANDLE_VALUE, ref action, ref winTrustData);

            if (winTrustData.pFile != IntPtr.Zero)
            {
                Marshal.DestroyStructure(winTrustData.pFile, typeof(Native.WINTRUST_FILE_INFO));
                Marshal.FreeHGlobal(winTrustData.pFile);
            }

            if (winTrustData.pSignatureSettings != IntPtr.Zero)
            {
                var signatureSettings = Marshal.PtrToStructure<Native.WINTRUST_SIGNATURE_SETTINGS>(winTrustData.pSignatureSettings);

                if (signatureSettings.pCryptoPolicy != IntPtr.Zero)
                {
                    Marshal.DestroyStructure(signatureSettings.pCryptoPolicy, typeof(Native.CERT_STRONG_SIGN_PARA));
                    Marshal.FreeHGlobal(signatureSettings.pCryptoPolicy);
                }

                Marshal.DestroyStructure(winTrustData.pSignatureSettings, typeof(Native.WINTRUST_SIGNATURE_SETTINGS));
                Marshal.FreeHGlobal(winTrustData.pSignatureSettings);
            }
        }

        private static Dictionary<string, string> s_idToAlgorithmMap = BuildIdToAlgorithmMap();

        private static Dictionary<string, string> BuildIdToAlgorithmMap()
        {
            var result = new Dictionary<string, string>
            {
                { "1.2.840.113549.1.1.1",    "RSA" },
                { "1.2.840.113549.1.1.2",    "md2RSA"},
                { "1.3.14.7.2.3.1",          "md2RSA"},
                { "1.3.14.3.2.4",            "md4RSA"},
                { "1.2.840.113549.1.1.3",    "md4RSA"},
                { "1.3.14.3.2.2",            "md4RSA"},
                { "1.2.840.113549.1.1.4",    "md5RSA"},
                { "1.2.840.113549.2.5",      "md5NoSign"},
                { "1.3.14.3.2.3",            "md5RSA"},
                { "2.16.840.1.101.2.1.1.19", "mosaicUpdatedSig"},
                // This algorithm is a padding method, not a signing algorithm. 
                { "1.2.840.113549.1.1.10",   "RSASSA - PSS"},
                { "1.2.840.10040.4.3",       "sha1DSA"},
                { "1.2.840.10045.4.1",       "sha1ECDSA"},
                { "1.2.840.113549.1.1.5",    "sha1RSA"},
                { "1.3.14.3.2.15",           "shaRSA"},
                { "1.3.14.3.2.13",           "sha1DSA"},
                { "1.3.14.3.2.26",           "sha1NoSign"},
                { "1.3.14.3.2.27",           "dsaSHA1"},
                { "1.3.14.3.2.29",           "sha1RSA"},
                { "1.2.840.10045.4.3.2",     "sha256ECDSA"},
                { "1.2.840.10045.4.3.3",     "sha384ECDSA"},
                { "1.2.840.10045.4.3.4",     "sha512ECDSA"},
                { "1.2.840.113549.1.1.11",   "sha256RSA"},
                { "1.2.840.113549.1.1.12",   "sha384RSA"},
                { "1.2.840.113549.1.1.13",   "sha512RSA"},
                { "2.16.840.1.101.3.4.2.1",  "sha256NoSign"},
                { "2.16.840.1.101.3.4.2.2",  "sha384NoSign"},
                { "2.16.840.1.101.3.4.2.3",  "sha512NoSign"},
                // ECDSA is weak or strong depending on the key size
                { "1.2.840.10045.4.3",       "specifiedECDSA"},
            };

            return result;
        }

        private CryptoError GetSignerHashAlgorithms(IntPtr hWVTStateData, out string hashAlgorithm, out string hashEncryptionAlgorithm, out string failedApiName)
        {
            hashAlgorithm = hashEncryptionAlgorithm = null;

            failedApiName = "WTHelperProvDataFromStateData";
            IntPtr providerData = Native.WTHelperProvDataFromStateData(hWVTStateData);
            if (providerData == IntPtr.Zero)
            {
                return (CryptoError)Marshal.GetLastWin32Error();
            }

            failedApiName = "WTHelperGetProvSignerFromChain";
            IntPtr providerSigner = Native.WTHelperGetProvSignerFromChain(providerData, 0, false, 0);

            if (providerSigner == IntPtr.Zero)
            {
                return (CryptoError)Marshal.GetLastWin32Error();
            }

            var cryptProviderSigner = (Native.CRYPT_PROVIDER_SGNR)Marshal.PtrToStructure(providerSigner, typeof(Native.CRYPT_PROVIDER_SGNR));

            var cryptoError = (CryptoError)cryptProviderSigner.dwError;

            if (cryptProviderSigner.psSigner != IntPtr.Zero)
            {
                var psSigner = (Native.CMSG_SIGNER_INFO)Marshal.PtrToStructure(cryptProviderSigner.psSigner, typeof(Native.CMSG_SIGNER_INFO));
                hashAlgorithm = GetAlgorithmName(psSigner.HashAlgorithm.pszObjId);
                hashEncryptionAlgorithm = GetAlgorithmName(psSigner.HashEncryptionAlgorithm.pszObjId);
            }

            if (cryptoError == CryptoError.ERROR_SUCCESS)
            {
                failedApiName = null;
            }

            return cryptoError;
        }

        private Native.WINTRUST_DATA InitializeWinTrustDataStruct(string filePath, WinTrustDataKind kind, uint signatureIndex = 0)
        {
            // See https://msdn.microsoft.com/en-us/library/windows/desktop/aa382384(v=vs.85).aspx
            // which was used to drive data initialization, API use and comments in this code.

            var winTrustData = new Native.WINTRUST_DATA();
            winTrustData.cbStruct = (uint)Marshal.SizeOf(typeof(Native.WINTRUST_DATA));

            var fileInfo = new Native.WINTRUST_FILE_INFO();
            fileInfo.cbStruct = (uint)Marshal.SizeOf(typeof(Native.WINTRUST_FILE_INFO));
            fileInfo.pcwszFilePath = filePath;

            winTrustData.pFile = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.WINTRUST_FILE_INFO)));
            Marshal.StructureToPtr(fileInfo, winTrustData.pFile, false);

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

            if (kind != WinTrustDataKind.Normal)
            {
                Native.SignatureSettingsFlags flags = Native.SignatureSettingsFlags.WSS_GET_SECONDARY_SIG_COUNT;

                if (kind == WinTrustDataKind.EnforcePolicy)
                {
                    flags = Native.SignatureSettingsFlags.WSS_VERIFY_SPECIFIC;
                }

                var signatureSettings = new Native.WINTRUST_SIGNATURE_SETTINGS();
                signatureSettings.cbStruct = (uint)Marshal.SizeOf(typeof(Native.WINTRUST_SIGNATURE_SETTINGS));
                signatureSettings.dwIndex = signatureIndex;
                signatureSettings.dwFlags = flags;
                signatureSettings.cSecondarySigs = 0;
                signatureSettings.dwVerifiedSigIndex = 0;

                var policy = new Native.CERT_STRONG_SIGN_PARA();
                policy.cbStruct = (uint)Marshal.SizeOf(typeof(Native.CERT_STRONG_SIGN_PARA));
                policy.dwInfoChoice = Native.InfoChoice.CERT_STRONG_SIGN_OID_INFO_CHOICE;
                policy.pszOID = Native.szOID_CERT_STRONG_SIGN_OS_1;

                signatureSettings.pCryptoPolicy = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.CERT_STRONG_SIGN_PARA)));
                Marshal.StructureToPtr(policy, signatureSettings.pCryptoPolicy, false);

                winTrustData.pSignatureSettings = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.WINTRUST_SIGNATURE_SETTINGS)));
                Marshal.StructureToPtr(signatureSettings, winTrustData.pSignatureSettings, false);
            }

            return winTrustData;
        }

        private enum WinTrustDataKind
        {
            Normal,
            SignatureCount,
            EnforcePolicy
        }
    }
}
