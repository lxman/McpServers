namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Contains information about the decryption process
    /// </summary>
    public class DecryptionInfo
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
        /// The size of the encrypted data in bytes
        /// </summary>
        public int EncryptedDataSize { get; set; }
        /// <summary>
        /// The type of encryption used
        /// </summary>
        public string EncryptionType { get; set; } = string.Empty;
        /// <summary>
        /// The version of the encryption algorithm
        /// </summary>
        public string Version { get; set; } = string.Empty;
        /// <summary>
        /// The security level of the encryption
        /// </summary>
        public string SecurityLevel { get; set; } = string.Empty;

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Algorithm: {Algorithm}, Key Size: {KeySize} bits, Data Size: {EncryptedDataSize:N0} bytes, Security: {SecurityLevel}";
        }
    }
}