using System;

namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when document decryption fails
    /// </summary>
    public class DecryptionException : OfficeCryptoException
    {
        /// <inheritdoc />
        public DecryptionException() : base("Document decryption failed") { }

        /// <inheritdoc />
        public DecryptionException(string message) : base(message) { }

        /// <inheritdoc />
        public DecryptionException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}