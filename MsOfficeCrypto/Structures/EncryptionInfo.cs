using System;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Enhanced encryption information structure with comprehensive legacy format support
    /// </summary>
    public class EncryptionInfo
    {
        #region Modern OOXML Encryption Properties

        /// <summary>
        /// Source file or stream identifier
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Version information from EncryptionInfo stream
        /// </summary>
        public VersionInfo? VersionInfo { get; set; }

        /// <summary>
        /// Raw EncryptionInfo stream data
        /// </summary>
        public byte[]? EncryptionInfoData { get; set; }

        /// <summary>
        /// Size of EncryptionInfo stream
        /// </summary>
        public int EncryptionInfoSize { get; set; }

        /// <summary>
        /// Parsed encryption header
        /// </summary>
        public EncryptionHeader? Header { get; set; }

        /// <summary>
        /// Parsed encryption verifier
        /// </summary>
        public EncryptionVerifier? Verifier { get; set; }

        /// <summary>
        /// Indicates if DataSpaces transformation is present
        /// </summary>
        public bool HasDataSpaces { get; set; }
        
        /// <summary>
        /// Indicates if DataSpaceMap is present
        /// </summary>
        public bool HasDataSpaceMap { get; set; }
        /// <summary>
        /// Indicates if DataSpaceInfo is present
        /// </summary>
        public bool HasDataSpaceInfo { get; set; }
        /// <summary>
        /// Indicates if TransformInfo is present
        /// </summary>
        public bool HasTransformInfo { get; set; }
        
        /// <summary>
        /// Size of encrypted package data
        /// </summary>
        public int EncryptedPackageSize { get; set; }

        /// <summary>
        /// Size of unencrypted package data
        /// </summary>
        public ulong UnencryptedPackageSize { get; set; }
        
        /// <summary>
        /// Agile key data (if applicable)
        /// </summary>
        public byte[]? AgileKeyData { get; set; }
        
        /// <summary>
        /// Agile verifier hash input (if applicable)
        /// </summary>
        public byte[]? AgileVerifierHashInput { get; set; }
        
        /// <summary>
        /// Agile verifier hash value (if applicable)
        /// </summary>
        public byte[]? AgileVerifierHashValue { get; set; }
        
        /// <summary>
        /// Agile encrypted key value (if applicable)
        /// </summary>
        public byte[]? AgileEncryptedKeyValue { get; set; }
        
        /// <summary>
        /// Agile hash algorithm name (SHA1, SHA256, SHA384, SHA512)
        /// </summary>
        public string? AgileHashAlgorithm { get; set; }

        /// <summary>
        /// Agile cipher algorithm name (AES)
        /// </summary>
        public string? AgileCipherAlgorithm { get; set; }

        /// <summary>
        /// Agile cipher chaining mode (ChainingModeCBC, ChainingModeCFB)
        /// </summary>
        public string? AgileCipherChaining { get; set; }

        /// <summary>
        /// Agile spin count (iterations for key derivation)
        /// </summary>
        public int AgileSpinCount { get; set; } = 100000;

        /// <summary>
        /// Agile block size in bytes
        /// </summary>
        public int AgileBlockSize { get; set; } = 16;

        /// <summary>
        /// Agile password salt (from encryptedKey element) - used for password verification
        /// </summary>
        public byte[]? AgilePasswordSalt { get; set; }

        #endregion

        #region Metadata Properties

        /// <summary>
        /// Timestamp when encryption was detected
        /// </summary>
        public DateTime DetectedAt { get; set; }

        #endregion

        #region Computed Properties

        /// <summary>
        /// Indicates if this is a modern OOXML format
        /// </summary>
        public bool IsModernFormat => VersionInfo != null || EncryptionInfoData != null;

        /// <summary>
        /// Gets the primary encryption method description
        /// </summary>
        public string EncryptionMethod
        {
            get
            {
                if (Header != null)
                {
                    return Header.AlgId switch
                    {
                        0x0000660E => "AES-128",
                        0x0000660F => "AES-192", 
                        0x00006610 => "AES-256",
                        0x00006801 => "RC4",
                        _ => $"Algorithm ID: 0x{Header.AlgId:X8}"
                    };
                }

                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the encryption type description
        /// </summary>
        public string EncryptionType
        {
            get
            {
                if (VersionInfo != null)
                {
                    return VersionInfo.VersionMajor switch
                    {
                        0x0002 => "ECMA-376 Standard",
                        0x0003 => "ECMA-376 Standard", 
                        0x0004 => "ECMA-376 Agile",
                        _ => $"Version {VersionInfo.VersionMajor}.{VersionInfo.VersionMinor}"
                    };
                }

                return HasDataSpaces ? "DataSpaces" : "Unknown";
            }
        }

        /// <summary>
        /// Indicates if the document uses strong encryption
        /// </summary>
        public bool HasStrongEncryption
        {
            get
            {
                return Header?.AlgId switch
                {
                    0x0000660E => true, // AES-128
                    0x0000660F => true, // AES-192
                    0x00006610 => true, // AES-256
                    _ => false
                };
            }
        }

        /// <summary>
        /// Gets the key size in bits (if available)
        /// </summary>
        public int? KeySizeBits => Convert.ToInt32(Header?.KeySize);

        #endregion

        #region Methods

        /// <summary>
        /// Gets the encryption type name
        /// </summary>
        /// <returns></returns>
        public string GetEncryptionTypeName()
        {
            string type = EncryptionType;

            return type.StartsWith("ECMA-376")
                ? type[9..]
                : type;
        }

        /// <summary>
        /// Gets a human-readable summary of the encryption information
        /// </summary>
        /// <returns>Formatted encryption summary</returns>
        public string GetSummary()
        {
            var summary = $"Encryption Type: {EncryptionType}\n";
            summary += $"Encryption Method: {EncryptionMethod}\n";
            
            if (KeySizeBits.HasValue)
                summary += $"Key Size: {KeySizeBits} bits\n";
            
            summary += $"Strong Encryption: {(HasStrongEncryption ? "Yes" : "No")}\n";
            
            if (HasDataSpaces)
                summary += "DataSpaces: Present\n";
            
            summary += $"Detected: {DetectedAt:yyyy-MM-dd HH:mm:ss}";
            
            return summary;
        }

        /// <summary>
        /// Gets detailed technical information
        /// </summary>
        /// <returns>Technical details string</returns>
        public string GetTechnicalDetails()
        {
            string details = GetSummary() + "\n\n";
            
            if (IsModernFormat && VersionInfo != null)
            {
                details += $"Version Info: {VersionInfo.VersionMajor}.{VersionInfo.VersionMinor}\n";
                details += $"EncryptionInfo Size: {EncryptionInfoSize} bytes\n";
            }
            
            if (Header != null)
            {
                details += $"Algorithm ID: 0x{Header.AlgId:X8}\n";
                details += $"Hash Algorithm: 0x{Header.AlgIdHash:X8}\n";
                details += $"Flags: 0x{Header.Flags:X8}\n";
            }
            
            if (UnencryptedPackageSize > 0)
                details += $"Unencrypted Package Size: {UnencryptedPackageSize:N0} bytes\n";
            
            return details;
        }

        /// <summary>
        /// Gets the security strength assessment of the encryption
        /// Works for both modern OOXML and legacy binary formats
        /// </summary>
        /// <returns>Security assessment string</returns>
        public string GetEncryptionStrength()
        {
            // Modern OOXML formats with VersionInfo
            if (VersionInfo != null)
            {
                return VersionInfo.GetSecurityAssessment();
            }
            
            // Check encryption method from EncryptionType property
            string encType = EncryptionType.ToLowerInvariant() ?? "";
            string encMethod = EncryptionMethod.ToLowerInvariant() ?? "";
                
            // XOR Obfuscation - very weak
            if (encMethod.Contains("xor"))
            {
                return "Very Weak - XOR obfuscation (trivially broken)";
            }
                
            // RC4 CryptoAPI - weak but better than XOR
            if (encMethod.Contains("rc4") || encMethod.Contains("cryptoapi"))
            {
                return "Weak - Legacy RC4 encryption (deprecated, vulnerable)";
            }
                
            // Generic legacy assessment
            return "Weak - Legacy binary format encryption (deprecated)";

        }

        /// <summary>
        /// Enhanced ToString with rich encryption summary
        /// </summary>
        /// <returns>Comprehensive encryption summary</returns>
        public override string ToString()
        {
            if (VersionInfo == null)
                return $"{EncryptionType} - {EncryptionMethod}" +
                       (KeySizeBits.HasValue ? $" ({KeySizeBits} bits)" : "");
            string strength = GetEncryptionStrength();
            string keyInfo = KeySizeBits.HasValue ? $"{KeySizeBits} bits" : "unknown key size";
            return $"{VersionInfo.GetEncryptionType()} - Security: {strength}, Key: {keyInfo}";

            // Fallback for legacy formats
        }

        #endregion
    }
}