// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.CodeAnalysis.IL.Rules
{ 
    /// <summary>
    /// This class defines a range of execution conditions that identify various results
    /// in code, both for primary function of application and negative conditions
    /// </summary>
    public enum CryptoError : uint
    {
        // Possible DOS/Win32 error codes
        ERROR_SUCCESS = 0,
        //  The ASN1 error values are offset by CRYPT_E_ASN1_ERROR.  
        CRYPT_E_ASN1_INTERNAL = 0x80093101, // ASN1 internal encode or decode error.  
        CRYPT_E_ASN1_EOD = 0x80093102, // ASN1 unexpected end of data.  
        CRYPT_E_ASN1_CORRUPT = 0x80093103, // ASN1 corrupted data.  
        CRYPT_E_ASN1_LARGE = 0x80093104, // ASN1 value too large.  
        CRYPT_E_ASN1_CONSTRAINT = 0x80093105, // ASN1 constraint violated.  
        CRYPT_E_ASN1_MEMORY = 0x80093106, // ASN1 out of memory.  
        CRYPT_E_ASN1_OVERFLOW = 0x80093107, // ASN1 buffer overflow.  
        CRYPT_E_ASN1_BADPDU = 0x80093108, // ASN1 function not supported for this PDU.  
        CRYPT_E_ASN1_BADARGS = 0x80093109, // ASN1 bad arguments to function call.  
        CRYPT_E_ASN1_BADREAL = 0x8009310A, // ASN1 bad real value.  
        CRYPT_E_ASN1_BADTAG = 0x8009310B, // ASN1 bad tag value met.  
        CRYPT_E_ASN1_CHOICE = 0x8009310C, // ASN1 bad choice value.  
        CRYPT_E_ASN1_RULE = 0x8009310D, // ASN1 bad encoding rule.  
        CRYPT_E_ASN1_UTF8 = 0x8009310E, // ASN1 bad unicode (UTF8).  
        CRYPT_E_ASN1_PDU_TYPE = 0x80093133, // ASN1 bad PDU type.  
        CRYPT_E_ASN1_NYI = 0x80093134, // ASN1 not yet implemented.  
        CRYPT_E_ASN1_EXTENDED = 0x80093201, // ASN1 skipped unknown extension(s).  
        CRYPT_E_ASN1_NOEOD = 0x80093202, // ASN1 end of data expected  
        CERTSRV_E_BAD_REQUESTSUBJECT = 0x80094001, // The request subject name is invalid or too long.  
        CERTSRV_E_NO_REQUEST = 0x80094002, // The request does not exist.  
        CERTSRV_E_BAD_REQUESTSTATUS = 0x80094003, // The request's current status does not allow this operation.  
        CERTSRV_E_PROPERTY_EMPTY = 0x80094004, // The requested property value is empty.  
        CERTSRV_E_INVALID_CA_CERTIFICATE = 0x80094005, // The certification authority's certificate contains invalid data.  
        CERTSRV_E_SERVER_SUSPENDED = 0x80094006, // Certificate service has been suspended for a database restore operation.  
        CERTSRV_E_ENCODING_LENGTH = 0x80094007, // The certificate contains an encoded length that is potentially incompatible with older enrollment software.  
        CERTSRV_E_UNSUPPORTED_CERT_TYPE = 0x80094800, // The requested certificate template is not supported by this CA.  
        CERTSRV_E_NO_CERT_TYPE = 0x80094801, // The request contains no certificate template information.  
        TRUST_E_SYSTEM_ERROR = 0x80096001, // A system-level error occurred while verifying trust.  
        TRUST_E_NO_SIGNER_CERT = 0x80096002, // The certificate for the signer of the message is invalid or not found.  
        TRUST_E_COUNTER_SIGNER = 0x80096003, // One of the counter signatures was invalid.  
        TRUST_E_CERT_SIGNATURE = 0x80096004, // The signature of the certificate can not be verified.  
        TRUST_E_TIME_STAMP = 0x80096005, // The timestamp signature and/or certificate could not be verified or is malformed.  
        TRUST_E_BAD_DIGEST = 0x80096010, // The digital signature of the object did not verify.  
        TRUST_E_BASIC_CONSTRAINTS = 0x80096019, // A certificate's basic constraint extension has not been observed.  
        TRUST_E_FINANCIAL_CRITERIA = 0x8009601E, // The certificate does not meet or contain the Authenticode financial extensions.  
        TRUST_E_PROVIDER_UNKNOWN = 0x800B0001, // Unknown trust provider.  
        TRUST_E_ACTION_UNKNOWN = 0x800B0002, // The trust verification action specified is not supported by the specified trust provider.  
        TRUST_E_SUBJECT_FORM_UNKNOWN = 0x800B0003, // The form specified for the subject is not one supported or known by the specified trust provider.  
        TRUST_E_SUBJECT_NOT_TRUSTED = 0x800B0004, // The subject is not trusted for the specified action.  
        DIGSIG_E_ENCODE = 0x800B0005, // Error due to problem in ASN.1 encoding process.  
        DIGSIG_E_DECODE = 0x800B0006, // Error due to problem in ASN.1 decoding process.  
        DIGSIG_E_EXTENSIBILITY = 0x800B0007, // Reading / writing Extensions where Attributes are appropriate, and visa versa.  
        DIGSIG_E_CRYPTO = 0x800B0008, // Unspecified cryptographic failure.  
        PERSIST_E_SIZEDEFINITE = 0x800B0009, // The size of the data could not be determined.  
        PERSIST_E_SIZEINDEFINITE = 0x800B000A, // The size of the indefinite-sized data could not be determined.  
        PERSIST_E_NOTSELFSIZING = 0x800B000B, // This object does not read and write self-sizing data.  
        TRUST_E_NOSIGNATURE = 0x800B0100, // No signature was present in the subject.  
        CERT_E_EXPIRED = 0x800B0101, // A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file.  
        CERT_E_VALIDITYPERIODNESTING = 0x800B0102, // The validity periods of the certification chain do not nest correctly.  
        CERT_E_ROLE = 0x800B0103, // A certificate that can only be used as an end-entity is being used as a CA or visa versa.  
        CERT_E_PATHLENCONST = 0x800B0104, // A path length constraint in the certification chain has been violated.  
        CERT_E_CRITICAL = 0x800B0105, // A certificate contains an unknown extension that is marked 'critical'.  
        CERT_E_PURPOSE = 0x800B0106, // A certificate being used for a purpose other than the ones specified by its CA.  
        CERT_E_ISSUERCHAINING = 0x800B0107, // A parent of a given certificate in fact did not issue that child certificate.  
        CERT_E_MALFORMED = 0x800B0108, // A certificate is missing or has an empty value for an important field, such as a subject or issuer name.  
        CERT_E_UNTRUSTEDROOT = 0x800B0109, // A certificate chain processed correctly, but terminated in a root certificate which is not trusted by the trust provider.  
        CERT_E_CHAINING = 0x800B010A, // An internal certificate chaining error has occurred.  
        TRUST_E_Error = 0x800B010B, // Generic trust failure.  
        CERT_E_REVOKED = 0x800B010C, // A certificate was explicitly revoked by its issuer.  
        CERT_E_UNTRUSTEDTESTROOT = 0x800B010D, // The certification path terminates with the test root which is not trusted with the current policy settings.  
        CERT_E_REVOCATION_ErrorURE = 0x800B010E, // The revocation process could not continue - the certificate(s) could not be checked.  
        CERT_E_CN_NO_MATCH = 0x800B010F, // The certificate's CN name does not match the passed value.  
        CERT_E_WRONG_USAGE = 0x800B0110, // The certificate is not valid for the requested usage.  
        TRUST_E_EXPLICIT_DISTRUST = 0x800B0111, // The certificate was explicitly marked as untrusted by the user.  
        CERT_E_UNTRUSTEDCA = 0x800B0112, // A certification chain processed correctly, but one of the CA certificates is not trusted by the policy provider.  
        CERT_E_INVALID_POLICY = 0x800B0113, // The certificate has invalid policy.
        CERT_E_INVALID_NAME = 0x800B0114,  // The certificate has an invalid name. The name is not included in the permitted list or is explicitly excluded.
        CRYPT_E_FILE_ERROR = 0x80092003, // An error occurred while reading or writing to a file.
        CRYPT_E_SECURITY_SETTINGS = 0x80092026, // The cryptographic operation failed due to a local security option setting.
        NTE_BAD_ALGID = 0x80090008, // Invalid algorithm specified.
    }
}