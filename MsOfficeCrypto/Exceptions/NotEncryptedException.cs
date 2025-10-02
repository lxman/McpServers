namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when attempting to process a document that is not encrypted
    /// </summary>
    public class NotEncryptedException : OfficeCryptoException
    {
        /// <inheritdoc />
        public NotEncryptedException(string message) : base(message) { }
    }
}