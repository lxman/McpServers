using System.Collections.Generic;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Decryption context for a legacy document
    /// </summary>
    public class DecryptionContext
    {
        /// <summary>
        /// The encryption method used
        /// </summary>
        public LegacyEncryptionMethod EncryptionMethod { get; set; }
        /// <summary>
        /// The document format
        /// </summary>
        public OfficeDocumentFormat OfficeDocumentFormat { get; set; }
        /// <summary>
        /// The password used for decryption
        /// </summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>
        /// The salt used for decryption
        /// </summary>
        public byte[]? Salt { get; set; }
        /// <summary>
        /// The encryption info used for decryption
        /// </summary>
        public byte[]? EncryptionInfo { get; set; }
        /// <summary>
        /// The block size used for decryption
        /// </summary>
        public int BlockSize { get; set; } = 512;
        /// <summary>
        /// Whether to skip the header during decryption
        /// </summary>
        public bool SkipHeader { get; set; } = true;
        /// <summary>
        /// The size of the header in bytes
        /// </summary>
        public int HeaderSize { get; set; } = 68; // Default for Word FIB
        /// <summary>
        /// Information about the CryptoAPI encryption
        /// </summary>
        public CryptoApiEncryptionInfo CryptoApiInfo { get; set; } = new CryptoApiEncryptionInfo();
        /// <summary>
        /// Additional properties for decryption context
        /// </summary>
        public Dictionary<string, object> AdditionalProperties { get; set; } = new Dictionary<string, object>();
    }
}