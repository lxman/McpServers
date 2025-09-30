namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Represents a PowerPoint record header
    /// </summary>
    public class RecordHeader
    {
        /// <summary>
        /// The record version
        /// </summary>
        public ushort RecVer { get; set; }      // 4 bits
        /// <summary>
        /// The record instance
        /// </summary>
        public ushort RecInstance { get; set; } // 12 bits
        /// <summary>
        /// The record type
        /// </summary>
        public ushort RecType { get; set; }
        /// <summary>
        /// The record length
        /// </summary>
        public uint RecLen { get; set; }
        /// <summary>
        /// The record offset in the stream
        /// </summary>
        public long FileOffset { get; set; }
    }
}