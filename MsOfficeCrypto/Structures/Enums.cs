// ReSharper disable CheckNamespace

/// <summary>
/// Supported hash algorithms for CryptoAPI key derivation
/// </summary>
public enum HashAlgorithmType
{
    /// <summary>
    /// The default hash algorithm for CryptoAPI
    /// </summary>
    Md5 = 0x8003,
    /// <summary>
    /// The SHA-1 hash algorithm for CryptoAPI
    /// </summary>
    Sha1 = 0x8004
}

/// <summary>
/// Encryption method enumeration
/// </summary>
public enum LegacyEncryptionMethod
{
    /// <summary>
    /// Xor obfuscation encryption method
    /// </summary>
    XorObfuscation,
    /// <summary>
    /// RC4 encryption method using CryptoAPI
    /// </summary>
    Rc4CryptoApi
}

/// <summary>
/// Document format enumeration
/// </summary>
public enum OfficeDocumentFormat
{
    /// <summary>
    /// Word 97-2003
    /// </summary>
    Word,
    /// <summary>
    /// Excel 97-2003
    /// </summary>
    Excel,
    /// <summary>
    /// PowerPoint 97-2003
    /// </summary>
    PowerPoint
}

