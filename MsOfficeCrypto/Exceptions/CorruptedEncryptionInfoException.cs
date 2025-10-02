using System;

namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when EncryptionInfo data is corrupted or invalid
    /// </summary>
    public class CorruptedEncryptionInfoException : OfficeCryptoException
    {
        /// <inheritdoc />
        public CorruptedEncryptionInfoException(string message) : base(message) { }

        /// <inheritdoc />
        public CorruptedEncryptionInfoException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}