﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis.BinaryParsers.ProgramDatabase;

namespace Microsoft.CodeAnalysis.IL.Rules
{
    public static class RulesExtensionMethods
    {
        public static string CreateOutputCoalescedByCompiler(this IList<ObjectModuleDetails> objectModuleDetailsList, string comment = null)
        {
            var compilerToLibraries = new Dictionary<string, Dictionary<string, SortedSet<string>>>();

            foreach (ObjectModuleDetails objectModuleDetail in objectModuleDetailsList)
            {
                string compiler = CreateCompilerKey(objectModuleDetail);
                if (!compilerToLibraries.TryGetValue(compiler, out Dictionary<string, SortedSet<string>> libraries))
                {
                    libraries = compilerToLibraries[compiler] = new Dictionary<string, SortedSet<string>>();
                }

                string modulesKey = "[directly linked]";
                string name = Path.GetFileName(objectModuleDetail.Name);
                string libraryName = Path.GetFileName(objectModuleDetail.Library);

                if (Path.GetExtension(name) != ".obj" || name != libraryName)
                {
                    modulesKey = libraryName;
                }

                if (!string.IsNullOrEmpty(comment))
                {
                    modulesKey = $"{modulesKey} [{comment}]";
                }

                if (!libraries.TryGetValue(modulesKey, out SortedSet<string> modules))
                {
                    modules = libraries[modulesKey] = new SortedSet<string>();
                }
                modules.Add(name);
            }

            var sb = new StringBuilder();

            string[] sortedByCompiler = compilerToLibraries.Keys.ToArray();
            Array.Sort(sortedByCompiler);

            foreach (string compiler in sortedByCompiler)
            {
                Dictionary<string, SortedSet<string>> libraryToModules = compilerToLibraries[compiler];

                string[] sortedByLibrary = libraryToModules.Keys.ToArray();
                Array.Sort(sortedByLibrary);

                foreach (string library in sortedByLibrary)
                {
                    sb.Append(compiler + " : " + library);

                    SortedSet<string> modules = libraryToModules[library];
                    if (modules.Count != 1)
                    {
                        sb.Append(" (").AppendLine(string.Join(",", modules) + ")");
                    }
                    else
                    {
                        sb.AppendLine($" ({modules.First()})");
                    }
                }
            }

            return sb.ToString();
        }

        public static string CreateOutputCoalescedByLibrary(this IList<ObjectModuleDetails> objectModuleDetailsList)
        {
            var librariesToObjectModulesMap = new Dictionary<string, List<string>>();

            foreach (ObjectModuleDetails objectModuleDetail in objectModuleDetailsList)
            {
                string key = CreateLibraryKey(objectModuleDetail);
                if (!librariesToObjectModulesMap.TryGetValue(key, out List<string> objectModules))
                {
                    objectModules = librariesToObjectModulesMap[key] = new List<string>();
                }
                objectModules.Add(Path.GetFileName(objectModuleDetail.Name));
            }

            var sb = new StringBuilder();

            foreach (string key in librariesToObjectModulesMap.Keys)
            {
                sb.Append(key);

                List<string> objectModules = librariesToObjectModulesMap[key];
                objectModules.Sort();
                string[] oms = objectModules.ToArray();

                if (oms.Length != 1 || !key.StartsWith(oms[0]))
                {
                    sb.Append(" (").AppendLine(string.Join(",", oms) + ")");
                }
                else
                {
                    sb.AppendLine($" ({oms[0]})");
                }
            }

            return sb.ToString();
        }

        private static string CreateCompilerKey(ObjectModuleDetails objectModuleDetail)
        {
            return objectModuleDetail.CompilerName + " : " +
                   objectModuleDetail.Language.ToString().ToLowerInvariant() + " : " +
                   objectModuleDetail.CompilerBackEndVersion;
        }

        private static string CreateLibraryKey(ObjectModuleDetails objectModuleDetail)
        {
            return Path.GetFileName(
                            objectModuleDetail.Library) + "," +
                            objectModuleDetail.Language.ToString().ToLowerInvariant() + "," +
                            objectModuleDetail.CompilerBackEndVersion;
        }

        private static readonly Dictionary<CryptoError, string> s_cryptoErrorToDescriptionMap = BuildCryptoErrorDescriptions();

        private static Dictionary<CryptoError, string> BuildCryptoErrorDescriptions()
        {
            var result = new Dictionary<CryptoError, string>()
            {
                { CryptoError.ERROR_SUCCESS, "Call succeeded." },
                { CryptoError.CERT_E_CHAINING, "An internal certificate chaining error has occurred." },
                { CryptoError.CERT_E_CN_NO_MATCH, "The certificate's CN name does not match the passed value." },
                { CryptoError.CERT_E_CRITICAL, "A certificate contains an unknown extension that is marked 'critical'." },
                { CryptoError.CERT_E_EXPIRED, "A required certificate is not within its validity period when verifying against the current system clock or the timestamp in the signed file." },
                { CryptoError.CERT_E_INVALID_NAME, "The certificate has an invalid name. The name is not included in the permitted list or is explicitly excluded." },
                { CryptoError.CERT_E_INVALID_POLICY, "The certificate has invalid policy." },
                { CryptoError.CERT_E_ISSUERCHAINING, "A parent of a given certificate in fact did not issue that child certificate." },
                { CryptoError.CERT_E_MALFORMED, "A certificate is missing or has an empty value for an important field, such as a subject or issuer name." },
                { CryptoError.CERT_E_PATHLENCONST, "A path length constraint in the certification chain has been violated." },
                { CryptoError.CERT_E_PURPOSE, "A certificate being used for a purpose other than the ones specified by its CA." },
                { CryptoError.CERT_E_REVOCATION_FAILURE, "The revocation process could not continue - the certificate(s) could not be checked." },
                { CryptoError.CERT_E_REVOKED, "A certificate was explicitly revoked by its issuer." },
                { CryptoError.CERT_E_ROLE, "A certificate that can only be used as an end-entity is being used as a CA or visa versa." },
                { CryptoError.CERT_E_UNTRUSTEDCA, "A certification chain processed correctly, but one of the CA certificates is not trusted by the policy provider." },
                { CryptoError.CERT_E_UNTRUSTEDROOT, "A certificate chain processed correctly, but terminated in a root certificate which is not trusted by the trust provider." },
                { CryptoError.CERT_E_UNTRUSTEDTESTROOT, "The certification path terminates with the test root which is not trusted with the current policy settings." },
                { CryptoError.CERT_E_VALIDITYPERIODNESTING, "The validity periods of the certification chain do not nest correctly." },
                { CryptoError.CERT_E_WRONG_USAGE, "The certificate is not valid for the requested usage." },
                { CryptoError.CERTSRV_E_BAD_REQUESTSTATUS, "The request's current status does not allow this operation." },
                { CryptoError.CERTSRV_E_BAD_REQUESTSUBJECT, "The request subject name is invalid or too long." },
                { CryptoError.CERTSRV_E_ENCODING_LENGTH, "The certificate contains an encoded length that is potentially incompatible with older enrollment software." },
                { CryptoError.CERTSRV_E_INVALID_CA_CERTIFICATE, "The certification authority's certificate contains invalid data." },
                { CryptoError.CERTSRV_E_NO_CERT_TYPE, "The request contains no certificate template information." },
                { CryptoError.CERTSRV_E_NO_REQUEST, "The request does not exist." },
                { CryptoError.CERTSRV_E_PROPERTY_EMPTY, "The requested property value is empty." },
                { CryptoError.CERTSRV_E_SERVER_SUSPENDED, "Certificate service has been suspended for a database restore operation." },
                { CryptoError.CERTSRV_E_UNSUPPORTED_CERT_TYPE, "The requested certificate template is not supported by this CA." },
                { CryptoError.CRYPT_E_ALREADY_DECRYPTED, "The content of the cryptographic message has already been decrypted." },
                { CryptoError.CRYPT_E_ASN1_BADARGS, "ASN1 bad arguments to function call." },
                { CryptoError.CRYPT_E_ASN1_BADPDU, "ASN1 function not supported for this PDU." },
                { CryptoError.CRYPT_E_ASN1_BADREAL, "ASN1 bad real value." },
                { CryptoError.CRYPT_E_ASN1_BADTAG, "ASN1 bad tag value met." },
                { CryptoError.CRYPT_E_ASN1_CHOICE, "ASN1 bad choice value." },
                { CryptoError.CRYPT_E_ASN1_CONSTRAINT, "ASN1 constraint violated." },
                { CryptoError.CRYPT_E_ASN1_CORRUPT, "ASN1 corrupted data." },
                { CryptoError.CRYPT_E_ASN1_EOD, "ASN1 unexpected end of data." },
                { CryptoError.CRYPT_E_ASN1_EXTENDED, "ASN1 skipped unknown extension(s)." },
                { CryptoError.CRYPT_E_ASN1_INTERNAL, "ASN1 internal encode or decode error." },
                { CryptoError.CRYPT_E_ASN1_LARGE, "ASN1 value too large." },
                { CryptoError.CRYPT_E_ASN1_MEMORY, "ASN1 out of memory." },
                { CryptoError.CRYPT_E_ASN1_NOEOD, "ASN1 end of data expected." },
                { CryptoError.CRYPT_E_ASN1_NYI, "ASN1 not yet implemented." },
                { CryptoError.CRYPT_E_ASN1_OVERFLOW, "ASN1 buffer overflow." },
                { CryptoError.CRYPT_E_ASN1_PDU_TYPE, "ASN1 bad PDU type." },
                { CryptoError.CRYPT_E_ASN1_RULE, "ASN1 bad encoding rule." },
                { CryptoError.CRYPT_E_ASN1_UTF8, "ASN1 bad unicode (UTF8)." },
                { CryptoError.CRYPT_E_ATTRIBUTES_MISSING, "The cryptographic message does not contain all of the requested attributes." },
                { CryptoError.CRYPT_E_AUTH_ATTR_MISSING, "The cryptographic message does not contain an expected authenticated attribute." },
                { CryptoError.CRYPT_E_BAD_ENCODE, "An error occurred during encode or decode operation." },
                { CryptoError.CRYPT_E_BAD_LEN, "The length specified for the output data was insufficient." },
                { CryptoError.CRYPT_E_BAD_MSG, "Not a cryptographic message or the cryptographic message is not formatted correctly." },
                { CryptoError.CRYPT_E_CONTROL_TYPE, "Invalid control type." },
                { CryptoError.CRYPT_E_DELETED_PREV, "The previous certificate or CRL context was deleted." },
                { CryptoError.CRYPT_E_EXISTS, "The object or property already exists." },
                { CryptoError.CRYPT_E_FILE_ERROR, "An error occurred while reading or writing to a file." },
                { CryptoError.CRYPT_E_FILERESIZED, "The Put operation can not continue.  The file needs to be resized.  However, there is already a signature present.  A complete signing operation must be done." },
                { CryptoError.CRYPT_E_HASH_VALUE, "The hash value is not correct." },
                { CryptoError.CRYPT_E_INVALID_IA5_STRING, "The string contains a character not in the 7 bit ASCII character set." },
                { CryptoError.CRYPT_E_INVALID_INDEX, "The index value is not valid." },
                { CryptoError.CRYPT_E_INVALID_MSG_TYPE, "Invalid cryptographic message type." },
                { CryptoError.CRYPT_E_INVALID_NUMERIC_STRING, "The string contains a non-numeric character." },
                { CryptoError.CRYPT_E_INVALID_PRINTABLE_STRING, "The string contains a non-printable character." },
                { CryptoError.CRYPT_E_INVALID_X500_STRING, "The string contains an invalid X500 name attribute key, oid, value or delimiter." },
                { CryptoError.CRYPT_E_ISSUER_SERIALNUMBER, "Invalid issuer and/or serial number." },
                { CryptoError.CRYPT_E_MISSING_PUBKEY_PARA, "The public key's algorithm parameters are missing." },
                { CryptoError.CRYPT_E_MSG_ERROR, "An error occurred while performing an operation on a cryptographic message." },
                { CryptoError.CRYPT_E_NO_DECRYPT_CERT, "Cannot find the certificate and private key to use for decryption." },
                { CryptoError.CRYPT_E_NO_KEY_PROPERTY, "Cannot find the certificate and private key for decryption." },
                { CryptoError.CRYPT_E_NO_MATCH, "Cannot find the requested object." },
                { CryptoError.CRYPT_E_NO_PROVIDER, "No provider was specified for the store or object." },
                { CryptoError.CRYPT_E_NO_REVOCATION_CHECK, "The revocation function was unable to check revocation for the certificate." },
                { CryptoError.CRYPT_E_NO_REVOCATION_DLL, "No Dll or exported function was found to verify revocation." },
                { CryptoError.CRYPT_E_NO_SIGNER, "The signed cryptographic message does not have a signer for the specified signer index." },
                { CryptoError.CRYPT_E_NO_TRUSTED_SIGNER, "None of the signers of the cryptographic message or certificate trust list is trusted." },
                { CryptoError.CRYPT_E_NO_VERIFY_USAGE_CHECK, "The called function was unable to do a usage check on the subject." },
                { CryptoError.CRYPT_E_NO_VERIFY_USAGE_DLL, "No DLL or exported function was found to verify subject usage." },
                { CryptoError.CRYPT_E_NOT_CHAR_STRING, "The dwValueType for the CERT_NAME_VALUE is not one of the character strings.  Most likely it is either a CERT_RDN_ENCODED_BLOB or CERT_TDN_OCTED_STRING." },
                { CryptoError.CRYPT_E_NOT_DECRYPTED, "The content of the cryptographic message has not been decrypted yet." },
                { CryptoError.CRYPT_E_NOT_FOUND, "Cannot find object or property." },
                { CryptoError.CRYPT_E_NOT_IN_CTL, "The subject was not found in a Certificate Trust List (CTL)." },
                { CryptoError.CRYPT_E_NOT_IN_REVOCATION_DATABASE, "The certificate is not in the revocation server's database." },
                { CryptoError.CRYPT_E_OID_FORMAT, "The object identifier is poorly formatted." },
                { CryptoError.CRYPT_E_OSS_ERROR, "OSS Certificate encode/decode error code base" },
                { CryptoError.CRYPT_E_PENDING_CLOSE, "Final closure is pending until additional frees or closes." },
                { CryptoError.CRYPT_E_RECIPIENT_NOT_FOUND, "The enveloped-data message does not contain the specified recipient." },
                { CryptoError.CRYPT_E_REVOCATION_OFFLINE, "The revocation function was unable to check revocation because the revocation server was offline." },
                { CryptoError.CRYPT_E_REVOKED, "The certificate is revoked." },
                { CryptoError.CRYPT_E_SECURITY_SETTINGS, "The cryptographic operation failed due to a local security option setting." },
                { CryptoError.CRYPT_E_SELF_SIGNED, "The specified certificate is self signed." },
                { CryptoError.CRYPT_E_SIGNER_NOT_FOUND, "Cannot find the original signer." },
                { CryptoError.CRYPT_E_STREAM_INSUFFICIENT_DATA, "The streamed cryptographic message requires more data to complete the decode operation." },
                { CryptoError.CRYPT_E_STREAM_MSG_NOT_READY, "The streamed cryptographic message is not ready to return data." },
                { CryptoError.CRYPT_E_UNEXPECTED_ENCODING, "Unexpected cryptographic message encoding." },
                { CryptoError.CRYPT_E_UNEXPECTED_MSG_TYPE, "The certificate does not have a property that references a private key." },
                { CryptoError.CRYPT_E_UNKNOWN_ALGO, "Unknown cryptographic algorithm." },
                { CryptoError.CRYPT_E_VERIFY_USAGE_OFFLINE, "Since the server was offline, the called function was unable to complete the usage check." },
                { CryptoError.DIGSIG_E_CRYPTO, "Unspecified cryptographic failure." },
                { CryptoError.DIGSIG_E_DECODE, "Error due to problem in ASN.1 decoding process." },
                { CryptoError.DIGSIG_E_ENCODE, "Error due to problem in ASN.1 encoding process." },
                { CryptoError.DIGSIG_E_EXTENSIBILITY, "Reading / writing Extensions where Attributes are appropriate, and visa versa." },
                { CryptoError.MSSIPOTF_E_BAD_FIRST_TABLE_PLACEMENT, "First table does not appear after header information." },
                { CryptoError.MSSIPOTF_E_BAD_MAGICNUMBER, "The magic number in the head table is incorrect." },
                { CryptoError.MSSIPOTF_E_BAD_OFFSET_TABLE, "The offset table has incorrect values." },
                { CryptoError.MSSIPOTF_E_BADVERSION, "There is a bad version number in the file." },
                { CryptoError.MSSIPOTF_E_CANTGETOBJECT, "Could not retrieve an object from the file." },
                { CryptoError.MSSIPOTF_E_CRYPT, "A call to a CryptoAPI function failed." },
                { CryptoError.MSSIPOTF_E_DSIG_STRUCTURE, "The structure of the DSIG table is incorrect." },
                { CryptoError.MSSIPOTF_E_FAILED_HINTS_CHECK, "The file did not pass the hints check." },
                { CryptoError.MSSIPOTF_E_FAILED_POLICY, "The signature does not have the correct attributes for the policy." },
                { CryptoError.MSSIPOTF_E_FILE, "Failed on a file operation (open, map, read, write)." },
                { CryptoError.MSSIPOTF_E_FILE_CHECKSUM, "The file checksum is incorrect." },
                { CryptoError.MSSIPOTF_E_FILETOOSMALL, "File is too small to contain the last table." },
                { CryptoError.MSSIPOTF_E_NOHEADTABLE, "Could not find the head table in the file." },
                { CryptoError.MSSIPOTF_E_NOT_OPENTYPE, "The file is not an OpenType file." },
                { CryptoError.MSSIPOTF_E_OUTOFMEMRANGE, "Tried to reference a part of the file outside the proper range." },
                { CryptoError.MSSIPOTF_E_PCONST_CHECK, "A check failed in a partially constant table." },
                { CryptoError.MSSIPOTF_E_STRUCTURE, "Some kind of structural error." },
                { CryptoError.MSSIPOTF_E_TABLE_CHECKSUM, "A table checksum is incorrect." },
                { CryptoError.MSSIPOTF_E_TABLE_LONGWORD, "A table does not start on a long word boundary." },
                { CryptoError.MSSIPOTF_E_TABLE_PADBYTES, "Too many pad bytes between tables or pad bytes are not 0." },
                { CryptoError.MSSIPOTF_E_TABLE_TAGORDER, "Duplicate table tags or tags out of alphabetical order." },
                { CryptoError.MSSIPOTF_E_TABLES_OVERLAP, "Two or more tables overlap." },
                { CryptoError.NTE_BAD_ALGID, "Invalid algorithm specified." },
                { CryptoError.PERSIST_E_NOTSELFSIZING, "This object does not read and write self-sizing data." },
                { CryptoError.PERSIST_E_SIZEDEFINITE, "The size of the data could not be determined." },
                { CryptoError.PERSIST_E_SIZEINDEFINITE, "The size of the indefinite-sized data could not be determined." },
                { CryptoError.TRUST_E_ACTION_UNKNOWN, "The trust verification action specified is not supported by the specified trust provider." },
                { CryptoError.TRUST_E_BAD_DIGEST, "The digital signature of the object did not verify." },
                { CryptoError.TRUST_E_BASIC_CONSTRAINTS, "A certificate's basic constraint extension has not been observed." },
                { CryptoError.TRUST_E_CERT_SIGNATURE, "The signature of the certificate can not be verified." },
                { CryptoError.TRUST_E_COUNTER_SIGNER, "One of the counter signatures was invalid." },
                { CryptoError.TRUST_E_EXPLICIT_DISTRUST, "The certificate was explicitly marked as untrusted by the user." },
                { CryptoError.TRUST_E_FAIL, "Generic trust failure." },
                { CryptoError.TRUST_E_FINANCIAL_CRITERIA, "The certificate does not meet or contain the Authenticode financial extensions." },
                { CryptoError.TRUST_E_NO_SIGNER_CERT, "The certificate for the signer of the message is invalid or not found." },
                { CryptoError.TRUST_E_NOSIGNATURE, "No signature was present in the subject." },
                { CryptoError.TRUST_E_PROVIDER_UNKNOWN, "Unknown trust provider." },
                { CryptoError.TRUST_E_SUBJECT_FORM_UNKNOWN, "The form specified for the subject is not one supported or known by the specified trust provider." },
                { CryptoError.TRUST_E_SUBJECT_NOT_TRUSTED, "The subject is not trusted for the specified action." },
                { CryptoError.TRUST_E_SYSTEM_ERROR, "A system-level error occurred while verifying trust." },
                { CryptoError.TRUST_E_TIME_STAMP, "The timestamp signature and/or certificate could not be verified or is malformed." },
            };
            return result;
        }

        public static string GetErrorDescription(this CryptoError cryptoError)
        {
            if (!s_cryptoErrorToDescriptionMap.TryGetValue(cryptoError, out string result))
            {
                throw new InvalidOperationException("Unrecognized crypto HRESULT: 0x" + cryptoError.ToString("x"));
            }
            return result;
        }
    }
}
