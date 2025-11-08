using Microsoft.Extensions.Logging;
using MsOfficeCrypto;
using MsOfficeCrypto.Exceptions;

namespace DocumentServer.Core.Services.Core;

/// <summary>
/// Document decryption service that wraps the MsOfficeCrypto facade
/// </summary>
public class DocumentDecryptionService(ILogger<DocumentDecryptionService> logger)
{
    /// <summary>
    /// Decrypts a document from a stream
    /// Handles both encrypted and unencrypted documents transparently
    /// </summary>
    public async Task<Stream> DecryptDocumentAsync(Stream inputStream, string? password = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Decrypting document from stream");
            
            // The MsOfficeCrypto facade handles all the complexity
            var stream = await OfficeDocument.DecryptAsync(inputStream, password, cancellationToken);
            
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

    /// <summary>
    /// Checks if a document stream is encrypted
    /// </summary>
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

    /// <summary>
    /// Verifies a password for an encrypted document
    /// </summary>
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
