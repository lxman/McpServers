using System;

namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when key derivation fails
    /// </summary>
    public class KeyDerivationException : OfficeCryptoException
    {
        /// <inheritdoc />
        public KeyDerivationException() : base("Key derivation failed") { }

        /// <inheritdoc />
        public KeyDerivationException(string message) : base(message) { }

        /// <inheritdoc />
        public KeyDerivationException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}