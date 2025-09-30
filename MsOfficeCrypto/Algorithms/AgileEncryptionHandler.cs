using System;
using System.Security.Cryptography;
using System.Text;
using MsOfficeCrypto.Exceptions;

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// Advanced Agile Encryption handler for MS-OFFCRYPTO specification
    /// Implements ECMA-376 Agile encryption with full key derivation
    /// </summary>
    public static class AgileEncryptionHandler
    {
        private const int AGILE_ITERATION_COUNT = 100000;
        private const int KEY_DATA_BLOCK_SIZE = 4096;

        /// <summary>
        /// Derives encryption key using Agile encryption key derivation
        /// Per MS-OFFCRYPTO Agile encryption specification
        /// </summary>
        /// <param name="password">User password</param>
        /// <param name="keyData">KeyData from EncryptionInfo</param>
        /// <param name="hashAlgorithm">Hash algorithm to use</param>
        /// <param name="keyBits">Key size in bits</param>
        /// <param name="blockKey">Block key for key derivation</param>
        /// <returns>Derived encryption key</returns>
        public static byte[] DeriveAgileKey(string password, byte[] keyData, HashAlgorithmName hashAlgorithm, 
            int keyBits, byte[] blockKey)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            
            if (keyData == null || keyData.Length == 0)
                throw new ArgumentException("KeyData cannot be null or empty", nameof(keyData));

            try
            {
                // Step 1: Convert password to UTF-16LE
                byte[] passwordBytes = Encoding.Unicode.GetBytes(password);

                // Step 2: Extract salt from keyData (first 16 bytes)
                if (keyData.Length < 16)
                    throw new CorruptedEncryptionInfoException("KeyData too short for salt extraction");

                var salt = new byte[16];
                Array.Copy(keyData, 0, salt, 0, 16);

                // Step 3: Perform PBKDF2-like derivation
                byte[] derivedKey = DeriveKeyPbKdf2(passwordBytes, salt, AGILE_ITERATION_COUNT, 
                    keyBits / 8, hashAlgorithm);

                // Step 4: Apply block key if provided
                if (blockKey.Length > 0)
                {
                    derivedKey = ApplyBlockKey(derivedKey, blockKey, hashAlgorithm);
                }

                return derivedKey;
            }
            catch (Exception ex)
            {
                throw new KeyDerivationException($"Agile key derivation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Verifies password using Agile encryption verification
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <param name="keyData">KeyData from EncryptionInfo</param>
        /// <param name="encryptedVerifierHashInput">Encrypted verifier hash input</param>
        /// <param name="encryptedVerifierHashValue">Encrypted verifier hash value</param>
        /// <param name="encryptedKeyValue">Encrypted key value</param>
        /// <param name="hashAlgorithm">Hash algorithm</param>
        /// <param name="keyBits">Key size in bits</param>
        /// <returns>True if the password is correct</returns>
        public static bool VerifyAgilePassword(string password, byte[] keyData,
            byte[] encryptedVerifierHashInput, byte[] encryptedVerifierHashValue,
            byte[] encryptedKeyValue, HashAlgorithmName hashAlgorithm, int keyBits)
        {
            try
            {
                // Derive the password verification key
                byte[] pwdVerifierKey = DeriveAgileKey(password, keyData, hashAlgorithm, keyBits, 
                    CreateBlockKey("verifierHashInput"));

                // Decrypt the verifier hash input
                byte[] verifierHashInput = DecryptAgileData(encryptedVerifierHashInput, pwdVerifierKey);

                // Calculate hash of the decrypted input
                byte[] computedHash = ComputeHash(verifierHashInput, hashAlgorithm);

                // Derive key for verifier hash value
                byte[] hashVerifierKey = DeriveAgileKey(password, keyData, hashAlgorithm, keyBits,
                    CreateBlockKey("verifierHashValue"));

                // Decrypt the expected hash
                byte[] expectedHash = DecryptAgileData(encryptedVerifierHashValue, hashVerifierKey);

                // Compare hashes
                return CompareHashes(computedHash, expectedHash);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Decrypts document data using Agile encryption
        /// </summary>
        /// <param name="encryptedData">Encrypted document data</param>
        /// <param name="password">Decryption password</param>
        /// <param name="keyData">KeyData from EncryptionInfo</param>
        /// <param name="hashAlgorithm">Hash algorithm</param>
        /// <param name="keyBits">Key size in bits</param>
        /// <param name="cipherAlgorithm">Cipher algorithm</param>
        /// <param name="cipherChaining">Cipher chaining mode</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptAgileDocument(byte[] encryptedData, string password,
            byte[] keyData, HashAlgorithmName hashAlgorithm, int keyBits,
            string cipherAlgorithm, string cipherChaining)
        {
            try
            {
                // Derive the document encryption key
                byte[] documentKey = DeriveAgileKey(password, keyData, hashAlgorithm, keyBits,
                    CreateBlockKey("encryptedPackage"));

                // Decrypt based on cipher algorithm and chaining mode
                return cipherAlgorithm.ToUpper() switch
                {
                    "AES" => DecryptAesAgile(encryptedData, documentKey, cipherChaining, keyBits),
                    _ => throw new UnsupportedEncryptionException($"Unsupported cipher algorithm: {cipherAlgorithm}")
                };
            }
            catch (Exception ex) when (!(ex is UnsupportedEncryptionException))
            {
                throw new DecryptionException($"Agile document decryption failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Performs PBKDF2-like key derivation for Agile encryption
        /// </summary>
        private static byte[] DeriveKeyPbKdf2(byte[] password, byte[] salt, int iterations, 
            int keyLength, HashAlgorithmName hashAlgorithm)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, hashAlgorithm);
            return pbkdf2.GetBytes(keyLength);
        }

        /// <summary>
        /// Applies block key transformation to derived key
        /// </summary>
        private static byte[] ApplyBlockKey(byte[] derivedKey, byte[] blockKey, HashAlgorithmName hashAlgorithm)
        {
            var combined = new byte[derivedKey.Length + blockKey.Length];
            Array.Copy(derivedKey, 0, combined, 0, derivedKey.Length);
            Array.Copy(blockKey, 0, combined, derivedKey.Length, blockKey.Length);

            return ComputeHash(combined, hashAlgorithm);
        }

        /// <summary>
        /// Creates a block key from a string identifier
        /// </summary>
        private static byte[] CreateBlockKey(string identifier)
        {
            return Encoding.UTF8.GetBytes(identifier);
        }

        /// <summary>
        /// Decrypts data using Agile encryption
        /// </summary>
        private static byte[] DecryptAgileData(byte[] encryptedData, byte[] key)
        {
            if (encryptedData.Length < 16)
                throw new DecryptionException("Encrypted data too short for Agile decryption");

            // Extract IV from first 16 bytes
            var iv = new byte[16];
            Array.Copy(encryptedData, 0, iv, 0, 16);

            // Extract actual encrypted data
            var actualData = new byte[encryptedData.Length - 16];
            Array.Copy(encryptedData, 16, actualData, 0, actualData.Length);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = EnsureKeyLength(key, 128);
            aes.IV = iv;

            using ICryptoTransform decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(actualData, 0, actualData.Length);
        }

        /// <summary>
        /// Decrypts AES data with Agile encryption
        /// </summary>
        private static byte[] DecryptAesAgile(byte[] encryptedData, byte[] key, string chainingMode, int keyBits)
        {
            using var aes = Aes.Create();
            aes.KeySize = keyBits;
            aes.Key = EnsureKeyLength(key, keyBits);

            aes.Mode = chainingMode.ToUpper() switch
            {
                "CBC"  => CipherMode.CBC,
                "CHAININGMODECBC" => CipherMode.CBC,
                "CFB" => CipherMode.CFB,
                "CHAINGNIMODECFB" => CipherMode.CFB,
                _ => throw new UnsupportedEncryptionException($"Unsupported chaining mode: {chainingMode}")
            };

            aes.Padding = PaddingMode.PKCS7;

            // For Agile encryption, IV is typically the first 16 bytes
            if (encryptedData.Length < 16)
                throw new DecryptionException("Encrypted data too short for IV extraction");

            var iv = new byte[16];
            Array.Copy(encryptedData, 0, iv, 0, 16);
            aes.IV = iv;

            var cipherData = new byte[encryptedData.Length - 16];
            Array.Copy(encryptedData, 16, cipherData, 0, cipherData.Length);

            using ICryptoTransform decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(cipherData, 0, cipherData.Length);
        }

        /// <summary>
        /// Computes hash using specified algorithm
        /// </summary>
        private static byte[] ComputeHash(byte[] data, HashAlgorithmName hashAlgorithm)
        {
            using HashAlgorithm hasher = HashAlgorithm.Create(hashAlgorithm.Name) ??
                                         throw new UnsupportedEncryptionException($"Hash algorithm not supported: {hashAlgorithm.Name}");
            
            return hasher.ComputeHash(data);
        }

        /// <summary>
        /// Compares two hash values securely
        /// </summary>
        private static bool CompareHashes(byte[] hash1, byte[] hash2)
        {
            if (hash1.Length != hash2.Length)
                return false;

            var result = 0;
            for (var i = 0; i < hash1.Length; i++)
            {
                result |= hash1[i] ^ hash2[i];
            }
            return result == 0;
        }

        /// <summary>
        /// Ensures key is the correct length for the specified key size
        /// </summary>
        private static byte[] EnsureKeyLength(byte[] key, int keyBits)
        {
            int requiredBytes = keyBits / 8;
            
            if (key.Length == requiredBytes)
                return key;

            var adjustedKey = new byte[requiredBytes];
            Array.Copy(key, 0, adjustedKey, 0, key.Length > requiredBytes ? requiredBytes : key.Length);

            return adjustedKey;
        }

        /// <summary>
        /// Gets the hash algorithm size in bytes
        /// </summary>
        public static int GetHashSize(HashAlgorithmName hashAlgorithm)
        {
            return hashAlgorithm.Name switch
            {
                "SHA1" => 20,
                "SHA256" => 32,
                "SHA384" => 48,
                "SHA512" => 64,
                "MD5" => 16,
                _ => throw new UnsupportedEncryptionException($"Unknown hash algorithm: {hashAlgorithm.Name}")
            };
        }

        /// <summary>
        /// Validates Agile encryption parameters
        /// </summary>
        public static void ValidateAgileParameters(byte[] keyData, HashAlgorithmName hashAlgorithm, 
            int keyBits, string cipherAlgorithm)
        {
            if (keyData == null || keyData.Length < 16)
                throw new ArgumentException("Invalid keyData for Agile encryption");

            if (keyBits != 128 && keyBits != 192 && keyBits != 256)
                throw new UnsupportedEncryptionException($"Unsupported key size: {keyBits}");

            if (string.IsNullOrEmpty(cipherAlgorithm))
                throw new ArgumentException("Cipher algorithm cannot be null or empty");

            // Validate hash algorithm
            try
            {
                using var hasher = HashAlgorithm.Create(hashAlgorithm.Name);
                if (hasher == null)
                    throw new UnsupportedEncryptionException($"Unsupported hash algorithm: {hashAlgorithm.Name}");
            }
            catch (Exception ex)
            {
                throw new UnsupportedEncryptionException("Hash algorithm", $"Hash algorithm validation failed: {ex.Message}", ex);
            }
        }
    }
}
