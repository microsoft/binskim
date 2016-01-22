// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public static class RulesExtensionMethods
    {
        private static Dictionary<CryptoError, string> s_cryptoErrorToDescriptionMap = BuildCryptoErrorDescriptions();

        private static Dictionary<CryptoError, string> BuildCryptoErrorDescriptions()
        {
            var result = new Dictionary<CryptoError, string>()
            {
                { CryptoError.ERROR_SUCCESS, "Call succeeded." },
                { CryptoError.CRYPT_E_ASN1_INTERNAL, "ASN1 internal encode or decode error." },
                { CryptoError.CRYPT_E_ASN1_EOD, "ASN1 unexpected end of data." },
                { CryptoError.CRYPT_E_ASN1_CORRUPT, "ASN1 corrupted data." },
                { CryptoError.CRYPT_E_ASN1_LARGE, "ASN1 value too large." },
                { CryptoError.CRYPT_E_ASN1_CONSTRAINT, "ASN1 constraint violated." },
                { CryptoError.CRYPT_E_ASN1_MEMORY, "ASN1 out of memory." },
                { CryptoError.CRYPT_E_ASN1_OVERFLOW, "ASN1 buffer overflow." },
                { CryptoError.CRYPT_E_ASN1_BADPDU, "ASN1 function not supported for this PDU." },
                { CryptoError.CRYPT_E_ASN1_BADARGS, "ASN1 bad arguments to function call." },
                { CryptoError.CRYPT_E_ASN1_BADREAL, "ASN1 bad real value." },
                { CryptoError.CRYPT_E_ASN1_BADTAG, "ASN1 bad tag value met." },
                { CryptoError.CRYPT_E_ASN1_CHOICE, "ASN1 bad choice value." },
                { CryptoError.CRYPT_E_ASN1_RULE, "ASN1 bad encoding rule." },
                { CryptoError.CRYPT_E_ASN1_UTF8, "ASN1 bad unicode (UTF8)." },
                { CryptoError.CRYPT_E_ASN1_PDU_TYPE, "ASN1 bad PDU type." },
                { CryptoError.CRYPT_E_ASN1_NYI, "ASN1 not yet implemented." },
                { CryptoError.CRYPT_E_ASN1_EXTENDED, "ASN1 skipped unknown extension(s)." },
                { CryptoError.CRYPT_E_ASN1_NOEOD, "ASN1 end of data expected." },  
                { CryptoError.CERTSRV_E_BAD_REQUESTSUBJECT, "The request subject name is invalid or too long." },
                { CryptoError.CERTSRV_E_NO_REQUEST, "The request does not exist." },
                { CryptoError.CERTSRV_E_BAD_REQUESTSTATUS, "The request's current status does not allow this operation." },
                { CryptoError.CERTSRV_E_PROPERTY_EMPTY, "The requested property value is empty." },
                { CryptoError.CERTSRV_E_INVALID_CA_CERTIFICATE, "The certification authority's certificate contains invalid data." },
                { CryptoError.CERTSRV_E_SERVER_SUSPENDED, "Certificate service has been suspended for a database restore operation." },
                { CryptoError.CERTSRV_E_ENCODING_LENGTH, "The certificate contains an encoded length that is potentially incompatible with older enrollment software." },
                { CryptoError.CERTSRV_E_UNSUPPORTED_CERT_TYPE, "The requested certificate template is not supported by this CA." },
                { CryptoError.CERTSRV_E_NO_CERT_TYPE, "The request contains no certificate template information." },
                { CryptoError.TRUST_E_SYSTEM_ERROR, "A system-level error occurred while verifying trust." },
                { CryptoError.TRUST_E_NO_SIGNER_CERT, "The certificate for the signer of the message is invalid or not found." },
                { CryptoError.TRUST_E_COUNTER_SIGNER, "One of the counter signatures was invalid." },
                { CryptoError.TRUST_E_CERT_SIGNATURE, "The signature of the certificate can not be verified." },
                { CryptoError.TRUST_E_TIME_STAMP, "The timestamp signature and/or certificate could not be verified or is malformed." },
                { CryptoError.TRUST_E_BAD_DIGEST, "The digital signature of the object did not verify." },
                { CryptoError.TRUST_E_BASIC_CONSTRAINTS, "A certificate's basic constraint extension has not been observed." },
                { CryptoError.TRUST_E_FINANCIAL_CRITERIA, "The certificate does not meet or contain the Authenticode financial extensions." },
                { CryptoError.TRUST_E_PROVIDER_UNKNOWN, "Unknown trust provider." },
                { CryptoError.TRUST_E_ACTION_UNKNOWN, "The trust verification action specified is not supported by the specified trust provider." },
                { CryptoError.TRUST_E_SUBJECT_FORM_UNKNOWN, "The form specified for the subject is not one supported or known by the specified trust provider." },
                { CryptoError.TRUST_E_SUBJECT_NOT_TRUSTED, "The subject is not trusted for the specified action." },
                { CryptoError.DIGSIG_E_ENCODE, "Error due to problem in ASN.1 encoding process." },
                { CryptoError.DIGSIG_E_DECODE, "Error due to problem in ASN.1 decoding process." },
                { CryptoError.DIGSIG_E_EXTENSIBILITY, "Reading / writing Extensions where Attributes are appropriate, and visa versa." },
                { CryptoError.DIGSIG_E_CRYPTO, "Unspecified cryptographic failure." },
                { CryptoError.PERSIST_E_SIZEDEFINITE, "The size of the data could not be determined." },
                { CryptoError.PERSIST_E_SIZEINDEFINITE, "The size of the indefinite-sized data could not be determined." },
                { CryptoError.PERSIST_E_NOTSELFSIZING, "This object does not read and write self-sizing data." },
                { CryptoError.TRUST_E_NOSIGNATURE, "No signature was present in the subject." },
                { CryptoError.CERT_E_EXPIRED, "A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file." },
                { CryptoError.CERT_E_VALIDITYPERIODNESTING, "The validity periods of the certification chain do not nest correctly." },
                { CryptoError.CERT_E_ROLE, "A certificate that can only be used as an end-entity is being used as a CA or visa versa." },
                { CryptoError.CERT_E_PATHLENCONST, "A path length constraint in the certification chain has been violated." },
                { CryptoError.CERT_E_CRITICAL, "A certificate contains an unknown extension that is marked 'critical'." },
                { CryptoError.CERT_E_PURPOSE, "A certificate being used for a purpose other than the ones specified by its CA." },
                { CryptoError.CERT_E_ISSUERCHAINING, "A parent of a given certificate in fact did not issue that child certificate." },
                { CryptoError.CERT_E_MALFORMED, "A certificate is missing or has an empty value for an important field, such as a subject or issuer name." },
                { CryptoError.CERT_E_UNTRUSTEDROOT, "A certificate chain processed correctly, but terminated in a root certificate which is not trusted by the trust provider." },
                { CryptoError.CERT_E_CHAINING, "An internal certificate chaining error has occurred." },
                { CryptoError.TRUST_E_FAIL, "Generic trust failure." },
                { CryptoError.CERT_E_REVOKED, "A certificate was explicitly revoked by its issuer." },
                { CryptoError.CERT_E_UNTRUSTEDTESTROOT, "The certification path terminates with the test root which is not trusted with the current policy settings." },
                { CryptoError.CERT_E_REVOCATION_FAILURE, "The revocation process could not continue - the certificate(s) could not be checked." },
                { CryptoError.CERT_E_CN_NO_MATCH, "The certificate's CN name does not match the passed value." },
                { CryptoError.CERT_E_WRONG_USAGE, "The certificate is not valid for the requested usage." },
                { CryptoError.TRUST_E_EXPLICIT_DISTRUST, "The certificate was explicitly marked as untrusted by the user." },
                { CryptoError.CERT_E_UNTRUSTEDCA, "A certification chain processed correctly, but one of the CA certificates is not trusted by the policy provider." },
                { CryptoError.CERT_E_INVALID_POLICY, "The certificate has invalid policy." },
                { CryptoError.CERT_E_INVALID_NAME, "The certificate has an invalid name. The name is not included in the permitted list or is explicitly excluded." },
                { CryptoError.CRYPT_E_FILE_ERROR, "An error occurred while reading or writing to a file." },
                { CryptoError.CRYPT_E_SECURITY_SETTINGS, "The cryptographic operation failed due to a local security option setting." },
                { CryptoError.NTE_BAD_ALGID, "Invalid algorithm specified." }
            };
            return result;
        }

        public static string GetErrorDescription(this CryptoError cryptoError)
        {
            string result;
            if (!s_cryptoErrorToDescriptionMap.TryGetValue(cryptoError, out result))
            {
                throw new InvalidOperationException("Unrecognized crypto HRESULT: 0x" + cryptoError.ToString("x"));
            }
            return result;
        }
    }
}
