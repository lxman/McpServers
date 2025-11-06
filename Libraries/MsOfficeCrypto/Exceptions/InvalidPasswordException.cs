namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Thrown when an incorrect password is provided for decryption
    /// </summary>
    public class InvalidPasswordException : OfficeCryptoException
    {
        /// <inheritdoc />
        public InvalidPasswordException(string message) : base(message) { }
    }
}