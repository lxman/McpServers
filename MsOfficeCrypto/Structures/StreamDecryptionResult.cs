using System;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Stream decryption result
    /// </summary>
    public class StreamDecryptionResult
    {
        /// <summary>
        /// Decrypted data
        /// </summary>
        public byte[] DecryptedData { get; set; } = Array.Empty<byte>();
        /// <summary>
        /// Whether decryption was successful
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Error message if decryption failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Time taken for decryption
        /// </summary>
        public TimeSpan DecryptionTime { get; set; }
        /// <summary>
        /// Number of blocks processed during decryption
        /// </summary>
        public int BlocksProcessed { get; set; }
    }
}