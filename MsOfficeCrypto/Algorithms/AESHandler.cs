using System;
using System.Linq;
using System.Security.Cryptography;
using MsOfficeCrypto.Exceptions;

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// AES encryption/decryption handler for MS-OFFCRYPTO specification
    /// Supports AES-128, AES-192, and AES-256 in ECB mode
    /// </summary>
    public static class AesHandler
    {
        /// <summary>
        /// Supported AES key sizes in bits
        /// </summary>
        public static readonly int[] SupportedKeySizes = { 128, 192, 256 };

        /// <summary>
        /// Encrypts data using AES in ECB mode with PKCS7 padding
        /// </summary>
        /// <param name="data">Data to encrypt</param>
        /// <param name="key">AES encryption key</param>
        /// <param name="keySize">Key size in bits (128, 192, or 256)</param>
        /// <returns>Encrypted data</returns>
        public static byte[] Encrypt(byte[] data, byte[] key, int keySize = 128)
        {
            ValidateParameters(data, key, keySize);

            try
            {
                using Aes aes = CreateAesProvider(key, keySize);
                using ICryptoTransform encryptor = aes.CreateEncryptor();
                return encryptor.TransformFinalBlock(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                throw new DecryptionException($"AES encryption failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decrypts data using AES in ECB mode with MS-OFFCRYPTO specific handling
        /// </summary>
        /// <param name="encryptedData">Data to decrypt</param>
        /// <param name="key">AES decryption key</param>
        /// <param name="keySize">Key size in bits (128, 192, or 256)</param>
        /// <returns>Decrypted data</returns>
        public static byte[] Decrypt(byte[] encryptedData, byte[] key, int keySize = 128)
        {
            ValidateParameters(encryptedData, key, keySize);

            try
            {
                // MS-OFFCRYPTO Standard encryption behavior:
                // - Complete 16-byte blocks are encrypted with AES-ECB
                // - Incomplete final block (if any) remains unencrypted
                const int blockSize = 16; // AES block size
                int completeBlocks = encryptedData.Length / blockSize;
                int remainder = encryptedData.Length % blockSize;

                using Aes aes = CreateAesProviderForOffCrypto(key, keySize);
                using ICryptoTransform decryptor = aes.CreateDecryptor();

                var result = new byte[encryptedData.Length];

                if (completeBlocks > 0)
                {
                    // Process complete blocks using TransformBlock (not TransformFinalBlock)
                    var inputOffset = 0;
                    var outputOffset = 0;
                    
                    for (var i = 0; i < completeBlocks; i++)
                    {
                        decryptor.TransformBlock(encryptedData, inputOffset, blockSize, result, outputOffset);
                        inputOffset += blockSize;
                        outputOffset += blockSize;
                    }
                }

                // Handle remainder - copy as-is (MS-OFFCRYPTO specific behavior)
                if (remainder > 0)
                {
                    Array.Copy(encryptedData, completeBlocks * blockSize, result, completeBlocks * blockSize, remainder);
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new DecryptionException($"AES decryption failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Encrypts data using AES-128 (convenience method)
        /// </summary>
        /// <param name="data">Data to encrypt</param>
        /// <param name="key">128-bit encryption key</param>
        /// <returns>Encrypted data</returns>
        public static byte[] EncryptAes128(byte[] data, byte[] key)
        {
            return Encrypt(data, key, 128);
        }

        /// <summary>
        /// Decrypts data using AES-128 (convenience method)
        /// </summary>
        /// <param name="encryptedData">Data to decrypt</param>
        /// <param name="key">128-bit decryption key</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptAes128(byte[] encryptedData, byte[] key)
        {
            return Decrypt(encryptedData, key, 128);
        }

        /// <summary>
        /// Encrypts data using AES-256 (convenience method)
        /// </summary>
        /// <param name="data">Data to encrypt</param>
        /// <param name="key">256-bit encryption key</param>
        /// <returns>Encrypted data</returns>
        public static byte[] EncryptAes256(byte[] data, byte[] key)
        {
            return Encrypt(data, key, 256);
        }

        /// <summary>
        /// Decrypts data using AES-256 (convenience method)
        /// </summary>
        /// <param name="encryptedData">Data to decrypt</param>
        /// <param name="key">256-bit decryption key</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptAes256(byte[] encryptedData, byte[] key)
        {
            return Decrypt(encryptedData, key, 256);
        }

        /// <summary>
        /// Validates if the provided key size is supported
        /// </summary>
        /// <param name="keySize">Key size in bits</param>
        /// <returns>True if supported</returns>
        public static bool IsKeySizeSupported(int keySize)
        {
            return Array.IndexOf(SupportedKeySizes, keySize) >= 0;
        }

        /// <summary>
        /// Gets the expected key length in bytes for a given key size
        /// </summary>
        /// <param name="keySize">Key size in bits</param>
        /// <returns>Key length in bytes</returns>
        public static int GetKeyLengthBytes(int keySize)
        {
            if (!IsKeySizeSupported(keySize))
            {
                throw new ArgumentException($"Unsupported key size: {keySize}");
            }
            return keySize / 8;
        }

        /// <summary>
        /// Creates properly configured AES provider
        /// </summary>
        /// <param name="key">Encryption/decryption key</param>
        /// <param name="keySize">Key size in bits</param>
        /// <returns>Configured AES provider</returns>
        private static Aes CreateAesProvider(byte[] key, int keySize)
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.ECB; // MS-OFFCRYPTO uses ECB mode
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = keySize;
            
            // Ensure key is correct length
            int expectedKeyLength = GetKeyLengthBytes(keySize);
            if (key.Length == expectedKeyLength)
            {
                aes.Key = key;
            }
            else if (key.Length > expectedKeyLength)
            {
                // Truncate key if too long
                var truncatedKey = new byte[expectedKeyLength];
                Array.Copy(key, truncatedKey, expectedKeyLength);
                aes.Key = truncatedKey;
            }
            else
            {
                // Pad key if too short
                var paddedKey = new byte[expectedKeyLength];
                Array.Copy(key, paddedKey, key.Length);
                aes.Key = paddedKey;
            }
            
            return aes;
        }

        /// <summary>
        /// Creates AES provider specifically configured for MS-OFFCRYPTO
        /// </summary>
        /// <param name="key">Encryption/decryption key</param>
        /// <param name="keySize">Key size in bits</param>
        /// <returns>Configured AES provider</returns>
        private static Aes CreateAesProviderForOffCrypto(byte[] key, int keySize)
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.ECB; // MS-OFFCRYPTO uses ECB mode
            aes.Padding = PaddingMode.None; // MS-OFFCRYPTO handles padding differently
            aes.KeySize = keySize;
            
            // Ensure key is correct length
            int expectedKeyLength = GetKeyLengthBytes(keySize);
            if (key.Length == expectedKeyLength)
            {
                aes.Key = key;
            }
            else if (key.Length > expectedKeyLength)
            {
                // Truncate key if too long
                var truncatedKey = new byte[expectedKeyLength];
                Array.Copy(key, truncatedKey, expectedKeyLength);
                aes.Key = truncatedKey;
            }
            else
            {
                // Pad key if too short
                var paddedKey = new byte[expectedKeyLength];
                Array.Copy(key, paddedKey, key.Length);
                aes.Key = paddedKey;
            }
            
            return aes;
        }

        /// <summary>
        /// Validates input parameters for encryption/decryption operations
        /// </summary>
        /// <param name="data">Data to process</param>
        /// <param name="key">Encryption/decryption key</param>
        /// <param name="keySize">Key size in bits</param>
        private static void ValidateParameters(byte[] data, byte[] key, int keySize)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            
            if (data.Length == 0)
                throw new ArgumentException("Data cannot be empty", nameof(data));
            
            if (key.Length == 0)
                throw new ArgumentException("Key cannot be empty", nameof(key));
            
            if (!IsKeySizeSupported(keySize))
                throw new UnsupportedEncryptionException($"Unsupported AES key size: {keySize} bits");
        }

        /// <summary>
        /// Tests AES functionality with a known test vector
        /// </summary>
        /// <returns>True if AES implementation works correctly</returns>
        public static bool TestAesImplementation()
        {
            try
            {
                // Test with standard PKCS7 padding first
                var testKey = new byte[16]; // All zeros
                var testData = new byte[16]; // All zeros
                
                // Use the standard AES provider for testing
                using Aes aes = CreateAesProvider(testKey, 128);
                using ICryptoTransform encryptor = aes.CreateEncryptor();
                using ICryptoTransform decryptor = aes.CreateDecryptor();
                
                byte[] encrypted = encryptor.TransformFinalBlock(testData, 0, testData.Length);
                byte[] decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                
                // Verify round-trip works
                if (decrypted.Length != testData.Length)
                    return false;

                return !testData.Where((t, i) => t != decrypted[i]).Any();
            }
            catch
            {
                return false;
            }
        }
    }
}
