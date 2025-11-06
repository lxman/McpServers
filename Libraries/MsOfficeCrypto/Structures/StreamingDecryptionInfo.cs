namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Information about streaming decryption operation
    /// </summary>
    public class StreamingDecryptionInfo
    {
        /// <summary>
        /// The encryption algorithm
        /// </summary>
        public string Algorithm { get; set; } = string.Empty;
        /// <summary>
        /// The hash algorithm used for integrity verification
        /// </summary>
        public string HashAlgorithm { get; set; } = string.Empty;
        /// <summary>
        /// The size of the encryption key in bits
        /// </summary>
        public uint KeySize { get; set; }
        /// <summary>
        /// The total number of bytes to be decrypted
        /// </summary>
        public long TotalBytesToDecrypt { get; set; }
        /// <summary>
        /// The size of the buffer used for streaming decryption
        /// </summary>
        public int BufferSize { get; set; }
        /// <summary>
        /// The type of encryption being used
        /// </summary>
        public string EncryptionType { get; set; } = string.Empty;
        /// <summary>
        /// Whether progress updates are supported during decryption
        /// </summary>
        public bool SupportsProgress { get; set; }
        /// <summary>
        /// The estimated memory usage for the decryption operation
        /// </summary>
        public long EstimatedMemoryUsage { get; set; }
    }
}