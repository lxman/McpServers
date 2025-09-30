using System;
using System.IO;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// PowerPoint-specific RC4 CryptoAPI encryption handler
    /// Based on MS-OFFCRYPTO Section 2.3.7.4 - Binary Document RC4 CryptoAPI Encryption Method 3
    /// Handles CryptSession10Container parsing and PowerPoint stream decryption
    /// </summary>
    public static class PowerPointRc4CryptoApiHandler
    {
        // PowerPoint record type constants
        private const ushort CURRENT_USER_ATOM = 0x0FF6;
        private const ushort CRYPT_SESSION10_CONTAINER = 0x2F14;
        private const uint ENCRYPTED_HEADER_TOKEN = 0xF3D1C4DF;

        /// <summary>
        /// Parses CryptSession10Container from PowerPoint Current User stream
        /// </summary>
        /// <param name="currentUserStream">Current User stream data</param>
        /// <returns>Parsed CryptoAPI encryption information</returns>
        public static CryptoApiEncryptionInfo ParseCryptSessionContainer(byte[] currentUserStream)
        {
            if (currentUserStream == null || currentUserStream.Length < 20)
                throw new ArgumentException("Invalid Current User stream data", nameof(currentUserStream));

            using var reader = new BinaryReader(new MemoryStream(currentUserStream));

            // Read CurrentUserAtom header
            ushort recordType = reader.ReadUInt16();
            ushort recordVersion = reader.ReadUInt16();
            uint recordLength = reader.ReadUInt32();

            if (recordType != CURRENT_USER_ATOM)
                throw new CorruptedEncryptionInfoException(
                    $"Expected CurrentUserAtom (0x{CURRENT_USER_ATOM:X4}), found 0x{recordType:X4}");

            // Read header token
            uint headerToken = reader.ReadUInt32();
            
            if (headerToken != ENCRYPTED_HEADER_TOKEN)
            {
                // Not encrypted with RC4 CryptoAPI
                throw new NotEncryptedException(
                    "Current User stream does not contain encrypted header token");
            }

            // Skip to find CryptSession10Container
            // The container may be embedded within the stream
            return FindAndParseCryptSession(reader);
        }

        /// <summary>
        /// Searches for and parses CryptSession10Container within the stream
        /// </summary>
        private static CryptoApiEncryptionInfo FindAndParseCryptSession(BinaryReader reader)
        {
            long startPos = reader.BaseStream.Position;
            long maxSearchBytes = Math.Min(reader.BaseStream.Length - startPos, 8192);

            while (reader.BaseStream.Position < startPos + maxSearchBytes)
            {
                if (reader.BaseStream.Position + 8 > reader.BaseStream.Length)
                    break;

                ushort recordType = reader.ReadUInt16();
                ushort recordVersion = reader.ReadUInt16();
                uint recordLength = reader.ReadUInt32();

                if (recordType == CRYPT_SESSION10_CONTAINER)
                {
                    // Found the container, parse it
                    return ParseCryptSessionData(reader, recordLength);
                }

                // Move to next potential record
                if (recordLength > 0 && recordLength < 1048576 && 
                    reader.BaseStream.Position + recordLength <= reader.BaseStream.Length)
                {
                    reader.BaseStream.Position += recordLength;
                }
                else
                {
                    // Try next byte
                    reader.BaseStream.Position -= 6;
                    reader.BaseStream.Position += 1;
                }
            }

            throw new CorruptedEncryptionInfoException(
                "CryptSession10Container not found in Current User stream");
        }

        /// <summary>
        /// Parses the CryptSession10Container data according to MS-OFFCRYPTO Section 2.3.5.1
        /// Enhanced to properly handle the RC4 CryptoAPI Encryption Header format
        /// </summary>
        private static CryptoApiEncryptionInfo ParseCryptSessionData(BinaryReader reader, uint containerLength)
        {
            var info = new CryptoApiEncryptionInfo();

            if (containerLength < 52) // Minimum: EncryptionVersionInfo(4) + Flags(4) + HeaderSize(4) + minimal header + verifier
                throw new CorruptedEncryptionInfoException(
                    $"CryptSession10Container too small: {containerLength} bytes");

            long startPosition = reader.BaseStream.Position;

            // Read EncryptionVersionInfo (4 bytes) - MS-OFFCRYPTO 2.3.5.1
            ushort vMajor = reader.ReadUInt16();
            ushort vMinor = reader.ReadUInt16();
            
            if (vMajor < 0x0002 || vMajor > 0x0004)
                throw new CorruptedEncryptionInfoException(
                    $"Unsupported encryption version: {vMajor}.{vMinor}");
            
            info.Version = (uint)(vMajor << 16 | vMinor);

            // Read EncryptionHeader.Flags (4 bytes)
            uint flags = reader.ReadUInt32();
            bool fCryptoApi = (flags & 0x04) != 0;
            bool fDocProps = (flags & 0x08) != 0;
            
            if (!fCryptoApi)
                throw new CorruptedEncryptionInfoException(
                    "Expected fCryptoAPI flag to be set in EncryptionHeader.Flags");

            // Read EncryptionHeaderSize (4 bytes)
            uint headerSize = reader.ReadUInt32();
            
            if (headerSize < 32) // Minimum header size
                throw new CorruptedEncryptionInfoException(
                    $"Invalid EncryptionHeaderSize: {headerSize}");

            long headerStart = reader.BaseStream.Position;

            // Read EncryptionHeader fields - MS-OFFCRYPTO Section 2.3.2
            uint headerFlags = reader.ReadUInt32(); // Flags (duplicate)
            uint sizeExtra = reader.ReadUInt32();   // MUST be 0x00000000
            uint algId = reader.ReadUInt32();        // MUST be 0x00006801 (RC4)
            uint algIdHash = reader.ReadUInt32();    // MUST be 0x00008004 (SHA-1) or MD5
            uint keySize = reader.ReadUInt32();      // Key size in bits
            uint providerType = reader.ReadUInt32(); // MUST be 0x00000001
            uint reserved1 = reader.ReadUInt32();    // Ignored
            uint reserved2 = reader.ReadUInt32();    // MUST be 0x00000000

            // Validate algorithm IDs
            if (algId != 0x00006801)
                throw new CorruptedEncryptionInfoException(
                    $"Expected RC4 algorithm (0x6801), found 0x{algId:X8}");

            // Determine hash algorithm from algIDHash
            info.HashAlgorithm = algIdHash switch
            {
                0x00008003 => HashAlgorithmType.Md5,   // MD5
                0x00008004 => HashAlgorithmType.Sha1,  // SHA-1
                _ => throw new CorruptedEncryptionInfoException(
                    $"Unsupported hash algorithm: 0x{algIdHash:X8}")
            };

            // Read CSPName (null-terminated Unicode string)
            // Skip to the end of the header
            long remainingHeaderBytes = headerSize - (reader.BaseStream.Position - headerStart);
            if (remainingHeaderBytes > 0)
                reader.ReadBytes((int)remainingHeaderBytes);

            // Read EncryptionVerifier - MS-OFFCRYPTO Section 2.3.3
            // Salt size (4 bytes)
            uint saltSize = reader.ReadUInt32();
            if (saltSize != 16)
                throw new CorruptedEncryptionInfoException(
                    $"Expected salt size of 16 bytes, found {saltSize}");

            // Salt (16 bytes)
            info.Salt = reader.ReadBytes(CryptoApiEncryptionInfo.SALT_SIZE);

            // Encrypted verifier (16 bytes)
            info.EncryptedVerifier = reader.ReadBytes(16);

            // Verifier hash size (4 bytes)
            uint verifierHashSize = reader.ReadUInt32();
            
            // Read encrypted verifier hash
            if (verifierHashSize >= 20)
            {
                info.EncryptedVerifierHash = reader.ReadBytes(20);
                // If SHA-1 was specified but we got more bytes, still use SHA-1
                if (info.HashAlgorithm == HashAlgorithmType.Sha1 && verifierHashSize > 20)
                    reader.ReadBytes((int)(verifierHashSize - 20)); // Skip padding
            }
            else if (verifierHashSize >= 16)
            {
                info.EncryptedVerifierHash = reader.ReadBytes(16);
                info.HashAlgorithm = HashAlgorithmType.Md5;
            }
            else
            {
                throw new CorruptedEncryptionInfoException(
                    $"Invalid verifier hash size: {verifierHashSize}");
            }

            return info;
        }

        /// <summary>
        /// Decrypts a PowerPoint Current User stream
        /// </summary>
        /// <param name="encryptedStream">Encrypted Current User stream</param>
        /// <param name="password">Password</param>
        /// <param name="encryptionInfo">Encryption information from CryptSession</param>
        /// <returns>Decrypted Current User stream</returns>
        public static byte[] DecryptCurrentUserStream(
            byte[] encryptedStream, 
            string password, 
            CryptoApiEncryptionInfo encryptionInfo)
        {
            // CurrentUserAtom is not encrypted, only the document content is
            // For now, return as-is since the encryption applies to other streams
            // The actual decryption happens in the PowerPoint Document stream
            return encryptedStream ?? throw new ArgumentNullException(nameof(encryptedStream));
        }

        /// <summary>
        /// Decrypts a PowerPoint Document stream using persist object-based decryption
        /// PowerPoint uses persist object identifiers as block numbers, NOT 512-byte offsets
        /// Based on MS-PPT Section 2.3.7 - CryptSession10Container
        /// </summary>
        /// <param name="encryptedStream">Encrypted PowerPoint Document stream</param>
        /// <param name="password">Password</param>
        /// <param name="encryptionInfo">Encryption information from CryptSession</param>
        /// <param name="persistObjectId">Persist object identifier to use as block number</param>
        /// <returns>Decrypted PowerPoint Document stream</returns>
        public static byte[] DecryptPowerPointDocumentStream(
            byte[] encryptedStream,
            string password,
            CryptoApiEncryptionInfo encryptionInfo,
            uint persistObjectId = 0)
        {
            if (encryptedStream == null)
                throw new ArgumentNullException(nameof(encryptedStream));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            // Verify password first
            if (!EnhancedRc4CryptoApiHandler.VerifyPassword(password, encryptionInfo))
                throw new InvalidPasswordException("Invalid password for PowerPoint document");

            // PowerPoint uses persist object identifier as block number
            return DecryptPowerPointPersistObject(
                encryptedStream, 
                password, 
                encryptionInfo, 
                persistObjectId);
        }

        /// <summary>
        /// Decrypts a PowerPoint persist object using its identifier as the block number
        /// MS-PPT Section 2.3.7: "the block number for the derived encryption key is the persist object identifier"
        /// </summary>
        /// <param name="encryptedData">Encrypted persist object data</param>
        /// <param name="password">Password</param>
        /// <param name="encryptionInfo">Encryption information</param>
        /// <param name="persistObjectId">Persist object identifier (used as the block number)</param>
        /// <returns>Decrypted persist object data</returns>
        public static byte[] DecryptPowerPointPersistObject(
            byte[] encryptedData,
            string password,
            CryptoApiEncryptionInfo encryptionInfo,
            uint persistObjectId)
        {
            if (encryptedData == null)
                throw new ArgumentNullException(nameof(encryptedData));

            // Derive base encryption key from password and salt
            byte[] baseKey = EnhancedRc4CryptoApiHandler.DeriveEncryptionKey(
                password, 
                encryptionInfo.Salt, 
                encryptionInfo.HashAlgorithm);

            // For PowerPoint: block number = persist object identifier
            // NOT the 512-byte offset like Word/Excel
            byte[] blockKey = EnhancedRc4CryptoApiHandler.DeriveBlockKey(baseKey, persistObjectId);

            // Decrypt the entire persist object with the block-specific key
            return LegacyRc4Handler.Rc4Transform(encryptedData, blockKey);
        }

        /// <summary>
        /// Decrypts a PowerPoint Pictures stream
        /// MS-PPT Section 2.3.7: Pictures use block number = 0
        /// </summary>
        /// <param name="encryptedPictures">Encrypted Pictures stream</param>
        /// <param name="password">Password</param>
        /// <param name="encryptionInfo">Encryption information</param>
        /// <returns>Decrypted Pictures stream</returns>
        public static byte[] DecryptPicturesStream(
            byte[] encryptedPictures,
            string password,
            CryptoApiEncryptionInfo encryptionInfo)
        {
            if (encryptedPictures == null)
                throw new ArgumentNullException(nameof(encryptedPictures));

            // MS-PPT Section 2.3.7: "The derived encryption key MUST be generated 
            // from the password hash and a block number equal to zero"
            return DecryptPowerPointPersistObject(
                encryptedPictures, 
                password, 
                encryptionInfo, 
                persistObjectId: 0);
        }

        /// <summary>
        /// Decrypts PowerPoint Encrypted Summary Info stream
        /// Uses the RC4 CryptoAPI Encrypted Summary Stream format (MS-OFFCRYPTO 2.3.5.4)
        /// </summary>
        /// <param name="encryptedSummaryStream">Encrypted Summary Info stream</param>
        /// <param name="password">Password</param>
        /// <param name="encryptionInfo">Encryption information</param>
        /// <returns>Decrypted Summary Info stream</returns>
        public static byte[] DecryptEncryptedSummaryStream(
            byte[] encryptedSummaryStream,
            string password,
            CryptoApiEncryptionInfo encryptionInfo)
        {
            if (encryptedSummaryStream == null)
                throw new ArgumentNullException(nameof(encryptedSummaryStream));
            if (encryptedSummaryStream.Length < 16)
                throw new ArgumentException("Encrypted Summary stream too small");

            using var reader = new BinaryReader(new MemoryStream(encryptedSummaryStream));
            
            // Read header (MS-OFFCRYPTO Section 2.3.5.4)
            uint streamDescriptorArrayOffset = reader.ReadUInt32();
            uint streamDescriptorArraySize = reader.ReadUInt32();

            // Validate offsets
            if (streamDescriptorArrayOffset >= encryptedSummaryStream.Length)
                throw new CorruptedEncryptionInfoException("Invalid stream descriptor array offset");

            // For Summary Info, typically uses block number 0
            // The actual stream data starts after the header
            var encryptedData = new byte[streamDescriptorArrayOffset - 8];
            Array.Copy(encryptedSummaryStream, 8, encryptedData, 0, encryptedData.Length);

            return DecryptPowerPointPersistObject(
                encryptedData, 
                password, 
                encryptionInfo, 
                persistObjectId: 0);
        }

        /// <summary>
        /// Decrypts a PowerPoint Summary Information stream
        /// </summary>
        /// <param name="encryptedStream">Encrypted Summary Information stream</param>
        /// <param name="password">Password</param>
        /// <param name="encryptionInfo">Encryption information</param>
        /// <returns>Decrypted Summary Information stream</returns>
        public static byte[] DecryptSummaryInfoStream(
            byte[] encryptedStream,
            string password,
            CryptoApiEncryptionInfo encryptionInfo)
        {
            if (encryptedStream == null)
                throw new ArgumentNullException(nameof(encryptedStream));

            // Summary Information uses the same encryption as the document
            return EnhancedRc4CryptoApiHandler.DecryptStream(
                encryptedStream,
                password,
                encryptionInfo,
                streamOffset: 0);
        }

        /// <summary>
        /// Extracts encryption info from the Current User stream
        /// Wrapper method for easier integration
        /// </summary>
        /// <param name="currentUserStream">Current User stream data</param>
        /// <returns>Extracted encryption information</returns>
        public static CryptoApiEncryptionInfo ExtractEncryptionInfo(byte[] currentUserStream)
        {
            return ParseCryptSessionContainer(currentUserStream);
        }

        /// <summary>
        /// Checks if a Current User stream contains RC4 CryptoAPI encryption
        /// </summary>
        /// <param name="currentUserStream">Current User stream data</param>
        /// <returns>True if encrypted with RC4 CryptoAPI</returns>
        public static bool IsRc4CryptoApiEncrypted(byte[] currentUserStream)
        {
            if (currentUserStream.Length < 12)
                return false;

            try
            {
                using var reader = new BinaryReader(new MemoryStream(currentUserStream));

                ushort recordType = reader.ReadUInt16();
                ushort recordVersion = reader.ReadUInt16();
                uint recordLength = reader.ReadUInt32();

                if (recordType != CURRENT_USER_ATOM)
                    return false;

                uint headerToken = reader.ReadUInt32();
                return headerToken == ENCRYPTED_HEADER_TOKEN;
            }
            catch
            {
                return false;
            }
        }
    }
}
