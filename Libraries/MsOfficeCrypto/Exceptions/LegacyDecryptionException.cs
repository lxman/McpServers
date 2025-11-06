using System;

namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Exception thrown when legacy decryption fails
    /// </summary>
    public class LegacyDecryptionException : DecryptionException
    {
        /// <inheritdoc />
        public LegacyDecryptionException(string message) : base(message) { }

        /// <inheritdoc />
        public LegacyDecryptionException(string message, Exception innerException) : base(message, innerException) { }
    }
}