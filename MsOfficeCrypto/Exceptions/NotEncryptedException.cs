using System;

namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when attempting to process a document that is not encrypted
    /// </summary>
    public class NotEncryptedException : OfficeCryptoException
    {
        /// <inheritdoc />
        public NotEncryptedException() : base("The document is not encrypted") { }

        /// <inheritdoc />
        public NotEncryptedException(string message) : base(message) { }

        /// <inheritdoc />
        public NotEncryptedException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}