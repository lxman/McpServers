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

        #endregion

        #region Legacy Format Encryption Properties

        /// <summary>
        /// Type of legacy encryption detected (e.g., "Excel BIFF", "Word Binary", "PowerPoint Binary")
        /// </summary>
        public string? LegacyEncryptionType { get; set; }

        /// <summary>
        /// Legacy encryption method (e.g., "RC4 Encryption", "XOR Obfuscation", "RC4 CryptoAPI")
        /// </summary>
        public string? LegacyEncryptionMethod { get; set; }

        /// <summary>
        /// Detailed legacy encryption information
        /// </summary>
        public string? LegacyEncryptionDetails { get; set; }

        /// <summary>
        /// Excel FilePass record information (if applicable)
        /// </summary>
        public FilePassRecord? ExcelFilePassRecord { get; set; }

        /// <summary>
        /// PowerPoint encryption information (if applicable)
        /// </summary>
        public PowerPointEncryptionInfo? PowerPointEncryptionInfo { get; set; }

        #endregion

        #region Metadata Properties

        /// <summary>
        /// Timestamp when encryption was detected
        /// </summary>
        public DateTime DetectedAt { get; set; }

        #endregion

        #region Computed Properties

        /// <summary>
        /// Indicates if this is a legacy Office format
        /// </summary>
        public bool IsLegacyFormat => !string.IsNullOrEmpty(LegacyEncryptionType);

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
                if (IsLegacyFormat)
                    return LegacyEncryptionMethod ?? "Unknown Legacy";
                
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
                if (IsLegacyFormat)
                    return LegacyEncryptionType ?? "Legacy Format";

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
                if (IsLegacyFormat)
                {
                    return LegacyEncryptionMethod?.Contains("RC4 CryptoAPI") == true;
                }

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
        public int? KeySizeBits
        {
            get
            {
                if (IsLegacyFormat)
                {
                    return LegacyEncryptionMethod switch
                    {
                        "RC4 Encryption" => 40,
                        "RC4 CryptoAPI" => Convert.ToInt32(Header?.KeySize ?? null),
                        "XOR Obfuscation" => 16,
                        _ => null
                    };
                }

                return Convert.ToInt32(Header?.KeySize);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the encryption type name
        /// </summary>
        /// <returns></returns>
        public string GetEncryptionTypeName()
        {
            return EncryptionMethod;
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
            
            if (IsLegacyFormat && !string.IsNullOrEmpty(LegacyEncryptionDetails))
                summary += $"Details: {LegacyEncryptionDetails}\n";
            
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
            
            if (IsLegacyFormat)
            {
                if (ExcelFilePassRecord != null)
                {
                    details += $"FilePass Encryption Type: 0x{ExcelFilePassRecord.EncryptionType:X4}\n";
                    details += $"FilePass Record Length: {ExcelFilePassRecord.RecordLength} bytes\n";
                }
                
                if (PowerPointEncryptionInfo != null)
                {
                    details += $"PowerPoint Encryption Details:\n";
                    details += $"  - Encrypted Current User: {PowerPointEncryptionInfo.HasEncryptedCurrentUser}\n";
                    details += $"  - CryptSession Container: {PowerPointEncryptionInfo.HasCryptSessionContainer}\n";
                    details += $"  - Encrypted Summary Info: {PowerPointEncryptionInfo.HasEncryptedSummaryInfo}\n";
                }
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
            
            // Legacy binary formats - assess based on the encryption type
            if (!IsLegacyFormat) return "Unknown security level";
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
        /// Gets whether this encryption is considered secure by modern standards
        /// </summary>
        /// <returns>True if encryption meets modern security standards</returns>
        public bool IsSecureEncryption()
        {
            return VersionInfo != null && VersionInfo.IsModernEncryption();
            // All legacy formats are considered insecure
        }

        /// <summary>
        /// Gets recommended action for this encryption type
        /// </summary>
        /// <returns>Recommendation string</returns>
        public string GetSecurityRecommendation()
        {
            string strength = GetEncryptionStrength();
            
            if (strength.StartsWith("Strong") || strength.StartsWith("Good"))
            {
                return "Encryption meets modern security standards";
            }
            
            if (strength.StartsWith("Weak"))
            {
                return "⚠️ RECOMMENDATION: Re-encrypt with modern OOXML format (AES-256)";
            }
            
            return strength.StartsWith("Very Weak")
                ? "🚨 CRITICAL: This encryption is trivially broken - re-encrypt immediately!"
                : "Unable to assess security";
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