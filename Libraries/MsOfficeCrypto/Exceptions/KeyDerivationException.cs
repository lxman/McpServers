using System;

namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when key derivation fails
    /// </summary>
    public class KeyDerivationException : OfficeCryptoException
    {
        /// <inheritdoc />
        public KeyDerivationException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}