namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Represents a persist object entry
    /// </summary>
    public class PersistObjectEntry
    {
        /// <summary>
        /// The persist object ID
        /// </summary>
        public uint PersistId { get; set; }
        /// <summary>
        /// The persist object offset in the stream
        /// </summary>
        public long FileOffset { get; set; }
        /// <summary>
        /// The persist object length
        /// </summary>
        public uint Length { get; set; }
        /// <summary>
        /// Whether the persist object is encrypted
        /// </summary>
        public bool IsEncrypted { get; set; }
    }
}