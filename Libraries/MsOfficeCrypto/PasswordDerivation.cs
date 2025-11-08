using System;
using System.Security.Cryptography;
using System.Text;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;

namespace MsOfficeCrypto
{
    /// <summary>
    /// Corrected MS-OFFCRYPTO compliant password derivation and verification
    /// Implementation follows MS-OFFCRYPTO specification sections 2.3.4.7 and 2.3.4.9
    /// </summary>
    public static class PasswordDerivation
    {
        private const int STANDARD_ITERATION_COUNT = 50000;

        /// <summary>
        /// Derives the encryption key using MS-OFFCRYPTO Standard Encryption algorithm
        /// Per sections 2.3.4.7 and 2.3.4.9 of MS-OFFCRYPTO specification
        /// </summary>
        public static byte[] DeriveKey(string password, byte[] salt, int keySize = 128, uint blockNumber = 0)
        {
            try
            {
                Console.WriteLine($"Debug: Starting CORRECTED key derivation for password length {password.Length}, salt length {salt.Length}");
                
                // Step 1: Convert password to UTF-16LE bytes
                var passwordBytes = Encoding.Unicode.GetBytes(password);
                Console.WriteLine($"Debug: Password bytes length: {passwordBytes.Length}");
                
                // Step 2: Initial hash H_0 = SHA1(Salt + Password)
                var combined = new byte[salt.Length + passwordBytes.Length];
                Array.Copy(salt, 0, combined, 0, salt.Length);
                Array.Copy(passwordBytes, 0, combined, salt.Length, passwordBytes.Length);
                
                Console.WriteLine($"Debug: Combined data length: {combined.Length}");
                
                using var sha1 = SHA1.Create();
                var h0 = sha1.ComputeHash(combined);
                
                Console.WriteLine($"Debug: H0 (initial hash): {BitConverter.ToString(h0).Replace("-", "")}");
                
                // Step 3: Iterative hashing for 50,000 iterations
                // H_n = SHA1(iterator + H_{n-1})
                var currentHash = h0;
                
                for (uint i = 0; i < STANDARD_ITERATION_COUNT; i++)
                {
                    // H_{i+1} = SHA1(i_as_4bytes_little_endian + H_i)
                    var iterationData = new byte[4 + currentHash.Length];
                    
                    // Add iteration counter as little-endian 4-byte integer
                    var iterBytes = BitConverter.GetBytes(i);
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(iterBytes);
                    Array.Copy(iterBytes, 0, iterationData, 0, 4);
                    Array.Copy(currentHash, 0, iterationData, 4, currentHash.Length);
                    
                    currentHash = sha1.ComputeHash(iterationData);
                    
                    // Only log first few and last few iterations to avoid spam
                    if (i < 5 || i >= STANDARD_ITERATION_COUNT - 5)
                        Console.WriteLine($"Debug: H{i+1}: {BitConverter.ToString(currentHash).Replace("-", "")}");
                    else if (i == 5)
                        Console.WriteLine("Debug: ... (iterations 6 to 49995 hidden) ...");
                }
                
                // Step 4: Generate Hfinal = SHA1(H_final + blockNumber)
                var finalData = new byte[currentHash.Length + 4];
                Array.Copy(currentHash, 0, finalData, 0, currentHash.Length);
                
                var blockBytes = BitConverter.GetBytes(blockNumber);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(blockBytes);
                Array.Copy(blockBytes, 0, finalData, currentHash.Length, 4);
                
                var hFinal = sha1.ComputeHash(finalData);
                Console.WriteLine($"Debug: Hfinal with block {blockNumber}: {BitConverter.ToString(hFinal).Replace("-", "")}");
                
                // Step 5: CRITICAL - Missing HMAC-like key derivation from spec!
                // This is the key step missing in the original implementation
                var derivedKey = DeriveKeyFromHash(hFinal, keySize);
                
                Console.WriteLine($"Debug: Final derived key ({keySize/8} bytes): {BitConverter.ToString(derivedKey).Replace("-", "")}");
                
                return derivedKey;
            }
            catch (Exception ex)
            {
                throw new KeyDerivationException($"Failed to derive encryption key: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Performs the final key derivation step using HMAC-like construction
        /// Per MS-OFFCRYPTO specification section 2.3.4.7
        /// </summary>
        private static byte[] DeriveKeyFromHash(byte[] hFinal, int keySize)
        {
            var cbRequiredKeyLength = keySize / 8; // Convert bits to bytes
            var cbHash = hFinal.Length; // SHA-1 produces 20 bytes
            
            Console.WriteLine($"Debug: Starting final key derivation - required key length: {cbRequiredKeyLength}, hash length: {cbHash}");
            
            using var sha1 = SHA1.Create();
            
            // Step 1: Form 64-byte buffer with 0x36, XOR Hfinal into first cbHash bytes
            var buffer1 = new byte[64];
            for (var i = 0; i < 64; i++)
                buffer1[i] = 0x36;
            
            for (var i = 0; i < cbHash; i++)
                buffer1[i] ^= hFinal[i];
            
            var x1 = sha1.ComputeHash(buffer1);
            Console.WriteLine($"Debug: X1: {BitConverter.ToString(x1).Replace("-", "")}");
            
            // Step 2: Form 64-byte buffer with 0x5C, XOR Hfinal into first cbHash bytes  
            var buffer2 = new byte[64];
            for (var i = 0; i < 64; i++)
                buffer2[i] = 0x5C;
            
            for (var i = 0; i < cbHash; i++)
                buffer2[i] ^= hFinal[i];
            
            var x2 = sha1.ComputeHash(buffer2);
            Console.WriteLine($"Debug: X2: {BitConverter.ToString(x2).Replace("-", "")}");
            
            // Step 3: Concatenate X1 with X2 to form X3
            var x3 = new byte[x1.Length + x2.Length];
            Array.Copy(x1, 0, x3, 0, x1.Length);
            Array.Copy(x2, 0, x3, x1.Length, x2.Length);
            
            Console.WriteLine($"Debug: X3 (X1+X2): {BitConverter.ToString(x3).Replace("-", "")}");
            
            // Step 4: Take first cbRequiredKeyLength bytes as the derived key
            var derivedKey = new byte[cbRequiredKeyLength];
            Array.Copy(x3, 0, derivedKey, 0, Math.Min(cbRequiredKeyLength, x3.Length));
            
            return derivedKey;
        }

        /// <summary>
        /// Verifies password with corrected derivation method
        /// </summary>
        public static bool VerifyPassword(string password, EncryptionVerifier verifier, EncryptionHeader header)
        {
            if (verifier.Salt == null || verifier.EncryptedVerifier == null || verifier.EncryptedVerifierHash == null)
            {
                throw new ArgumentException("Verifier contains null data");
            }

            Console.WriteLine($"Debug: Starting CORRECTED password verification for '{password}'");
            
            try
            {
                // Use the corrected key derivation
                var derivedKey = DeriveKey(password, verifier.Salt, (int)header.KeySize);

                return TryVerifyWithKey(derivedKey, verifier, "Corrected Standard");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Corrected verification failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tests a derived key against the verifier
        /// </summary>
        private static bool TryVerifyWithKey(byte[] derivedKey, EncryptionVerifier verifier, string approach)
        {
            if (verifier.EncryptedVerifier is null || verifier.EncryptedVerifierHash is null)
            {
                return false;
            }
            try
            {
                Console.WriteLine($"Debug: Testing key from {approach}: {BitConverter.ToString(derivedKey).Replace("-", "")}");
                
                // Decrypt the verifier (should be 16 bytes)
                var decryptedVerifier = DecryptAesEcb(verifier.EncryptedVerifier, derivedKey);
                
                // The verifier should be exactly 16 bytes
                if (decryptedVerifier.Length != 16)
                {
                    Console.WriteLine($"Debug: {approach} - Incorrect verifier length: {decryptedVerifier.Length}, expected 16");
                    return false;
                }
                
                Console.WriteLine($"Debug: Decrypted verifier: {BitConverter.ToString(decryptedVerifier).Replace("-", "")}");
                
                // Hash the decrypted verifier using SHA-1
                using var sha1 = SHA1.Create();
                var verifierHash = sha1.ComputeHash(decryptedVerifier);
                Console.WriteLine($"Debug: Computed verifier hash: {BitConverter.ToString(verifierHash).Replace("-", "")}");
                
                // Decrypt the expected hash (should yield 32 bytes, but only first 20 are the hash)
                var decryptedExpectedHash = DecryptAesEcb(verifier.EncryptedVerifierHash, derivedKey);
                Console.WriteLine($"Debug: Decrypted expected hash: {BitConverter.ToString(decryptedExpectedHash).Replace("-", "")}");
                
                // Compare hashes (first 20 bytes for SHA-1)
                var compareLength = Math.Min(20, Math.Min(verifierHash.Length, decryptedExpectedHash.Length));
                
                for (var i = 0; i < compareLength; i++)
                {
                    if (verifierHash[i] == decryptedExpectedHash[i]) continue;
                    Console.WriteLine($"Debug: {approach} - Hash mismatch at byte {i}: expected {decryptedExpectedHash[i]:X2}, got {verifierHash[i]:X2}");
                    return false;
                }
                
                Console.WriteLine($"Debug: {approach} - Hash match successful!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: {approach} verification failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// AES ECB decryption for verifier data
        /// </summary>
        private static byte[] DecryptAesEcb(byte[] encryptedData, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None; // Standard encryption uses no padding
            aes.Key = EnsureKeySize(key, 128);
            
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }

        private static byte[] EnsureKeySize(byte[] key, int keyBits)
        {
            var requiredBytes = keyBits / 8;
            
            if (key.Length == requiredBytes)
                return key;
            
            var resizedKey = new byte[requiredBytes];
            Array.Copy(key, 0, resizedKey, 0, Math.Min(key.Length, requiredBytes));
            return resizedKey;
        }
    }
}