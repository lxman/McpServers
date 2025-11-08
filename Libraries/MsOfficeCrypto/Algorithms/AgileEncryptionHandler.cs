using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MsOfficeCrypto.Exceptions;

// ReSharper disable InconsistentNaming

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// Advanced Agile Encryption handler for MS-OFFCRYPTO specification
    /// Implements ECMA-376 Agile encryption with full key derivation
    /// </summary>
    public static class AgileEncryptionHandler
    {
        // MS-OFFCRYPTO defined block key constants (section 2.3.4.7)
        private static readonly byte[] BlockKeyVerifierHashInput = 
            { 0xFE, 0xA7, 0xD2, 0x76, 0x3B, 0x4B, 0x9E, 0x79 };
    
        private static readonly byte[] BlockKeyEncryptedVerifierHashValue = 
            { 0xD7, 0xAA, 0x0F, 0x6D, 0x30, 0x61, 0x34, 0x4E };
    
        private static readonly byte[] BlockKeyEncryptedKeyValue = 
            { 0x14, 0x6E, 0x0B, 0xE7, 0xAB, 0xAC, 0xD0, 0xD6 };
        
        /// <summary>
        /// Derives encryption key using Agile encryption key derivation
        /// Per MS-OFFCRYPTO section 2.3.4.7
        /// </summary>
        public static byte[] DeriveAgileKey(string password, byte[] salt, int spinCount,
            HashAlgorithmName hashAlgorithm, int keyBits, byte[] blockKey)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
    
            if (salt == null || salt.Length == 0)
                throw new ArgumentException("Salt cannot be null or empty", nameof(salt));

            try
            {
                // Step 1: Convert password to UTF-16LE
                var passwordBytes = Encoding.Unicode.GetBytes(password);

                // Step 2: Derive key using Agile algorithm - returns FULL hash
                var derivedKey = DeriveAgileKeyInternal(passwordBytes, salt, spinCount, hashAlgorithm);

                // Step 3: Apply block key if provided
                // H_final = H(derivedKey || blockKey), then truncate
                if (blockKey.Length > 0)
                {
                    derivedKey = ApplyBlockKey(derivedKey, blockKey, hashAlgorithm, keyBits);
                }
                else
                {
                    // No block key, just truncate
                    var keyLength = keyBits / 8;
                    if (derivedKey.Length <= keyLength) return derivedKey;
                    var truncated = new byte[keyLength];
                    Array.Copy(derivedKey, 0, truncated, 0, keyLength);
                    derivedKey = truncated;
                }

                return derivedKey;
            }
            catch (Exception ex)
            {
                throw new KeyDerivationException($"Agile key derivation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Performs Agile key derivation per MS-OFFCRYPTO section 2.3.4.7
        /// Returns the FULL hash digest (NOT truncated!)
        /// </summary>
        private static byte[] DeriveAgileKeyInternal(byte[] password, byte[] salt, int spinCount, 
            HashAlgorithmName hashAlgorithm)
        {
            using var hasher = HashAlgorithm.Create(hashAlgorithm.Name) ??
                               throw new UnsupportedEncryptionException($"Hash algorithm not supported: {hashAlgorithm.Name}");

            // Step 1: H_0 = H(salt || password)
            var combined = new byte[salt.Length + password.Length];
            Array.Copy(salt, 0, combined, 0, salt.Length);
            Array.Copy(password, 0, combined, salt.Length, password.Length);
    
            var hash = hasher.ComputeHash(combined);

            // Step 2: Iterate spinCount times
            // H_i = H(iterator || H_{i-1}) where iterator is 32-bit little-endian integer
            for (var i = 0; i < spinCount; i++)
            {
                var iterator = BitConverter.GetBytes(i);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(iterator);
        
                combined = new byte[iterator.Length + hash.Length];
                Array.Copy(iterator, 0, combined, 0, iterator.Length);
                Array.Copy(hash, 0, combined, iterator.Length, hash.Length);
        
                hash = hasher.ComputeHash(combined);
            }

            // Step 3: Return FULL hash digest (e.g., 64 bytes for SHA512)
            // DO NOT truncate here!
            return hash;
        }

        /// <summary>
        /// Verifies password using Agile encryption verification
        /// </summary>
        public static bool VerifyAgilePassword(string password, byte[] passwordSalt, int spinCount,
            byte[] encryptedVerifierHashInput, byte[] encryptedVerifierHashValue,
            byte[] encryptedKeyValue, HashAlgorithmName hashAlgorithm, int keyBits)
        {
            try
            {
                // Derive the password verification key
                var pwdVerifierKey = DeriveAgileKey(password, passwordSalt, spinCount, 
                    hashAlgorithm, keyBits, GetBlockKey("verifierHashInput"));

                // FIXED: Use salt as IV for verifier data (not extracted from data)
                var verifierHashInput = DecryptVerifierData(encryptedVerifierHashInput, 
                    pwdVerifierKey, passwordSalt);

                // Calculate hash of the decrypted input
                var computedHash = ComputeHash(verifierHashInput, hashAlgorithm);

                // Derive key for verifier hash value
                var hashVerifierKey = DeriveAgileKey(password, passwordSalt, spinCount,
                    hashAlgorithm, keyBits, GetBlockKey("verifierHashValue"));

                // FIXED: Use salt as IV for verifier data (not extracted from data)
                var expectedHash = DecryptVerifierData(encryptedVerifierHashValue, 
                    hashVerifierKey, passwordSalt);

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
        public static byte[] DecryptAgileDocument(byte[] encryptedData, string password,
            byte[] passwordSalt, int spinCount, byte[] encryptedKeyValue, 
            byte[] keyDataSalt, long totalSize, HashAlgorithmName hashAlgorithm, 
            int keyBits, string cipherAlgorithm, string cipherChaining)
        {
            try
            {
                // Step 1: Derive the password key to decrypt the encryptedKeyValue
                var passwordKey = DeriveAgileKey(password, passwordSalt, spinCount, 
                    hashAlgorithm, keyBits, GetBlockKey("encryptedKey"));

                // Step 2: FIXED - Use salt as IV for encrypted key value
                var documentEncryptionKey = DecryptVerifierData(encryptedKeyValue, 
                    passwordKey, passwordSalt);

                // Step 3: Use the document encryption key to decrypt the package
                // Pass totalSize to handle proper truncation of decrypted data
                return cipherAlgorithm.ToUpper() switch
                {
                    "AES" => DecryptAesAgile(encryptedData, documentEncryptionKey, 
                        keyDataSalt, totalSize, hashAlgorithm, cipherChaining, keyBits),
                    _ => throw new UnsupportedEncryptionException($"Unsupported cipher algorithm: {cipherAlgorithm}")
                };
            }
            catch (Exception ex) when (!(ex is UnsupportedEncryptionException))
            {
                throw new DecryptionException($"Agile document decryption failed: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Applies block key transformation to derived key
        /// </summary>
        private static byte[] ApplyBlockKey(byte[] derivedKey, byte[] blockKey, 
            HashAlgorithmName hashAlgorithm, int keyBits)
        {
            var combined = new byte[derivedKey.Length + blockKey.Length];
            Array.Copy(derivedKey, 0, combined, 0, derivedKey.Length);
            Array.Copy(blockKey, 0, combined, derivedKey.Length, blockKey.Length);

            var hash = ComputeHash(combined, hashAlgorithm);
    
            // Truncate to keyBits/8
            var keyLength = keyBits / 8;
            if (hash.Length <= keyLength)
                return hash;
    
            var truncated = new byte[keyLength];
            Array.Copy(hash, 0, truncated, 0, keyLength);
            return truncated;
        }

        /// <summary>
        /// Creates a block key from a string identifier
        /// </summary>
        private static byte[] GetBlockKey(string keyType)
        {
            return keyType switch
            {
                "verifierHashInput" => BlockKeyVerifierHashInput,
                "verifierHashValue" => BlockKeyEncryptedVerifierHashValue,
                "encryptedKey" => BlockKeyEncryptedKeyValue,
                _ => throw new ArgumentException($"Unknown block key type: {keyType}", nameof(keyType))
            };
        }

        /// <summary>
        /// For verifier data, the passwordSalt is used directly as the IV (NOT extracted from data)
        /// </summary>
        private static byte[] DecryptVerifierData(byte[] encryptedData, byte[] key, byte[] salt)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;  // CRITICAL: No padding (data is already block-aligned)
            aes.Key = EnsureKeyLength(key, key.Length * 8);
            aes.IV = EnsureKeyLength(salt, 128);  // Use salt as IV directly

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }

        /// <summary>
        /// Decrypts AES data with Agile encryption (for document payload)
        /// Per MS-OFFCRYPTO section 2.3.4.14 - decrypts in 4096-byte segments with computed IVs
        /// </summary>
        private static byte[] DecryptAesAgile(byte[] encryptedData, byte[] key, 
            byte[] keyDataSalt, long totalSize, HashAlgorithmName hashAlgorithm, string chainingMode, int keyBits)
        {
            // Validate chaining mode
            var mode = chainingMode.ToUpper() switch
            {
                "CBC" => CipherMode.CBC,
                "CHAININGMODECBC" => CipherMode.CBC,
                "CFB" => CipherMode.CFB,
                "CHAININGMODECFB" => CipherMode.CFB,
                _ => throw new UnsupportedEncryptionException($"Unsupported chaining mode: {chainingMode}")
            };

            // Step 1: Process the document in 4096-byte segments
            const int SEGMENT_LENGTH = 4096;
            var output = new MemoryStream();
            var remaining = totalSize;
            var offset = 0;  // Start at the beginning - NO 8-byte header to skip
            var blockNumber = 0;

            while (offset < encryptedData.Length && remaining > 0)
            {
                // Calculate segment size - read up to SEGMENT_LENGTH bytes
                // Encrypted data is already block-aligned from encryption
                var segmentSize = Math.Min(SEGMENT_LENGTH, encryptedData.Length - offset);
        
                // Extract segment
                var segment = new byte[segmentSize];
                Array.Copy(encryptedData, offset, segment, 0, segmentSize);

                // Step 2: Compute IV for this segment
                // IV = H(keyDataSalt || blockNumber)[0:16]
                var iv = ComputeSegmentIV(keyDataSalt, blockNumber, hashAlgorithm);

                // Step 3: Decrypt segment
                var decrypted = DecryptSegment(segment, key, iv, mode, keyBits);
        
                // Write to output (truncate based on the remaining plaintext size)
                var writeLength = (int)Math.Min(decrypted.Length, remaining);
                output.Write(decrypted, 0, writeLength);
        
                remaining -= writeLength;
                offset += segmentSize;
                blockNumber++;
        
                if (remaining <= 0)
                    break;
            }

            return output.ToArray();
        }

        /// <summary>
        /// Computes the IV for a specific segment
        /// IV = H(keyDataSalt || blockNumber)[0:16]
        /// </summary>
        private static byte[] ComputeSegmentIV(byte[] keyDataSalt, int blockNumber, HashAlgorithmName hashAlgorithm)
        {
            // Create: keyDataSalt || blockNumber (as 32-bit LE)
            var blockBytes = BitConverter.GetBytes(blockNumber);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(blockBytes);
            
            var combined = new byte[keyDataSalt.Length + blockBytes.Length];
            Array.Copy(keyDataSalt, 0, combined, 0, keyDataSalt.Length);
            Array.Copy(blockBytes, 0, combined, keyDataSalt.Length, blockBytes.Length);
            
            // Hash it
            var hash = ComputeHash(combined, hashAlgorithm);
            
            // Take the first 16 bytes as IV
            var iv = new byte[16];
            Array.Copy(hash, 0, iv, 0, 16);
            return iv;
        }

        /// <summary>
        /// Decrypts a single segment with the provided IV
        /// </summary>
        private static byte[] DecryptSegment(byte[] segment, byte[] key, byte[] iv, CipherMode mode, int keyBits)
        {
            using var aes = Aes.Create();
            aes.KeySize = keyBits;  // Explicitly set key size for validation
            aes.Key = EnsureKeyLength(key, keyBits);  // Ensure key matches expected size
            aes.Mode = mode;
            aes.Padding = PaddingMode.None;  // CRITICAL: No padding
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(segment, 0, segment.Length);
        }

        /// <summary>
        /// Computes hash using the specified algorithm
        /// </summary>
        private static byte[] ComputeHash(byte[] data, HashAlgorithmName hashAlgorithm)
        {
            using var hasher = HashAlgorithm.Create(hashAlgorithm.Name) ??
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
            var requiredBytes = keyBits / 8;
            
            if (key.Length == requiredBytes)
                return key;

            var adjustedKey = new byte[requiredBytes];
            Array.Copy(key, 0, adjustedKey, 0, key.Length > requiredBytes ? requiredBytes : key.Length);

            return adjustedKey;
        }
    }
}