using System;
using MsOfficeCrypto.Utils;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Encryption Verifier structure from MS-OFFCRYPTO specification  
    /// Phase 3: Complete implementation with password verification
    /// </summary>
    public class EncryptionVerifier
    {
        /// <summary>
        /// Size of the salt value
        /// </summary>
        public uint SaltSize { get; set; }

        /// <summary>
        /// Salt value used in key derivation
        /// </summary>
        public byte[]? Salt { get; set; }

        /// <summary>
        /// Encrypted verifier value
        /// </summary>
        public byte[]? EncryptedVerifier { get; set; }

        /// <summary>
        /// Size of verifier hash value
        /// </summary>
        public uint VerifierHashSize { get; set; }

        /// <summary>
        /// Encrypted verifier hash
        /// </summary>
        public byte[]? EncryptedVerifierHash { get; set; }

        /// <summary>
        /// Raw verifier data
        /// </summary>
        public byte[]? RawData { get; set; }

        /// <summary>
        /// Verifies a password against this verifier
        /// Phase 3: Complete implementation using MS-OFFCRYPTO specification
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <param name="header">Associated encryption header</param>
        /// <returns>True if password is correct</returns>
        public bool VerifyPassword(string password, EncryptionHeader header)
        {
            if (Salt == null || EncryptedVerifier == null || EncryptedVerifierHash == null)
            {
                throw new InvalidOperationException("Verifier data is incomplete");
            }

            return PasswordDerivation.VerifyPassword(password, this, header);
        }

        /// <summary>
        /// Derives encryption key from password
        /// Phase 3: Complete implementation using MS-OFFCRYPTO specification
        /// </summary>
        /// <param name="password">Password for key derivation</param>
        /// <param name="header">Associated encryption header</param>
        /// <returns>Derived encryption key</returns>
        public byte[] DeriveKey(string password, EncryptionHeader header)
        {
            return Salt == null
                ? throw new InvalidOperationException("Salt data is missing")
                : PasswordDerivation.DeriveKey(password, Salt, (int)header.KeySize);
        }

        /// <summary>
        /// Validates the verifier structure integrity
        /// </summary>
        /// <returns>True if verifier data appears valid</returns>
        public bool IsValid()
        {
            return SaltSize > 0 && 
                   Salt != null && 
                   Salt.Length == SaltSize &&
                   EncryptedVerifier != null && 
                   EncryptedVerifier.Length > 0 &&
                   VerifierHashSize > 0 &&
                   EncryptedVerifierHash != null && 
                   EncryptedVerifierHash.Length > 0;
        }

        /// <summary>
        /// Gets security assessment of the verifier
        /// </summary>
        /// <returns>Security level description</returns>
        public string GetSecurityLevel()
        {
            if (!IsValid()) return "Invalid";

            int saltBytes = Salt?.Length ?? 0;
            int verifierBytes = EncryptedVerifier?.Length ?? 0;
            int hashBytes = EncryptedVerifierHash?.Length ?? 0;

            if (saltBytes >= 16 && verifierBytes >= 16 && hashBytes >= 20)
            {
                return "Standard Security";
            }
            if (saltBytes >= 8 && verifierBytes >= 8 && hashBytes >= 16)
            {
                return "Minimal Security";
            }
            return "Weak Security";
        }

        /// <summary>
        /// Gets detailed information about the verifier data
        /// </summary>
        public string GetDetailedInfo()
        {
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Salt Size: {SaltSize} bytes");
            if (Salt != null)
                details.AppendLine($"Salt: {HexUtils.ToHexString(Salt)}");
            if (EncryptedVerifier != null)
                details.AppendLine($"Encrypted Verifier: {HexUtils.ToHexString(EncryptedVerifier)}");
            details.AppendLine($"Verifier Hash Size: {VerifierHashSize} bytes");
            if (EncryptedVerifierHash != null)
                details.AppendLine($"Encrypted Verifier Hash: {HexUtils.ToHexString(EncryptedVerifierHash)}");
            details.AppendLine($"Security Level: {GetSecurityLevel()}");
            details.AppendLine($"Structure Valid: {IsValid()}");
            return details.ToString();
        }

        /// <summary>
        /// Creates a copy of this verifier with raw data cleared (for security)
        /// </summary>
        /// <returns>Sanitized copy of verifier</returns>
        public EncryptionVerifier CreateSanitizedCopy()
        {
            return new EncryptionVerifier
            {
                SaltSize = SaltSize,
                Salt = Salt?.Length > 0 ? new byte[Salt.Length] : null, // Zero out actual salt
                EncryptedVerifier = null, // Remove encrypted data
                VerifierHashSize = VerifierHashSize,
                EncryptedVerifierHash = null, // Remove encrypted hash
                RawData = null // Remove raw data
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Salt: {Salt?.Length ?? 0} bytes, Verifier: {EncryptedVerifier?.Length ?? 0} bytes, Hash: {EncryptedVerifierHash?.Length ?? 0} bytes, Security: {GetSecurityLevel()}";
        }
    }
}