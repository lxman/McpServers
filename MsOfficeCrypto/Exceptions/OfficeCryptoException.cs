using System;

namespace MsOfficeCrypto.Exceptions
{
    /// <summary>
    /// Base exception for MS-OFFCRYPTO operations
    /// </summary>
    public class OfficeCryptoException : Exception
    {
        /// <inheritdoc />
        public OfficeCryptoException(string message) : base(message) { }

        /// <inheritdoc />
        public OfficeCryptoException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}