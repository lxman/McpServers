using System;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Enhanced decryption information with additional capabilities
    /// </summary>
    public class EnhancedDecryptionInfo : DecryptionInfo
    {
        /// <summary>
        /// Whether the algorithm supports streaming decryption
        /// </summary>
        public bool SupportsStreaming { get; set; }
        /// <summary>
        /// Whether the algorithm supports asynchronous decryption operations
        /// </summary>
        public bool SupportsAsync { get; set; }
        /// <summary>
        /// Information about DataSpaces transformation
        /// </summary>
        public DataSpacesInfo? DataSpacesInfo { get; set; }
        /// <summary>
        /// Estimated time required for decryption
        /// </summary>
        public TimeSpan EstimatedDecryptionTime { get; set; }
        /// <summary>
        /// List of supported features by the decryption algorithm
        /// </summary>
        public string[] SupportedFeatures { get; set; } = Array.Empty<string>();

        /// <inheritdoc />
        public EnhancedDecryptionInfo(DecryptionInfo baseInfo)
        {
            Algorithm = baseInfo.Algorithm;
            HashAlgorithm = baseInfo.HashAlgorithm;
            KeySize = baseInfo.KeySize;
            EncryptedDataSize = baseInfo.EncryptedDataSize;
            EncryptionType = baseInfo.EncryptionType;
            Version = baseInfo.Version;
            SecurityLevel = baseInfo.SecurityLevel;
        }
    }
}