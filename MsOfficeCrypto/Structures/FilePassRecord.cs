namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Represents a FilePass record from an Excel BIFF file
    /// </summary>
    public class FilePassRecord
    {
        /// <summary>
        /// The encryption type (0x0000 = XOR Obfuscation, 0x0001 = RC4 Encryption)
        /// </summary>
        public ushort EncryptionType { get; set; }
        /// <summary>
        /// The length of the encryption information
        /// </summary>
        public ushort RecordLength { get; set; }
        /// <summary>
        /// The encryption information bytes
        /// </summary>
        public byte[]? EncryptionInfo { get; set; }

        /// <summary>
        /// The encryption type (0x0000 = XOR Obfuscation, 0x0001 = RC4 Encryption)
        /// </summary>
        public bool IsXorObfuscation => EncryptionType == 0x0000;
        /// <summary>
        /// The encryption type (0x0000 = XOR Obfuscation, 0x0001 = RC4 Encryption)
        /// </summary>
        public bool IsRc4Encryption => EncryptionType == 0x0001;

        /// <summary>
        /// The encryption method name
        /// </summary>
        public string EncryptionMethod => EncryptionType switch
        {
            0x0000 => "XOR Obfuscation",
            0x0001 => "RC4 Encryption",
            _ => $"Unknown (0x{EncryptionType:X4})"
        };
    }
}