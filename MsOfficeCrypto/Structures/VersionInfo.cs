namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Version information from MS-OFFCRYPTO EncryptionInfo stream
    /// Based on MS-OFFCRYPTO specification sections 2.1.4.1 and 2.1.5.1
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// Major version number
        /// </summary>
        public uint VersionMajor { get; set; }

        /// <summary>
        /// Minor version number  
        /// </summary>
        public uint VersionMinor { get; set; }

        /// <summary>
        /// Raw 32-bit version value
        /// </summary>
        public uint RawVersion { get; set; }

        /// <summary>
        /// Gets the encryption type based on version numbers
        /// Per MS-OFFCRYPTO specification
        /// </summary>
        public string GetEncryptionType()
        {
            return (VersionMajor, VersionMinor) switch
            {
                // ECMA-376 Agile Encryption (most modern)
                (4, 4) => "ECMA-376 Agile",
                
                // ECMA-376 Standard Encryption  
                (4, 2) => "ECMA-376 Standard",
                (3, 2) => "ECMA-376 Standard",
                
                // Office Binary Document RC4 CryptoAPI Encryption
                (4, 3) => "RC4 CryptoAPI",
                (3, 3) => "RC4 CryptoAPI", 
                (2, 3) => "RC4 CryptoAPI",
                
                // Office Binary Document RC4 Encryption (legacy)
                (2, 2) => "RC4 40-bit",
                (1, 1) => "RC4 40-bit",
                
                // Unknown/future versions
                _ => $"Unknown ({VersionMajor}.{VersionMinor})"
            };
        }

        /// <summary>
        /// Indicates if this is a modern, secure encryption method
        /// </summary>
        public bool IsModernEncryption()
        {
            return GetEncryptionType() switch
            {
                "ECMA-376 Agile" => true,
                "ECMA-376 Standard" => true,
                _ => false
            };
        }

        /// <summary>
        /// Indicates if this uses legacy/weak encryption
        /// </summary>
        public bool IsLegacyEncryption()
        {
            return GetEncryptionType() switch
            {
                "RC4 40-bit" => true,
                "RC4 CryptoAPI" => true, // Still considered legacy
                _ => false
            };
        }

        /// <summary>
        /// Gets the expected key length in bits for this encryption type
        /// </summary>
        public int GetKeyLengthBits()
        {
            return GetEncryptionType() switch
            {
                "ECMA-376 Agile" => 256,      // AES-256
                "ECMA-376 Standard" => 128,    // AES-128
                "RC4 CryptoAPI" => 128,        // Variable, but typically 128
                "RC4 40-bit" => 40,            // Fixed 40-bit
                _ => 0
            };
        }

        /// <summary>
        /// Gets the algorithm family used
        /// </summary>
        public string GetAlgorithmFamily()
        {
            return GetEncryptionType() switch
            {
                "ECMA-376 Agile" => "AES",
                "ECMA-376 Standard" => "AES",
                "RC4 CryptoAPI" => "RC4",
                "RC4 40-bit" => "RC4",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Indicates if this encryption supports password verification without decryption
        /// </summary>
        public bool SupportsPasswordVerification()
        {
            // All MS-OFFCRYPTO formats support password verification
            return !GetEncryptionType().StartsWith("Unknown");
        }

        /// <summary>
        /// Gets security assessment of this encryption method
        /// </summary>
        public string GetSecurityAssessment()
        {
            return GetEncryptionType() switch
            {
                "ECMA-376 Agile" => "Strong - Modern AES encryption",
                "ECMA-376 Standard" => "Good - Standard AES encryption",
                "RC4 CryptoAPI" => "Weak - Legacy RC4 (deprecated)",
                "RC4 40-bit" => "Very Weak - Export-grade RC4 (broken)",
                _ => "Unknown security level"
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{GetEncryptionType()} (v{VersionMajor}.{VersionMinor}, {GetKeyLengthBits()}-bit {GetAlgorithmFamily()})";
        }
    }
}