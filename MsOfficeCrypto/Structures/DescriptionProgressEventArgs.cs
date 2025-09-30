using System;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Enhanced decryption progress event arguments
    /// </summary>
    public class DecryptionProgressEventArgs : EventArgs
    {
        /// <summary>
        /// How many bytes have been processed
        /// </summary>
        public long BytesProcessed { get; }
        /// <summary>
        /// Total number of bytes to be processed
        /// </summary>
        public long TotalBytes { get; }
        /// <summary>
        /// Percentage of completion
        /// </summary>
        public double ProgressPercentage { get; }
        /// <summary>
        /// Description of the current operation
        /// </summary>
        public string Operation { get; set; } = string.Empty;
        /// <summary>
        /// Timestamp of the event
        /// </summary>
        public DateTime Timestamp { get; }

        /// <inheritdoc />
        public DecryptionProgressEventArgs(long bytesProcessed, long totalBytes, double progressPercentage)
        {
            BytesProcessed = bytesProcessed;
            TotalBytes = totalBytes;
            ProgressPercentage = progressPercentage;
            Timestamp = DateTime.UtcNow;
        }
    }
}