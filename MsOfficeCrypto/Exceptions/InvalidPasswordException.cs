using System;

namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when an incorrect password is provided for decryption
    /// </summary>
    public class InvalidPasswordException : OfficeCryptoException
    {
        /// <inheritdoc />
        public InvalidPasswordException() : base("Invalid password provided") { }

        /// <inheritdoc />
        public InvalidPasswordException(string message) : base(message) { }

        /// <inheritdoc />
        public InvalidPasswordException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}