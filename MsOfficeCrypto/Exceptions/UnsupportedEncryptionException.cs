namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when the encryption format is not supported
    /// </summary>
    public class UnsupportedEncryptionException : OfficeCryptoException
    {
        /// <summary>
        /// The encryption type that is not supported
        /// </summary>
        public string? EncryptionType { get; }

        /// <inheritdoc />
        public UnsupportedEncryptionException(string encryptionType) 
            : base($"Unsupported encryption format: {encryptionType}")
        {
            EncryptionType = encryptionType;
        }

        /// <inheritdoc />
        public UnsupportedEncryptionException(string encryptionType, string message) 
            : base(message)
        {
            EncryptionType = encryptionType;
        }
    }
}