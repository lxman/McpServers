using System;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Decryption options and configuration
    /// </summary>
    public class DecryptionOptions
    {
        /// <summary>
        /// Threshold for switching to streaming decryption (default: 10MB)
        /// </summary>
        public long StreamingThreshold { get; set; } = 10 * 1024 * 1024;

        /// <summary>
        /// Buffer size for streaming operations (default: 64KB)
        /// </summary>
        public int StreamingBufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// Enable progress reporting
        /// </summary>
        public bool EnableProgressReporting { get; set; } = true;

        /// <summary>
        /// Timeout for password verification operations
        /// </summary>
        public TimeSpan PasswordVerificationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum parallel password verification attempts
        /// </summary>
        public int MaxParallelPasswordAttempts { get; set; } = Environment.ProcessorCount;
    }
}