using System;
using System.Security.Cryptography;
using System.Text;
using MsOfficeCrypto.Structures;

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// Enhanced RC4 CryptoAPI implementation for legacy Office documents
    /// Based on MS-OFFCRYPTO Section 2.3.7.2 - Binary Document RC4 CryptoAPI Encryption Method
    /// Implements proper CryptDeriveKey equivalent functionality
    /// </summary>
    public static class EnhancedRc4CryptoApiHandler
    {
        private const int RC4_KEY_SIZE = 128; // 128-bit RC4 key
        private const int MD5_HASH_SIZE = 16;
        private const int SHA1_HASH_SIZE = 20;

        /// <summary>
        /// Parses CryptoAPI encryption information from FilePass record
        /// </summary>
        /// <param name="filePassData">FilePass record data (excluding type field)</param>
        /// <returns>Parsed encryption information</returns>
        public static CryptoApiEncryptionInfo ParseEncryptionInfo(byte[] filePassData)
        {
            if (filePassData == null || filePassData.Length < 32)
                throw new ArgumentException("Invalid FilePass data for CryptoAPI encryption", nameof(filePassData));

            var info = new CryptoApiEncryptionInfo();
            
            using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(filePassData));
            
            // Read version (4 bytes)
            info.Version = reader.ReadUInt32();
            
            // Read salt (16 bytes)
            info.Salt = reader.ReadBytes(CryptoApiEncryptionInfo.SALT_SIZE);
            
            // Read encrypted verifier (16 bytes)
            info.EncryptedVerifier = reader.ReadBytes(16);
            
            // Read encrypted verifier hash (20 bytes for SHA1, 16 for MD5)
            // For simplicity, always read 20 bytes and truncate if needed
            if (reader.BaseStream.Position + 20 <= reader.BaseStream.Length)
            {
                info.EncryptedVerifierHash = reader.ReadBytes(20);
            }
            else
            {
                // Fallback for MD5 (16 bytes)
                info.EncryptedVerifierHash = reader.ReadBytes(16);
                info.HashAlgorithm = HashAlgorithmType.Md5;
            }

            return info;
        }

        /// <summary>
        /// Derives an encryption key using CryptoAPI key derivation
        /// Equivalent to Windows CryptDeriveKey function
        /// </summary>
        /// <param name="password">Password string</param>
        /// <param name="salt">Salt bytes from encryption info</param>
        /// <param name="hashAlgorithm">Hash algorithm to use</param>
        /// <param name="keySize">Key size in bits (default 128)</param>
        /// <returns>Derived encryption key</returns>
        public static byte[] DeriveEncryptionKey(string password, byte[] salt, HashAlgorithmType hashAlgorithm = HashAlgorithmType.Md5, int keySize = 128)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            if (!(salt is { Length: CryptoApiEncryptionInfo.SALT_SIZE }))
                throw new ArgumentException("Salt must be 16 bytes", nameof(salt));

            // Convert password to UTF-16LE
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            
            // Combine password and salt
            var combined = new byte[passwordBytes.Length + salt.Length];
            Array.Copy(passwordBytes, 0, combined, 0, passwordBytes.Length);
            Array.Copy(salt, 0, combined, passwordBytes.Length, salt.Length);

            // Hash the combined data
            byte[] hash = hashAlgorithm switch
            {
                HashAlgorithmType.Md5 => ComputeMd5Hash(combined),
                HashAlgorithmType.Sha1 => ComputeSha1Hash(combined),
                _ => throw new ArgumentException($"Unsupported hash algorithm: {hashAlgorithm}")
            };

            // Derive key from hash using key strengthening
            return DeriveKeyFromHash(hash, keySize / 8);
        }

        /// <summary>
        /// Derives key from hash with key strengthening (CryptDeriveKey equivalent)
        /// </summary>
        private static byte[] DeriveKeyFromHash(byte[] hash, int keyLength)
        {
            if (keyLength <= hash.Length)
            {
                var key = new byte[keyLength];
                Array.Copy(hash, 0, key, 0, keyLength);
                return key;
            }

            // If we need more key material, use PBKDF1-like stretching
            var extendedKey = new byte[keyLength];
            var copied = 0;
            
            // Copy initial hash
            int toCopy = Math.Min(hash.Length, keyLength);
            Array.Copy(hash, 0, extendedKey, 0, toCopy);
            copied += toCopy;

            // Extend with additional rounds if needed
            while (copied < keyLength)
            {
                using var md5 = MD5.Create();
                hash = md5.ComputeHash(hash);
                
                toCopy = Math.Min(hash.Length, keyLength - copied);
                Array.Copy(hash, 0, extendedKey, copied, toCopy);
                copied += toCopy;
            }

            return extendedKey;
        }

        /// <summary>
        /// Derives a block-specific encryption key
        /// </summary>
        /// <param name="baseKey">Base encryption key</param>
        /// <param name="blockNumber">Block number (512-byte blocks)</param>
        /// <returns>Block-specific key</returns>
        public static byte[] DeriveBlockKey(byte[] baseKey, uint blockNumber)
        {
            if (baseKey is null)
                throw new ArgumentNullException(nameof(baseKey));

            // Combine base key with block number
            byte[] blockBytes = BitConverter.GetBytes(blockNumber);
            var combined = new byte[baseKey.Length + blockBytes.Length];
            Array.Copy(baseKey, 0, combined, 0, baseKey.Length);
            Array.Copy(blockBytes, 0, combined, baseKey.Length, blockBytes.Length);

            // Hash to create block-specific key
            using var md5 = MD5.Create();
            byte[] blockHash = md5.ComputeHash(combined);

            // Return key sized for RC4 (use first 16 bytes)
            var blockKey = new byte[Math.Min(16, baseKey.Length)];
            Array.Copy(blockHash, 0, blockKey, 0, blockKey.Length);
            return blockKey;
        }

        /// <summary>
        /// Verifies password using the CryptoAPI method
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <param name="encryptionInfo">CryptoAPI encryption information</param>
        /// <returns>True if the password is correct</returns>
        public static bool VerifyPassword(string password, CryptoApiEncryptionInfo encryptionInfo)
        {
            try
            {
                // Derive encryption key
                byte[] encryptionKey = DeriveEncryptionKey(password, encryptionInfo.Salt, encryptionInfo.HashAlgorithm);

                // Decrypt the verifier
                byte[] decryptedVerifier = LegacyRc4Handler.Rc4Transform(encryptionInfo.EncryptedVerifier, encryptionKey);

                // Hash the decrypted verifier
                byte[] verifierHash = encryptionInfo.HashAlgorithm switch
                {
                    HashAlgorithmType.Md5 => ComputeMd5Hash(decryptedVerifier),
                    HashAlgorithmType.Sha1 => ComputeSha1Hash(decryptedVerifier),
                    _ => throw new NotSupportedException($"Unsupported hash algorithm: {encryptionInfo.HashAlgorithm}")
                };

                // Decrypt the stored verifier hash
                byte[] decryptedStoredHash = LegacyRc4Handler.Rc4Transform(encryptionInfo.EncryptedVerifierHash, encryptionKey);

                // Compare hashes (constant time comparison to prevent timing attacks)
                return ConstantTimeEquals(verifierHash, decryptedStoredHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Decrypts document stream using RC4 CryptoAPI
        /// </summary>
        /// <param name="encryptedData">Encrypted stream data</param>
        /// <param name="password">Password for decryption</param>
        /// <param name="encryptionInfo">CryptoAPI encryption information</param>
        /// <param name="streamOffset">Offset within the document (for block calculation)</param>
        /// <returns>Decrypted stream data</returns>
        public static byte[] DecryptStream(byte[] encryptedData, string password, CryptoApiEncryptionInfo encryptionInfo, long streamOffset = 0)
        {
            if (encryptedData == null)
                throw new ArgumentNullException(nameof(encryptedData));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            // Derive base encryption key
            byte[] baseKey = DeriveEncryptionKey(password, encryptionInfo.Salt, encryptionInfo.HashAlgorithm);

            // Decrypt in 512-byte blocks
            return DecryptBlocks(encryptedData, baseKey, streamOffset);
        }

        /// <summary>
        /// Decrypts data in 512-byte blocks with block-specific keys
        /// </summary>
        private static byte[] DecryptBlocks(byte[] encryptedData, byte[] baseKey, long streamOffset)
        {
            const int blockSize = 512;
            var result = new byte[encryptedData.Length];
            
            for (var i = 0; i < encryptedData.Length; i += blockSize)
            {
                // Calculate block number
                var blockNumber = (uint)((streamOffset + i) / blockSize);
                
                // Derive block-specific key
                byte[] blockKey = DeriveBlockKey(baseKey, blockNumber);
                
                // Decrypt this block
                int currentBlockSize = Math.Min(blockSize, encryptedData.Length - i);
                var blockData = new byte[currentBlockSize];
                Array.Copy(encryptedData, i, blockData, 0, currentBlockSize);
                
                byte[] decryptedBlock = LegacyRc4Handler.Rc4Transform(blockData, blockKey);
                Array.Copy(decryptedBlock, 0, result, i, currentBlockSize);
            }

            return result;
        }

        /// <summary>
        /// Decrypts a Word document using RC4 CryptoAPI
        /// </summary>
        /// <param name="encryptedData">Encrypted WordDocument stream</param>
        /// <param name="password">Password</param>
        /// <param name="encryptionInfo">Encryption information</param>
        /// <returns>Decrypted WordDocument stream</returns>
        public static byte[] DecryptWordDocument(byte[] encryptedData, string password, CryptoApiEncryptionInfo encryptionInfo)
        {
            // Word documents encrypt everything after FIB header
            const int FIB_SIZE = 68;
            
            if (encryptedData.Length <= FIB_SIZE)
                return encryptedData;

            var result = new byte[encryptedData.Length];
            
            // Copy unencrypted FIB header
            Array.Copy(encryptedData, 0, result, 0, FIB_SIZE);
            
            // Decrypt the rest
            if (encryptedData.Length <= FIB_SIZE) return result;
            var encryptedPortion = new byte[encryptedData.Length - FIB_SIZE];
            Array.Copy(encryptedData, FIB_SIZE, encryptedPortion, 0, encryptedPortion.Length);
                
            byte[] decryptedPortion = DecryptStream(encryptedPortion, password, encryptionInfo, FIB_SIZE);
            Array.Copy(decryptedPortion, 0, result, FIB_SIZE, decryptedPortion.Length);

            return result;
        }

        /// <summary>
        /// Creates CryptoAPI encryption information for new documents
        /// </summary>
        /// <param name="password">Password to use</param>
        /// <param name="hashAlgorithm">Hash algorithm</param>
        /// <returns>New encryption information</returns>
        public static CryptoApiEncryptionInfo CreateEncryptionInfo(string password, HashAlgorithmType hashAlgorithm = HashAlgorithmType.Md5)
        {
            var info = new CryptoApiEncryptionInfo
            {
                Version = 1,
                HashAlgorithm = hashAlgorithm
            };

            // Generate random salt
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(info.Salt);

            // Generate random verifier
            var verifier = new byte[16];
            rng.GetBytes(verifier);

            // Derive encryption key
            byte[] encryptionKey = DeriveEncryptionKey(password, info.Salt, hashAlgorithm);

            // Encrypt verifier
            info.EncryptedVerifier = LegacyRc4Handler.Rc4Transform(verifier, encryptionKey);

            // Hash verifier and encrypt the hash
            byte[] verifierHash = hashAlgorithm switch
            {
                HashAlgorithmType.Md5 => ComputeMd5Hash(verifier),
                HashAlgorithmType.Sha1 => ComputeSha1Hash(verifier),
                _ => throw new ArgumentException($"Unsupported hash algorithm: {hashAlgorithm}")
            };

            info.EncryptedVerifierHash = LegacyRc4Handler.Rc4Transform(verifierHash, encryptionKey);

            return info;
        }

        #region Helper Methods

        private static byte[] ComputeMd5Hash(byte[] data)
        {
            using var md5 = MD5.Create();
            return md5.ComputeHash(data);
        }

        private static byte[] ComputeSha1Hash(byte[] data)
        {
            using var sha1 = SHA1.Create();
            return sha1.ComputeHash(data);
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            var result = 0;
            for (var i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        #endregion
    }
}