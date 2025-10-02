using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MsOfficeCrypto.Exceptions;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Parses binary EncryptionInfo stream data into structured objects
    /// Based on MS-OFFCRYPTO specification sections 2.3.4.5 and 2.3.4.6
    /// </summary>
    public static class EncryptionInfoParser
    {
        /// <summary>
        /// Parses EncryptionInfo stream data into header and verifier structures
        /// </summary>
        /// <param name="encryptionInfoData">Raw EncryptionInfo stream data (224 bytes for your test case)</param>
        /// <param name="versionInfo">Version information</param>
        /// <returns>Tuple of parsed header and verifier</returns>
        public static (EncryptionHeader header, EncryptionVerifier verifier) Parse(
            byte[] encryptionInfoData, 
            VersionInfo versionInfo)
        {
            if (encryptionInfoData == null || encryptionInfoData.Length < 8)
                throw new CorruptedEncryptionInfoException("EncryptionInfo data is too small");

            // Route to the appropriate parser based on the encryption type
            return versionInfo.GetEncryptionType() switch
            {
                "ECMA-376 Standard" => ParseEcma376Standard(encryptionInfoData, versionInfo),
                "ECMA-376 Agile" => ParseEcma376Agile(encryptionInfoData, versionInfo),
                _ => throw new UnsupportedEncryptionException(versionInfo.GetEncryptionType(), 
                    $"Parsing not implemented for {versionInfo.GetEncryptionType()}")
            };
        }

        /// <summary>
        /// Parses ECMA-376 Standard encryption (your test case: version 3.2, 224 bytes)
        /// Based on MS-OFFCRYPTO sections 2.3.4.5 and 2.3.4.6
        /// Fixed based on actual binary structure analysis
        /// </summary>
        private static (EncryptionHeader header, EncryptionVerifier verifier) ParseEcma376Standard(
            byte[] data, VersionInfo versionInfo)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            
            // Skip VersionInfo (already parsed)
            reader.BaseStream.Position = 4;

            Console.WriteLine($"Debug: Starting header parse at offset {reader.BaseStream.Position}");

            // Parse EncryptionHeader - CORRECTED structure based on hex analysis
            var header = new EncryptionHeader();
            
            // CORRECTED: Read HeaderSize first (this was missing!)
            uint headerSize = reader.ReadUInt32(); // Offset 4: HeaderSize = 36
            Console.WriteLine($"Debug: HeaderSize = {headerSize} (at offset {reader.BaseStream.Position - 4})");
            
            header.Flags = reader.ReadUInt32(); // Offset 8: Flags = 140
            Console.WriteLine($"Debug: Flags = 0x{header.Flags:X8} (at offset {reader.BaseStream.Position - 4})");
            
            header.SizeExtra = reader.ReadUInt32(); // Offset 12: SizeExtra = 36
            Console.WriteLine($"Debug: SizeExtra = {header.SizeExtra} (at offset {reader.BaseStream.Position - 4})");
            
            uint reserved = reader.ReadUInt32(); // Offset 16: Reserved = 0
            Console.WriteLine($"Debug: Reserved = 0x{reserved:X8} (at offset {reader.BaseStream.Position - 4})");
            
            header.AlgId = reader.ReadUInt32(); // Offset 20: AlgID = 0x0000660E (AES-128)
            Console.WriteLine($"Debug: AlgID = 0x{header.AlgId:X8} ({header.GetAlgorithmName()}) (at offset {reader.BaseStream.Position - 4})");
            
            header.AlgIdHash = reader.ReadUInt32(); // Offset 24: AlgIDHash = 0x00008004 (SHA1)
            Console.WriteLine($"Debug: AlgIDHash = 0x{header.AlgIdHash:X8} ({header.GetHashAlgorithmName()}) (at offset {reader.BaseStream.Position - 4})");
            
            header.KeySize = reader.ReadUInt32(); // Offset 28: KeySize = 128
            Console.WriteLine($"Debug: KeySize = {header.KeySize} (at offset {reader.BaseStream.Position - 4})");
            
            header.ProviderType = reader.ReadUInt32(); // Offset 32: ProviderType = 24
            Console.WriteLine($"Debug: ProviderType = 0x{header.ProviderType:X8} (at offset {reader.BaseStream.Position - 4})");
            
            header.Reserved1 = reader.ReadUInt32(); // Offset 36: Reserved1 = 0
            Console.WriteLine($"Debug: Reserved1 = 0x{header.Reserved1:X8} (at offset {reader.BaseStream.Position - 4})");
            
            header.Reserved2 = reader.ReadUInt32(); // Offset 40: Reserved2 = 0
            Console.WriteLine($"Debug: Reserved2 = 0x{header.Reserved2:X8} (at offset {reader.BaseStream.Position - 4})");

            Console.WriteLine($"Debug: Current position after basic header: {reader.BaseStream.Position}");

            // Parse CSP Name (variable length Unicode string)
            // The CSP name might be longer than SizeExtra indicates
            // Read until we find the start of the verifier (reasonable salt size)
            Console.WriteLine($"Debug: Reading CSP name starting at offset {reader.BaseStream.Position}");
            
            // Read the full CSP name by looking for null termination
            var cspBytes = new List<byte>();
            long cspStartPosition = reader.BaseStream.Position;
            
            // Read Unicode characters until we find the end of the CSP name
            // and the start of the verifier (salt size between 1-64)
            while (reader.BaseStream.Position < data.Length - 4)
            {
                long currentPos = reader.BaseStream.Position;
                
                // Check if this might be the start of the verifier
                uint potentialSaltSize = reader.ReadUInt32();
                
                if (potentialSaltSize > 0 && potentialSaltSize <= 64)
                {
                    // This looks like a valid salt size - we found the verifier start
                    Console.WriteLine($"Debug: Found potential verifier start at offset {currentPos}, SaltSize = {potentialSaltSize}");
                    
                    // Extract CSP name from what we've collected so far
                    if (cspBytes.Count > 0)
                    {
                        header.CspName = Encoding.Unicode.GetString(cspBytes.ToArray()).TrimEnd('\0');
                        Console.WriteLine($"Debug: Full CSP Name = '{header.CspName}'");
                    }
                    
                    // Reset position to start of verifier
                    reader.BaseStream.Position = currentPos;
                    break;
                }
                // Not the verifier yet, add this byte to CSP name and continue
                reader.BaseStream.Position = currentPos;
                cspBytes.Add(reader.ReadByte());
            }

            // Store raw header data for debugging
            long headerEndPosition = reader.BaseStream.Position;
            header.RawData = data[4..(int)headerEndPosition];
            Console.WriteLine($"Debug: Header ends at position {headerEndPosition}");

            // Parse EncryptionVerifier (MS-OFFCRYPTO 2.3.4.6)
            Console.WriteLine($"Debug: Starting verifier parse at offset {reader.BaseStream.Position}");
            
            var verifier = new EncryptionVerifier
            {
                SaltSize = reader.ReadUInt32()
            };
            Console.WriteLine($"Debug: SaltSize = {verifier.SaltSize} (at offset {reader.BaseStream.Position - 4})");

            // Read Salt
            if (verifier.SaltSize > 0 && verifier.SaltSize <= 64) // Sanity check
            {
                verifier.Salt = reader.ReadBytes((int)verifier.SaltSize);
                Console.WriteLine($"Debug: Salt = {BitConverter.ToString(verifier.Salt).Replace("-", "")}");
            }
            else
            {
                throw new CorruptedEncryptionInfoException($"Invalid salt size: {verifier.SaltSize} at offset {reader.BaseStream.Position - 4}");
            }

            // Read EncryptedVerifier (always 16 bytes for ECMA-376 Standard)
            verifier.EncryptedVerifier = reader.ReadBytes(16);
            Console.WriteLine($"Debug: EncryptedVerifier = {BitConverter.ToString(verifier.EncryptedVerifier).Replace("-", "")}");

            // Read VerifierHashSize
            verifier.VerifierHashSize = reader.ReadUInt32();
            Console.WriteLine($"Debug: VerifierHashSize = {verifier.VerifierHashSize} (at offset {reader.BaseStream.Position - 4})");

            // Read EncryptedVerifierHash
            if (verifier.VerifierHashSize > 0 && verifier.VerifierHashSize <= 64) // Sanity check
            {
                int paddedHashSize = ((int)verifier.VerifierHashSize + 15) / 16 * 16;
                verifier.EncryptedVerifierHash = reader.ReadBytes(paddedHashSize);
                Console.WriteLine($"Debug: EncryptedVerifierHash = {BitConverter.ToString(verifier.EncryptedVerifierHash).Replace("-", "")}");
            }
            else
            {
                throw new CorruptedEncryptionInfoException($"Invalid verifier hash size: {verifier.VerifierHashSize}");
            }

            // Store raw verifier data for debugging
            verifier.RawData = data[(int)headerEndPosition..];

            Console.WriteLine($"Debug: Parse completed. Final position: {reader.BaseStream.Position}");
            return (header, verifier);
        }

        /// <summary>
        /// Placeholder for ECMA-376 Agile encryption parsing
        /// </summary>
        private static (EncryptionHeader header, EncryptionVerifier verifier) ParseEcma376Agile(
            byte[] data, VersionInfo versionInfo)
        {
            // Phase 2b: Implement Agile encryption parsing
            // This uses XML-based encryption info instead of binary
            throw new NotImplementedException("ECMA-376 Agile parsing will be implemented in Phase 2b");
        }

        /// <summary>
        /// Validates parsed encryption header for consistency
        /// </summary>
        public static void ValidateHeader(EncryptionHeader header, VersionInfo versionInfo)
        {
            // Validate algorithm ID matches version
            string expectedAlgFamily = versionInfo.GetAlgorithmFamily();
            string actualAlgFamily = header.GetAlgorithmName();

            if (expectedAlgFamily == "AES" && !actualAlgFamily.Contains("AES"))
            {
                throw new CorruptedEncryptionInfoException(
                    $"Algorithm mismatch: expected {expectedAlgFamily}, got {actualAlgFamily}");
            }

            // Validate key size
            int expectedKeySize = versionInfo.GetKeyLengthBits();
            if (expectedKeySize > 0 && header.KeySize != expectedKeySize)
            {
                // This might be a warning rather than an error - some implementations vary
                Console.WriteLine($"Warning: Key size mismatch - expected {expectedKeySize}, got {header.KeySize}");
            }
        }

        /// <summary>
        /// Validates parsed encryption verifier for consistency
        /// </summary>
        public static void ValidateVerifier(EncryptionVerifier verifier)
        {
            if (verifier.Salt == null || verifier.Salt.Length == 0)
                throw new CorruptedEncryptionInfoException("Missing or empty salt");

            if (verifier.EncryptedVerifier == null || verifier.EncryptedVerifier.Length == 0)
                throw new CorruptedEncryptionInfoException("Missing or empty encrypted verifier");

            if (verifier.EncryptedVerifierHash == null || verifier.EncryptedVerifierHash.Length == 0)
                throw new CorruptedEncryptionInfoException("Missing or empty encrypted verifier hash");

            // For ECMA-376 Standard, verifier should be exactly 16 bytes
            if (verifier.EncryptedVerifier.Length != 16)
            {
                Console.WriteLine($"Warning: Unexpected verifier length: {verifier.EncryptedVerifier.Length} (expected 16)");
            }
        }
    }
}