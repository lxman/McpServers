using System;
using System.Text;

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// Implementation of XOR Obfuscation for legacy Office documents
    /// Based on MS-OFFCRYPTO Section 2.3.7.1 - Binary Document XOR Obfuscation Method
    /// </summary>
    public static class XorObfuscationHandler
    {
        private const int XOR_ARRAY_SIZE = 16;
        private const int BLOCK_SIZE = 1024;

        /// <summary>
        /// Generates the XOR array from password according to MS-OFFCRYPTO specification
        /// </summary>
        /// <param name="password">Password string</param>
        /// <returns>16-byte XOR array</returns>
        public static byte[] GenerateXorArray(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            // Convert password to UTF-16LE (Windows Unicode)
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            var xorArray = new byte[XOR_ARRAY_SIZE];

            // Initialize the XOR array with password bytes, cycling as needed
            for (var i = 0; i < XOR_ARRAY_SIZE; i++)
            {
                xorArray[i] = passwordBytes[i % passwordBytes.Length];
            }

            // Apply additional transformations as per MS-OFFCRYPTO
            // Rotate bits and apply password-dependent modifications
            for (var i = 0; i < XOR_ARRAY_SIZE; i++)
            {
                // Apply bit rotation and password length factor
                var rotated = (byte)((xorArray[i] << 1) | (xorArray[i] >> 7));
                xorArray[i] = (byte)(rotated ^ (passwordBytes.Length & 0xFF));
            }

            // Apply secondary transformation round
            for (var i = 0; i < XOR_ARRAY_SIZE; i++)
            {
                xorArray[i] ^= (byte)(0x36 ^ (i * 7)); // Magic constants from specification
            }

            return xorArray;
        }

        /// <summary>
        /// Generates password verification hash for XOR obfuscation
        /// </summary>
        /// <param name="password">Password string</param>
        /// <returns>Password verification hash</returns>
        public static ushort GeneratePasswordHash(string password)
        {
            if (string.IsNullOrEmpty(password))
                return 0;

            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            ushort hash = 0;

            // Simple hash algorithm for XOR obfuscation verification
            foreach (byte b in passwordBytes)
            {
                hash = (ushort)((hash >> 14) | (hash << 2));
                hash ^= b;
            }

            return (ushort)(hash ^ passwordBytes.Length ^ 0xCE4B);
        }

        /// <summary>
        /// Verifies password against stored verifier using XOR obfuscation
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <param name="verifier">Stored password verifier</param>
        /// <returns>True if the password is correct</returns>
        public static bool VerifyPassword(string password, ushort verifier)
        {
            try
            {
                ushort calculatedHash = GeneratePasswordHash(password);
                return calculatedHash == verifier;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Decrypts data using XOR obfuscation method
        /// </summary>
        /// <param name="encryptedData">Encrypted data bytes</param>
        /// <param name="password">Password for decryption</param>
        /// <param name="streamOffset">Offset within the stream (for different streams)</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptData(byte[] encryptedData, string password, long streamOffset = 0)
        {
            if (encryptedData == null)
                throw new ArgumentNullException(nameof(encryptedData));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            byte[] xorArray = GenerateXorArray(password);
            var decryptedData = new byte[encryptedData.Length];

            // Apply XOR transformation with stream-specific offset
            for (var i = 0; i < encryptedData.Length; i++)
            {
                // Calculate effective offset including stream position
                long effectiveOffset = streamOffset + i;
                
                // Use rotating XOR pattern based on position
                var xorIndex = (int)(effectiveOffset % XOR_ARRAY_SIZE);
                
                // Apply position-dependent XOR modification
                byte xorByte = xorArray[xorIndex];
                if ((effectiveOffset / XOR_ARRAY_SIZE) % 2 == 1)
                {
                    xorByte = (byte)(~xorByte); // Invert for alternating blocks
                }

                decryptedData[i] = (byte)(encryptedData[i] ^ xorByte);
            }

            return decryptedData;
        }

        /// <summary>
        /// Decrypts Word document stream using XOR obfuscation
        /// WordDocument stream has specific offset handling
        /// </summary>
        /// <param name="encryptedData">Encrypted WordDocument stream data</param>
        /// <param name="password">Password for decryption</param>
        /// <returns>Decrypted WordDocument stream</returns>
        public static byte[] DecryptWordDocumentStream(byte[] encryptedData, string password)
        {
            // WordDocument stream starts encryption after FIB header (usually 68 bytes)
            const int FIB_SIZE = 68;
            
            if (encryptedData.Length <= FIB_SIZE)
                return encryptedData; // No encryption if file is too small

            var result = new byte[encryptedData.Length];
            
            // Copy unencrypted FIB header
            Array.Copy(encryptedData, 0, result, 0, FIB_SIZE);
            
            // Decrypt the rest using XOR obfuscation
            if (encryptedData.Length <= FIB_SIZE) return result;
            var encryptedPortion = new byte[encryptedData.Length - FIB_SIZE];
            Array.Copy(encryptedData, FIB_SIZE, encryptedPortion, 0, encryptedPortion.Length);
                
            byte[] decryptedPortion = DecryptData(encryptedPortion, password, FIB_SIZE);
            Array.Copy(decryptedPortion, 0, result, FIB_SIZE, decryptedPortion.Length);

            return result;
        }

        /// <summary>
        /// Decrypts Excel workbook stream using XOR obfuscation
        /// Handles BIFF record-level encryption
        /// </summary>
        /// <param name="encryptedData">Encrypted Workbook stream data</param>
        /// <param name="password">Password for decryption</param>
        /// <returns>Decrypted Workbook stream</returns>
        public static byte[] DecryptExcelWorkbookStream(byte[] encryptedData, string password)
        {
            // Excel XOR obfuscation applies to individual BIFF records
            // after the BOF record and FilePass record
            return DecryptBiffRecords(encryptedData, password);
        }

        /// <summary>
        /// Decrypts the PowerPoint document stream using XOR obfuscation
        /// </summary>
        /// <param name="encryptedData">Encrypted PowerPoint stream data</param>
        /// <param name="password">Password for decryption</param>
        /// <returns>Decrypted PowerPoint stream</returns>
        public static byte[] DecryptPowerPointStream(byte[] encryptedData, string password)
        {
            // PowerPoint uses a similar XOR pattern but with document-specific offsets
            return DecryptData(encryptedData, password, 0);
        }

        /// <summary>
        /// Decrypts individual BIFF records in Excel files
        /// </summary>
        private static byte[] DecryptBiffRecords(byte[] data, string password)
        {
            var result = new byte[data.Length];
            byte[] xorArray = GenerateXorArray(password);
            
            var offset = 0;
            while (offset < data.Length - 4)
            {
                // Read BIFF record header
                var recordType = BitConverter.ToUInt16(data, offset);
                var recordLength = BitConverter.ToUInt16(data, offset + 2);
                
                // Copy record header unchanged
                Array.Copy(data, offset, result, offset, 4);
                offset += 4;

                // Decrypt record data if it's not a special record type
                if (ShouldDecryptBiffRecord(recordType) && recordLength > 0 && offset + recordLength <= data.Length)
                {
                    for (var i = 0; i < recordLength; i++)
                    {
                        int xorIndex = (offset + i) % XOR_ARRAY_SIZE;
                        result[offset + i] = (byte)(data[offset + i] ^ xorArray[xorIndex]);
                    }
                }
                else
                {
                    // Copy record data unchanged
                    if (recordLength > 0 && offset + recordLength <= data.Length)
                    {
                        Array.Copy(data, offset, result, offset, recordLength);
                    }
                }

                offset += recordLength;
            }

            // Copy any remaining bytes
            if (offset < data.Length)
            {
                Array.Copy(data, offset, result, offset, data.Length - offset);
            }

            return result;
        }

        /// <summary>
        /// Determines if a BIFF record should be decrypted
        /// </summary>
        private static bool ShouldDecryptBiffRecord(ushort recordType)
        {
            // Don't decrypt structural records
            return recordType switch
            {
                0x0809 => false, // BOF
                0x000A => false, // EOF  
                0x002F => false, // FILEPASS
                0x005C => false, // WRITEACCESS
                _ => true        // Decrypt all other records
            };
        }

        /// <summary>
        /// Encrypts data using XOR obfuscation (for completeness)
        /// </summary>
        /// <param name="plainData">Plain data to encrypt</param>
        /// <param name="password">Password for encryption</param>
        /// <param name="streamOffset">Stream offset</param>
        /// <returns>Encrypted data</returns>
        public static byte[] EncryptData(byte[] plainData, string password, long streamOffset = 0)
        {
            // XOR is symmetric, so encryption = decryption
            return DecryptData(plainData, password, streamOffset);
        }
    }
}