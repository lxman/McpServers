using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MsOfficeCrypto.Decryption;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;
using OpenMcdf;

namespace MsOfficeCrypto
{
    /// <summary>
    /// Simple facade for Office document decryption - "file + password = decrypted stream"
    /// Handles all complexity internally while keeping advanced features accessible
    /// </summary>
    public static class OfficeDocument
    {
        /// <summary>
        /// Decrypts an Office document from a stream and returns the unencrypted content.
        /// If the document is not encrypted, returns a copy of the original content.
        /// </summary>
        /// <param name="inputStream">Stream containing the Office document</param>
        /// <param name="password">Password for encrypted documents (optional for unencrypted content)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream containing the decrypted document content</returns>
        /// <exception cref="InvalidPasswordException">When the password is incorrect</exception>
        /// <exception cref="NotEncryptedException">When a password is provided but the document isn't encrypted</exception>
        /// <exception cref="UnsupportedEncryptionException">When the encryption method is not supported</exception>
        public static async Task<Stream> DecryptAsync(Stream inputStream, string? password = null, CancellationToken cancellationToken = default)
        {
            if (!inputStream.CanRead)
                throw new ArgumentException("Input stream must be readable", nameof(inputStream));

            // Copy the input stream to memory so we can work with it multiple times if needed
            var memoryStream = new MemoryStream();
            inputStream.Position = 0;
            await inputStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            // Try to open as a compound file and check if encrypted
            RootStorage? root = null;
            try
            {
                try
                {
                    root = RootStorage.Open(memoryStream);
                }
                catch
                {
                    // Not a compound file - return original content
                    if (!string.IsNullOrEmpty(password))
                        throw new NotEncryptedException("Password provided but document is not encrypted");
                    
                    memoryStream.Position = 0;
                    return memoryStream;
                }

                // Check if it's encrypted
                if (!OfficeCryptoDetector.IsEncryptedOleDocument(root))
                {
                    // Not encrypted
                    if (!string.IsNullOrEmpty(password))
                        throw new NotEncryptedException("Password provided but document is not encrypted");
                    
                    memoryStream.Position = 0;
                    return memoryStream;
                }

                // Document IS encrypted - password required
                if (string.IsNullOrEmpty(password))
                    throw new InvalidPasswordException("Password required for encrypted document");

                // Extract encryption info and encrypted data from the SAME RootStorage
                var encryptionInfo = OfficeCryptoDetector.ExtractEncryptionInfo(root, "<stream>");

                byte[] encryptedPackageData;
                // Extract the EncryptedPackage stream
                encryptedPackageData = OfficeCryptoDetector.ExtractEncryptedPackageData(root, encryptionInfo);

                // Decrypt the data
                using var decryptor = new DocumentDecryptor(encryptionInfo, encryptedPackageData);
                var decryptedData = await decryptor.DecryptDocumentAsync(password, cancellationToken);
                
                return new MemoryStream(decryptedData);
            }
            finally
            {
                // Dispose RootStorage if we opened it
                root?.Dispose();
            }
        }

        /// <summary>
        /// Checks if a stream contains an encrypted Office document
        /// </summary>
        /// <param name="stream">Stream containing the Office document</param>
        /// <returns>True if the document is encrypted, false otherwise</returns>
        public static bool IsEncrypted(Stream stream)
        {
            if (!stream.CanRead)
                return false;
                
            try
            {
                return OfficeCryptoDetector.IsEncryptedOfficeDocument(stream);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifies a password against an encrypted Office document in a stream
        /// </summary>
        /// <param name="stream">Stream containing the Office document</param>
        /// <param name="password">Password to verify</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the password is correct, false otherwise</returns>
        /// <exception cref="NotEncryptedException">When the document is not encrypted</exception>
        public static async Task<bool> VerifyPasswordAsync(Stream stream, string password, CancellationToken cancellationToken = default)
        {
            if (!IsEncrypted(stream))
                throw new NotEncryptedException("Document is not encrypted");

            try
            {
                var encryptionInfo = OfficeCryptoDetector.GetEncryptionInfo(stream);
                var encryptedPackageData = OfficeCryptoDetector.ExtractEncryptedPackageData(stream, encryptionInfo);
                
                using var decryptor = new DocumentDecryptor(encryptionInfo, encryptedPackageData);
                return await decryptor.VerifyPasswordAsync(password, cancellationToken);
            }
            catch (InvalidPasswordException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Advanced Office document operations for users who need detailed control
    /// Access to all the complexity and detailed information
    /// </summary>
    public static class AdvancedOfficeDocument
    {
        /// <summary>
        /// Creates a DocumentDecryptor for advanced operations
        /// </summary>
        /// <param name="filePath">Path to the Office document</param>
        /// <returns>DocumentDecryptor instance for advanced operations</returns>
        public static DocumentDecryptor CreateDecryptor(string filePath)
        {
            return DocumentDecryptor.FromFile(filePath);
        }

        /// <summary>
        /// Creates a streaming decryptor for large files
        /// </summary>
        /// <param name="encryptionInfo">Encryption information</param>
        /// <param name="inputStream">Input stream</param>
        /// <returns>StreamingDocumentDecryptor for advanced streaming operations</returns>
        public static StreamingDocumentDecryptor CreateStreamingDecryptor(EncryptionInfo encryptionInfo, Stream inputStream)
        {
            return new StreamingDocumentDecryptor(encryptionInfo, inputStream);
        }

        /// <summary>
        /// Gets detailed encryption information about a document
        /// </summary>
        /// <param name="filePath">Path to the Office document</param>
        /// <returns>Detailed encryption information</returns>
        public static EncryptionInfo GetEncryptionInfo(string filePath)
        {
            return OfficeCryptoDetector.GetEncryptionInfo(filePath);
        }

        /// <summary>
        /// Gets detailed encryption information from a stream
        /// </summary>
        /// <param name="stream">Stream containing the Office document</param>
        /// <returns>Detailed encryption information</returns>
        public static EncryptionInfo GetEncryptionInfo(Stream stream)
        {
            return OfficeCryptoDetector.GetEncryptionInfo(stream);
        }
    }
}