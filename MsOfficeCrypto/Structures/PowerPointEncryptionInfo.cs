using System.Collections.Generic;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Contains encryption information extracted from a PowerPoint document
    /// </summary>
    public class PowerPointEncryptionInfo
    {
        /// <summary>
        /// Whether the document has an EncryptedCurrentUser stream
        /// </summary>
        public bool HasEncryptedCurrentUser { get; set; }
        /// <summary>
        /// Whether the document has a CryptSessionContainer stream
        /// </summary>
        public bool HasCryptSessionContainer { get; set; }
        /// <summary>
        /// Whether the document has an EncryptedSummaryInfo stream
        /// </summary>
        public bool HasEncryptedSummaryInfo { get; set; }

        /// <summary>
        /// Whether the document is encrypted
        /// </summary>
        public bool IsEncrypted => HasEncryptedCurrentUser || HasCryptSessionContainer || HasEncryptedSummaryInfo;

        /// <summary>
        /// Encryption method used
        /// </summary>
        public string EncryptionMethod => IsEncrypted ? "RC4 CryptoAPI" : "Not Encrypted";

        /// <inheritdoc />
        public override string ToString()
        {
            if (!IsEncrypted)
                return "Not Encrypted";

            var indicators = new List<string>();
            if (HasEncryptedCurrentUser) indicators.Add("Encrypted Current User");
            if (HasCryptSessionContainer) indicators.Add("CryptSession Container");
            if (HasEncryptedSummaryInfo) indicators.Add("Encrypted Summary Info");

            return $"RC4 CryptoAPI ({string.Join(", ", indicators)})";
        }
    }
}