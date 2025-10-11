using MsOfficeCrypto;
using MsOfficeCrypto.Exceptions;

namespace OfficeReader.Services;

/// <summary>
/// Simple document decryption service that wraps the MsOfficeCrypto facade
/// Provides logging and integration-specific error handling
/// Users manage their own file streams for maximum flexibility
/// </summary>
public interface IDocumentDecryptionService
{
    /// <summary>
    /// Decrypts a document from a stream
    /// Handles both encrypted and unencrypted documents transparently
    /// </summary>
    /// <param name="inputStream">Stream containing the document</param>
    /// <param name="password">Password (optional for unencrypted content)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing the document content</returns>
    Task<Stream> DecryptDocumentAsync(Stream inputStream, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document stream is encrypted
    /// </summary>
    /// <param name="stream">Stream containing the document</param>
    /// <returns>True if encrypted</returns>
    bool IsDocumentEncrypted(Stream stream);

    /// <summary>
    /// Verifies a password for an encrypted document
    /// </summary>
    /// <param name="stream">Stream containing the document</param>
    /// <param name="password">Password to verify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if password is correct</returns>
    Task<bool> VerifyPasswordAsync(Stream stream, string password, CancellationToken cancellationToken = default);
}

public class DocumentDecryptionService(ILogger<DocumentDecryptionService> logger) : IDocumentDecryptionService
{
    public async Task<Stream> DecryptDocumentAsync(Stream inputStream, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Decrypting document from stream");
            
            // The MsOfficeCrypto facade handles all the complexity
            Stream stream = await OfficeDocument.DecryptAsync(inputStream, password, cancellationToken);
            
            logger.LogDebug("Successfully decrypted document from stream");
            return stream;
        }
        catch (InvalidPasswordException ex)
        {
            logger.LogWarning("Invalid password provided for stream");
            throw new UnauthorizedAccessException("Invalid password provided", ex);
        }
        catch (NotEncryptedException ex)
        {
            logger.LogWarning("Password provided for unencrypted stream");
            throw new ArgumentException("Document is not encrypted, password not needed", ex);
        }
        catch (UnsupportedEncryptionException ex)
        {
            logger.LogError("Unsupported encryption type in stream");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt document from stream");
            throw;
        }
    }

    public bool IsDocumentEncrypted(Stream stream)
    {
        try
        {
            return OfficeDocument.IsEncrypted(stream);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error checking encryption status for stream");
            return false;
        }
    }

    public async Task<bool> VerifyPasswordAsync(Stream stream, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsDocumentEncrypted(stream))
                return await OfficeDocument.VerifyPasswordAsync(stream, password, cancellationToken);
            logger.LogDebug("Document stream is not encrypted");
            return true; // Any password is "correct" for unencrypted documents

        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error verifying password for stream");
            return false;
        }
    }
}