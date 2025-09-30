// ReSharper disable InconsistentNaming
namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// RC4 CryptoAPI encryption information structure
    /// </summary>
    public class CryptoApiEncryptionInfo
    {
        /// <summary>
        /// The salt size in bytes
        /// </summary>
        public const int SALT_SIZE = 16;
        
        /// <summary>
        /// The encryption version
        /// </summary>
        public uint Version { get; set; }
        /// <summary>
        /// The salt used for key derivation
        /// </summary>
        public byte[] Salt { get; set; } = new byte[SALT_SIZE];
        /// <summary>
        /// The encrypted verifier
        /// </summary>
        public byte[] EncryptedVerifier { get; set; } = new byte[16];
        /// <summary>
        /// The encrypted verifier hash
        /// </summary>
        public byte[] EncryptedVerifierHash { get; set; } = new byte[20];
        /// <summary>
        /// The key size in bits
        /// </summary>
        public uint KeySize { get; set; } = 128; // Key size in bits
        /// <summary>
        /// The hash algorithm used for key derivation
        /// </summary>
        public HashAlgorithmType HashAlgorithm { get; set; } = HashAlgorithmType.Md5;
    }
}