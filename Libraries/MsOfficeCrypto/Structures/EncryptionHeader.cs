namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Encryption Header structure from MS-OFFCRYPTO specification
    /// Phase 2: Will contain parsed binary header information
    /// </summary>
    public class EncryptionHeader
    {
        /// <summary>
        /// Flags field from the encryption header
        /// </summary>
        public uint Flags { get; set; }

        /// <summary>
        /// Size of encryption header in bytes
        /// </summary>
        public uint SizeExtra { get; set; }

        /// <summary>
        /// Algorithm ID (e.g., AES, RC4)
        /// </summary>
        public uint AlgId { get; set; }

        /// <summary>
        /// Hash algorithm ID
        /// </summary>
        public uint AlgIdHash { get; set; }

        /// <summary>
        /// Key size in bits
        /// </summary>
        public uint KeySize { get; set; }

        /// <summary>
        /// Provider type
        /// </summary>
        public uint ProviderType { get; set; }

        /// <summary>
        /// Reserved field 1
        /// </summary>
        public uint Reserved1 { get; set; }
        /// <summary>
        /// Reserved field 2
        /// </summary>
        public uint Reserved2 { get; set; }

        /// <summary>
        /// Cryptographic Service Provider (CSP) name
        /// </summary>
        public string? CspName { get; set; }

        /// <summary>
        /// Raw header data
        /// </summary>
        public byte[]? RawData { get; set; }

        /// <summary>
        /// Gets the algorithm name from AlgId
        /// </summary>
        public string GetAlgorithmName()
        {
            // Phase 2: Implement algorithm ID lookup
            // Based on MS-OFFCRYPTO specification Table 2.1.4.2
            return AlgId switch
            {
                0x00000000 => "None",
                0x00006801 => "RC4", 
                0x0000660E => "AES-128",
                0x0000660F => "AES-192", 
                0x00006610 => "AES-256",
                _ => $"Unknown (0x{AlgId:X8})"
            };
        }

        /// <summary>
        /// Gets the hash algorithm name from AlgIdHash
        /// </summary>
        public string GetHashAlgorithmName()
        {
            // Phase 2: Implement hash algorithm ID lookup
            return AlgIdHash switch
            {
                0x00000000 => "None",
                0x00008003 => "MD5",
                0x00008004 => "SHA1",
                0x0000800C => "SHA256",
                0x0000800D => "SHA384", 
                0x0000800E => "SHA512",
                _ => $"Unknown (0x{AlgIdHash:X8})"
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Algorithm: {GetAlgorithmName()}, Hash: {GetHashAlgorithmName()}, KeySize: {KeySize} bits";
        }
    }
}