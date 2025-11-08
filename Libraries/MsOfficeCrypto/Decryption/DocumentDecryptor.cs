using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MsOfficeCrypto.Algorithms;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;
using OpenMcdf;

namespace MsOfficeCrypto.Decryption
{
    /// <summary>
    /// Enhanced MS-OFFCRYPTO document decryptor with comprehensive encryption support
    /// Supports Standard, Agile, and DataSpaces encryption methods
    /// </summary>
    public class DocumentDecryptor : IDisposable
    {
        /// <summary>
        /// Decryption options and settings
        /// </summary>
        public DecryptionOptions Options { get; set; } = new DecryptionOptions();

        private readonly EncryptionInfo _encryptionInfo;
        private readonly byte[] _encryptedPackageData;
        private readonly DataSpacesHandler? _dataSpacesHandler;
        private bool _disposed;

        /// <summary>
        /// Creates an enhanced document decryptor
        /// </summary>
        /// <param name="encryptionInfo">Parsed encryption information</param>
        /// <param name="encryptedPackageData">Raw encrypted package data</param>
        /// <param name="dataSpacesHandler">Optional DataSpaces handler</param>
        public DocumentDecryptor(EncryptionInfo encryptionInfo, byte[] encryptedPackageData,
            DataSpacesHandler? dataSpacesHandler = null)
        {
            _encryptionInfo = encryptionInfo ?? throw new ArgumentNullException(nameof(encryptionInfo));
            _encryptedPackageData =
                encryptedPackageData ?? throw new ArgumentNullException(nameof(encryptedPackageData));
            _dataSpacesHandler = dataSpacesHandler;
        }

        #region Public methods

        /// <summary>
        /// Decrypts the document using the most appropriate method based on the encryption type
        /// </summary>
        /// <param name="password">Decryption password</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Decrypted document data</returns>
        public async Task<byte[]> DecryptDocumentAsync(string password, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            try
            {
                // Verify password first
                if (!await VerifyPasswordAsync(password, cancellationToken))
                    throw new InvalidPasswordException("Invalid password provided");

                // Determine the encryption type and use an appropriate decryption method
                return _encryptionInfo.GetEncryptionTypeName() switch
                {
                    "Standard" => await DecryptStandardAsync(password, cancellationToken),
                    "Agile" => await DecryptAgileAsync(password, cancellationToken),
                    "DataSpaces" => await DecryptDataSpacesAsync(password, cancellationToken),
                    _ => throw new UnsupportedEncryptionException($"Unsupported encryption type: {_encryptionInfo.GetEncryptionTypeName()}")
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is InvalidPasswordException || ex is UnsupportedEncryptionException))
            {
                throw new DecryptionException($"Enhanced document decryption failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Verifies password asynchronously with timeout support
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the password is correct</returns>
        public async Task<bool> VerifyPasswordAsync(string password, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return VerifyPassword(password);
            }, cancellationToken);
        }

        /// <summary>
        /// Verifies password using the appropriate method based on the encryption type
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <returns>True if the password is correct</returns>
        public bool VerifyPassword(string password)
        {
            ThrowIfDisposed();

            if (_encryptionInfo.Header == null || _encryptionInfo.Verifier == null)
                throw new InvalidOperationException("Encryption info is incomplete");

            return _encryptionInfo.GetEncryptionTypeName() switch
            {
                "Standard" => _encryptionInfo.Verifier.VerifyPassword(password, _encryptionInfo.Header),
                "Agile" => VerifyAgilePassword(password),
                "DataSpaces" => VerifyDataSpacesPassword(password),
                _ => _encryptionInfo.Verifier.VerifyPassword(password, _encryptionInfo.Header)
            };
        }

        /// <summary>
        /// Creates an enhanced decryptor from an encrypted file
        /// </summary>
        /// <param name="filePath">Path to the encrypted document</param>
        /// <returns>Enhanced document decryptor instance</returns>
        public static DocumentDecryptor FromFile(string filePath)
        {
            if (!OfficeCryptoDetector.IsEncryptedOfficeDocument(filePath))
                throw new NotEncryptedException($"Document is not encrypted: {filePath}");

            var encryptionInfo = OfficeCryptoDetector.GetEncryptionInfo(filePath);
            var encryptedPackageData = OfficeCryptoDetector.ExtractEncryptedPackageData(filePath, encryptionInfo);

            // Create a DataSpaces handler if needed
            DataSpacesHandler? dataSpacesHandler = null;
            if (!encryptionInfo.HasDataSpaces)
                return new DocumentDecryptor(encryptionInfo, encryptedPackageData, dataSpacesHandler);
            try
            {
                using var rootStorage = RootStorage.OpenRead(filePath);
                if (DataSpacesHandler.IsDataSpacesEncrypted(rootStorage))
                {
                    dataSpacesHandler = new DataSpacesHandler(rootStorage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize DataSpaces handler: {ex.Message}");
            }

            return new DocumentDecryptor(encryptionInfo, encryptedPackageData, dataSpacesHandler);
        }
        
        #endregion Public methods
        
        #region Private methods

        /// <summary>
        /// Decrypts using Standard encryption
        /// </summary>
        private async Task<byte[]> DecryptStandardAsync(string password, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var decryptionKey =
                    PasswordDerivation.DeriveKey(password, _encryptionInfo.Verifier!.Salt!, (int)_encryptionInfo.Header!.KeySize);
                var algorithm = _encryptionInfo.Header?.GetAlgorithmName() ?? "Unknown";

                return algorithm switch
                {
                    "AES-128" => AesHandler.Decrypt(_encryptedPackageData, decryptionKey),
                    "AES-192" => AesHandler.Decrypt(_encryptedPackageData, decryptionKey, 192),
                    "AES-256" => AesHandler.Decrypt(_encryptedPackageData, decryptionKey, 256),
                    _ => throw new UnsupportedEncryptionException($"Unsupported algorithm: {algorithm}")
                };
            }, cancellationToken);
        }

        /// <summary>
        /// Decrypts using Agile encryption
        /// </summary>
        private async Task<byte[]> DecryptAgileAsync(string password, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_encryptionInfo.AgileKeyData == null)
                    throw new CorruptedEncryptionInfoException("Agile encryption KeyData not available");

                // Map hash algorithm string to HashAlgorithmName
                var hashAlgorithm = _encryptionInfo.AgileHashAlgorithm?.ToUpper() switch
                {
                    "SHA1" => HashAlgorithmName.SHA1,
                    "SHA256" => HashAlgorithmName.SHA256,
                    "SHA384" => HashAlgorithmName.SHA384,
                    "SHA512" => HashAlgorithmName.SHA512,
                    _ => HashAlgorithmName.SHA512 // Default to SHA512 for Agile
                };

                return AgileEncryptionHandler.DecryptAgileDocument(
                    _encryptedPackageData,
                    password,
                    _encryptionInfo.AgilePasswordSalt ?? Array.Empty<byte>(),
                    _encryptionInfo.AgileSpinCount,
                    _encryptionInfo.AgileEncryptedKeyValue ?? Array.Empty<byte>(),
                    _encryptionInfo.AgileKeyData ?? Array.Empty<byte>(),
                    Convert.ToInt64(_encryptionInfo.UnencryptedPackageSize),
                    hashAlgorithm,
                    (int)(_encryptionInfo.Header?.KeySize ?? 256),
                    _encryptionInfo.AgileCipherAlgorithm ?? "AES",
                    _encryptionInfo.AgileCipherChaining ?? "ChainingModeCBC"
                );
            }, cancellationToken);
        }

        /// <summary>
        /// Decrypts using DataSpaces transformation
        /// </summary>
        private async Task<byte[]> DecryptDataSpacesAsync(string password, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return _dataSpacesHandler is null
                    ? throw new UnsupportedEncryptionException("DataSpaces handler not available")
                    : _dataSpacesHandler.DecryptDataSpacesStream("EncryptedPackage", password);
            }, cancellationToken);
        }

        /// <summary>
        /// Verifies Agile encryption password
        /// </summary>
        private bool VerifyAgilePassword(string password)
        {
            // CRITICAL: Use AgilePasswordSalt for password verification, not AgileKeyData!
            if (_encryptionInfo.AgilePasswordSalt == null)
                return false;

            try
            {
                // Map hash algorithm string to HashAlgorithmName
                var hashAlgorithm = _encryptionInfo.AgileHashAlgorithm?.ToUpper() switch
                {
                    "SHA1" => HashAlgorithmName.SHA1,
                    "SHA256" => HashAlgorithmName.SHA256,
                    "SHA384" => HashAlgorithmName.SHA384,
                    "SHA512" => HashAlgorithmName.SHA512,
                    _ => HashAlgorithmName.SHA512
                };

                // Pass the spinCount from EncryptionInfo!
                return AgileEncryptionHandler.VerifyAgilePassword(
                    password,
                    _encryptionInfo.AgilePasswordSalt!,
                    _encryptionInfo.AgileSpinCount,
                    _encryptionInfo.AgileVerifierHashInput ?? Array.Empty<byte>(),
                    _encryptionInfo.AgileVerifierHashValue ?? Array.Empty<byte>(),
                    _encryptionInfo.AgileEncryptedKeyValue ?? Array.Empty<byte>(),
                    hashAlgorithm,
                    (int)(_encryptionInfo.Header?.KeySize ?? 256)
                );
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifies DataSpaces password
        /// </summary>
        private bool VerifyDataSpacesPassword(string password)
        {
            try
            {
                if (_dataSpacesHandler == null)
                    return false;

                // Attempt to decrypt a small portion to verify password
                _dataSpacesHandler.DecryptDataSpacesStream("EncryptedPackage", password);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Throws if the object has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DocumentDecryptor));
        }
        
        #endregion Private methods

        /// <summary>
        /// Disposes the decryptor and associated resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _dataSpacesHandler?.Dispose();
            _disposed = true;
        }
    }
}


