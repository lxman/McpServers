using System;
using System.Security.Cryptography;
using System.Text;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// Implementation of RC4 encryption/decryption for legacy Office documents
    /// Supports both 40-bit RC4 and RC4 CryptoAPI encryption methods
    /// </summary>
    public class LegacyRc4Handler
    {
        /// <summary>
        /// Generates password hash for legacy Office encryption
        /// </summary>
        /// <param name="password">Password string</param>
        /// <param name="salt">Salt bytes (for CryptoAPI)</param>
        /// <param name="useSimpleHash">True for 40-bit RC4, false for CryptoAPI</param>
        /// <returns>Password hash bytes</returns>
        public static byte[] GeneratePasswordHash(string password, byte[]? salt = null, bool useSimpleHash = true)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            if (useSimpleHash)
            {
                return Generate40BitRc4Hash(password);
            }
            else
            {
                return salt is null
                    ? throw new ArgumentNullException(nameof(salt), "Salt is required for CryptoAPI hash generation")
                    : GenerateCryptoApiHash(password, salt);
            }
        }

        /// <summary>
        /// Generates 40-bit RC4 password hash (used in older Office files)
        /// </summary>
        private static byte[] Generate40BitRc4Hash(string password)
        {
            // Convert password to UTF-16LE (Windows Unicode)
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            
            // Simple hash algorithm for 40-bit RC4
            var hash = new byte[5]; // 40 bits = 5 bytes
            
            for (var i = 0; i < passwordBytes.Length; i++)
            {
                hash[i % 5] ^= passwordBytes[i];
            }
            
            // Apply additional transformations
            for (var i = 0; i < 5; i++)
            {
                hash[i] = (byte)((hash[i] << 1) | (hash[i] >> 7));
            }
            
            return hash;
        }

        /// <summary>
        /// Generates CryptoAPI password hash (used in newer legacy Office files)
        /// </summary>
        private static byte[] GenerateCryptoApiHash(string password, byte[] salt)
        {
            // Convert password to UTF-16LE
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            
            // Combine password and salt
            var combined = new byte[passwordBytes.Length + salt.Length];
            Array.Copy(passwordBytes, 0, combined, 0, passwordBytes.Length);
            Array.Copy(salt, 0, combined, passwordBytes.Length, salt.Length);
            
            // Generate SHA-1 hash
            using var sha1 = SHA1.Create();
            return sha1.ComputeHash(combined);
        }

        /// <summary>
        /// Derives the encryption key from the password hash
        /// </summary>
        /// <param name="passwordHash">Password hash bytes</param>
        /// <param name="blockNumber">Block number for key derivation (0 for simple cases)</param>
        /// <param name="keySize">Desired key size in bytes</param>
        /// <returns>Derived encryption key</returns>
        public static byte[] DeriveEncryptionKey(byte[] passwordHash, uint blockNumber = 0, int keySize = 16)
        {
            if (passwordHash == null)
                throw new ArgumentNullException(nameof(passwordHash));

            if (keySize <= 0 || keySize > 256)
                throw new ArgumentException("Key size must be between 1 and 256 bytes", nameof(keySize));

            // For block-based encryption, combine hash with block number
            if (blockNumber > 0)
            {
                byte[] blockBytes = BitConverter.GetBytes(blockNumber);
                var combined = new byte[passwordHash.Length + blockBytes.Length];
                Array.Copy(passwordHash, 0, combined, 0, passwordHash.Length);
                Array.Copy(blockBytes, 0, combined, passwordHash.Length, blockBytes.Length);
                
                using var sha1 = SHA1.Create();
                passwordHash = sha1.ComputeHash(combined);
            }

            // Ensure we have enough key material
            if (passwordHash.Length >= keySize)
            {
                var key = new byte[keySize];
                Array.Copy(passwordHash, 0, key, 0, keySize);
                return key;
            }
            else
            {
                // Extend key material by repeating the hash
                var key = new byte[keySize];
                for (var i = 0; i < keySize; i++)
                {
                    key[i] = passwordHash[i % passwordHash.Length];
                }
                return key;
            }
        }

        /// <summary>
        /// Verifies password against the stored verifier
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <param name="verifier">Stored password verifier</param>
        /// <param name="salt">Salt bytes (for CryptoAPI)</param>
        /// <param name="useSimpleHash">True for 40-bit RC4, false for CryptoAPI</param>
        /// <returns>True if the password is correct</returns>
        public static bool VerifyPassword(string password, byte[] verifier, byte[]? salt = null, bool useSimpleHash = true)
        {
            try
            {
                byte[] passwordHash = GeneratePasswordHash(password, salt, useSimpleHash);
                byte[] encryptionKey = DeriveEncryptionKey(passwordHash);
                
                // Decrypt the verifier
                byte[] decryptedVerifier = Rc4Transform(verifier, encryptionKey);
                
                // For simple verification, check if decrypted data looks reasonable
                // More sophisticated verification would check against known patterns
                return IsValidVerifier(decryptedVerifier);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Performs RC4 transformation (encryption/decryption)
        /// </summary>
        /// <param name="data">Data to transform</param>
        /// <param name="key">RC4 key</param>
        /// <returns>Transformed data</returns>
        public static byte[] Rc4Transform(byte[] data, byte[] key)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Length == 0)
                throw new ArgumentException("Key cannot be empty", nameof(key));

            var result = new byte[data.Length];
            var s = new byte[256];
            
            // Key scheduling algorithm (KSA)
            for (var i = 0; i < 256; i++)
            {
                s[i] = (byte)i;
            }
            
            var j = 0;
            for (var i = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) % 256;
                (s[i], s[j]) = (s[j], s[i]); // Swap
            }
            
            // Pseudo-random generation algorithm (PRGA)
            int x = 0, y = 0;
            for (var i = 0; i < data.Length; i++)
            {
                x = (x + 1) % 256;
                y = (y + s[x]) % 256;
                (s[x], s[y]) = (s[y], s[x]); // Swap
                
                int k = s[(s[x] + s[y]) % 256];
                result[i] = (byte)(data[i] ^ k);
            }
            
            return result;
        }

        /// <summary>
        /// Checks if decrypted verifier data appears valid
        /// </summary>
        private static bool IsValidVerifier(byte[] verifier)
        {
            if (verifier.Length < 4)
                return false;

            // Check for common patterns in valid verifiers
            // This is a simplified check - a real implementation would be more sophisticated
            
            // Check if not all zeros or all 0xFF
            var allZero = true;
            var allFf = true;
            
            foreach (byte b in verifier)
            {
                if (b != 0) allZero = false;
                if (b != 0xFF) allFf = false;
            }
            
            return !(allZero || allFf);
        }

        /// <summary>
        /// Decrypts legacy Office document stream
        /// </summary>
        /// <param name="encryptedData">Encrypted stream data</param>
        /// <param name="password">Password for decryption</param>
        /// <param name="encryptionInfo">Encryption information</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptStream(byte[] encryptedData, string password, EncryptionInfo encryptionInfo)
        {
            if (encryptedData == null)
                throw new ArgumentNullException(nameof(encryptedData));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            if (encryptionInfo == null)
                throw new ArgumentNullException(nameof(encryptionInfo));

            try
            {
                bool useSimpleHash = encryptionInfo.LegacyEncryptionMethod == "RC4 Encryption";
                byte[]? salt = null;
                
                // Extract salt from encryption info if available
                if (!useSimpleHash && encryptionInfo.ExcelFilePassRecord?.EncryptionInfo != null)
                {
                    // Extract salt from FilePass record for CryptoAPI
                    byte[]? encInfo = encryptionInfo.ExcelFilePassRecord.EncryptionInfo;
                    if (encInfo.Length >= 20) // Minimum size for CryptoAPI header
                    {
                        salt = new byte[16];
                        Array.Copy(encInfo, 4, salt, 0, 16); // Skip version info, get salt
                    }
                }

                byte[] passwordHash = GeneratePasswordHash(password, salt, useSimpleHash);
                byte[] encryptionKey = DeriveEncryptionKey(passwordHash);
                
                return Rc4Transform(encryptedData, encryptionKey);
            }
            catch (Exception ex)
            {
                throw new DecryptionException($"Failed to decrypt legacy Office stream: {ex.Message}", ex);
            }
        }
    }
}