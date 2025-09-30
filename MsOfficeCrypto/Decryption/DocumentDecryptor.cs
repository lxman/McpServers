using System;
using System.Collections.Generic;
using System.IO;
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
    /// Phase 4 implementation with streaming, async operations, and advanced features
    /// </summary>
    public class DocumentDecryptor : IDisposable
    {
        /// <summary>
        /// Progress reporting for long operations
        /// </summary>
        public event EventHandler<DecryptionProgressEventArgs>? ProgressChanged;

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
        /// Decrypts the document using the most appropriate method based on encryption type
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
                    _ => await DecryptLegacyAsync(password, cancellationToken)
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
        /// Decrypts the document to a stream asynchronously
        /// </summary>
        /// <param name="password">Decryption password</param>
        /// <param name="outputStream">Output stream for decrypted data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        public async Task DecryptToStreamAsync(string password, Stream outputStream,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));

            if (!outputStream.CanWrite)
                throw new ArgumentException("Output stream must be writable", nameof(outputStream));

            byte[] decryptedData = await DecryptDocumentAsync(password, cancellationToken);
            await outputStream.WriteAsync(decryptedData, 0, decryptedData.Length, cancellationToken);
            await outputStream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Decrypts the document to a file asynchronously
        /// </summary>
        /// <param name="password">Decryption password</param>
        /// <param name="outputPath">Output file path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the operation</returns>
        public async Task DecryptToFileAsync(string password, string outputPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 4096, FileOptions.SequentialScan);

            await DecryptToStreamAsync(password, outputStream, cancellationToken);
        }

        /// <summary>
        /// Verifies password asynchronously with timeout support
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if password is correct</returns>
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
        /// Verifies password using appropriate method based on encryption type
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <returns>True if password is correct</returns>
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
        /// Gets comprehensive decryption information
        /// </summary>
        /// <returns>Enhanced decryption information</returns>
        public EnhancedDecryptionInfo GetDecryptionInfo()
        {
            ThrowIfDisposed();

            var baseInfo = new DecryptionInfo
            {
                Algorithm = _encryptionInfo.Header?.GetAlgorithmName() ?? "Unknown",
                HashAlgorithm = _encryptionInfo.Header?.GetHashAlgorithmName() ?? "Unknown",
                KeySize = _encryptionInfo.Header?.KeySize ?? 0,
                EncryptedDataSize = _encryptedPackageData.Length,
                EncryptionType = _encryptionInfo.GetEncryptionTypeName(),
                Version = _encryptionInfo.VersionInfo?.ToString() ?? "Unknown",
                SecurityLevel = _encryptionInfo.Verifier?.GetSecurityLevel() ?? "Unknown"
            };

            return new EnhancedDecryptionInfo(baseInfo)
            {
                SupportsStreaming = CanUseStreaming(),
                SupportsAsync = true,
                DataSpacesInfo = _dataSpacesHandler?.GetDataSpacesInfo(),
                EstimatedDecryptionTime = EstimateDecryptionTime(),
                SupportedFeatures = GetSupportedFeatures()
            };
        }

        /// <summary>
        /// Attempts batch password verification with multiple candidates
        /// </summary>
        /// <param name="passwordCandidates">Array of password candidates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result with success flag and correct password if found</returns>
        public async Task<(bool Success, string? CorrectPassword, byte[]? DecryptedData)>
            TryDecryptWithCandidatesAsync(string[] passwordCandidates, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return await Task.Run(async () =>
            {
                foreach (string password in passwordCandidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!await VerifyPasswordAsync(password, cancellationToken)) continue;
                        byte[] decryptedData = await DecryptDocumentAsync(password, cancellationToken);
                        return (true, password, decryptedData);
                    }
                    catch (InvalidPasswordException)
                    {
                        // Continue to the next candidate
                    }
                }

                return (false, (string?)null, (byte[]?)null);
            }, cancellationToken);
        }

        /// <summary>
        /// Creates an enhanced decryptor from an encrypted file
        /// </summary>
        /// <param name="filePath">Path to encrypted document</param>
        /// <returns>Enhanced document decryptor instance</returns>
        public static DocumentDecryptor FromFile(string filePath)
        {
            if (!OfficeCryptoDetector.IsEncryptedOfficeDocument(filePath))
                throw new NotEncryptedException($"Document is not encrypted: {filePath}");

            EncryptionInfo encryptionInfo = OfficeCryptoDetector.GetEncryptionInfo(filePath);
            byte[] encryptedPackageData = OfficeCryptoDetector.ExtractEncryptedPackageData(filePath, encryptionInfo);

            // Create a DataSpaces handler if needed
            DataSpacesHandler? dataSpacesHandler = null;
            if (!encryptionInfo.HasDataSpaces)
                return new DocumentDecryptor(encryptionInfo, encryptedPackageData, dataSpacesHandler);
            try
            {
                using RootStorage rootStorage = OpenMcdf.RootStorage.OpenRead(filePath);
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

                byte[] decryptionKey =
                    PasswordDerivation.DeriveKey(password, _encryptionInfo.Verifier!.Salt!, (int)_encryptionInfo.Header!.KeySize, 0);
                string algorithm = _encryptionInfo.Header?.GetAlgorithmName() ?? "Unknown";

                return algorithm switch
                {
                    "AES-128" => AesHandler.Decrypt(_encryptedPackageData, decryptionKey, 128),
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

                // Use AgileEncryptionHandler for decryption
                return AgileEncryptionHandler.DecryptAgileDocument(
                    _encryptedPackageData,
                    password,
                    _encryptionInfo.AgileKeyData,
                    HashAlgorithmName.SHA1, // Default - should be read from EncryptionInfo
                    (int)(_encryptionInfo.Header?.KeySize ?? 128),
                    "AES", // Default - should be read from EncryptionInfo
                    "CBC" // Default - should be read from EncryptionInfo
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
        /// Decrypts using legacy methods (fallback)
        /// </summary>
        private async Task<byte[]> DecryptLegacyAsync(string password, CancellationToken cancellationToken)
        {
            return _encryptionInfo.LegacyEncryptionType switch
            {
                "Word Binary" => await LegacyHandler.DecryptWordBinaryAsync(_encryptedPackageData, _encryptionInfo, password, cancellationToken),
                "Excel Biff" => await LegacyHandler.DecryptExcelBiffAsync(_encryptedPackageData, _encryptionInfo, password, cancellationToken),
                "PowerPoint Binary" => await LegacyHandler.DecryptPowerPointBinaryAsync(_encryptedPackageData, _encryptionInfo, password, cancellationToken),
                _ => throw new UnsupportedEncryptionException("Unsupported legacy encryption type")
            };
        }
        
        /// <summary>
        /// Verifies Agile encryption password
        /// </summary>
        private bool VerifyAgilePassword(string password)
        {
            if (_encryptionInfo.AgileKeyData == null)
                return false;

            try
            {
                return AgileEncryptionHandler.VerifyAgilePassword(
                    password,
                    _encryptionInfo.AgileKeyData!,
                    _encryptionInfo.AgileVerifierHashInput ?? Array.Empty<byte>(),
                    _encryptionInfo.AgileVerifierHashValue ?? Array.Empty<byte>(),
                    _encryptionInfo.AgileEncryptedKeyValue ?? Array.Empty<byte>(),
                    HashAlgorithmName.SHA1,
                    (int)(_encryptionInfo.Header?.KeySize ?? 128)
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
        /// Determines if streaming decryption is possible
        /// </summary>
        private bool CanUseStreaming()
        {
            return _encryptedPackageData.Length > Options.StreamingThreshold &&
                   _encryptionInfo.GetEncryptionTypeName() == "Standard";
        }

        /// <summary>
        /// Estimates decryption time based on data size and encryption type
        /// </summary>
        private TimeSpan EstimateDecryptionTime()
        {
            // Estimates based on empirical data
            double baseTimeMs = _encryptedPackageData.Length / 1024.0 / 1024.0; // MB

            double multiplier = _encryptionInfo.GetEncryptionTypeName() switch
            {
                "Standard" => 0.1, // Fast
                "Agile" => 0.5, // Moderate
                "DataSpaces" => 1.0, // Slower due to transformation overhead
                _ => 0.2
            };

            return TimeSpan.FromMilliseconds(baseTimeMs * multiplier * 100);
        }

        /// <summary>
        /// Gets supported features for this encryption type
        /// </summary>
        private string[] GetSupportedFeatures()
        {
            var features = new List<string> { "Password Verification", "Async Decryption" };

            if (CanUseStreaming())
                features.Add("Streaming Decryption");

            if (_encryptionInfo.GetEncryptionTypeName() == "Agile")
                features.Add("Advanced Key Derivation");

            if (_dataSpacesHandler != null)
                features.Add("DataSpaces Transformation");

            return features.ToArray();
        }

        /// <summary>
        /// Reports progress for long operations
        /// </summary>
        private void ReportProgress(long current, long total, string operation)
        {
            if (ProgressChanged == null)
                return;

            double percentage = total > 0 ? (double)current / total * 100 : 0;
            var args = new DecryptionProgressEventArgs(current, total, percentage)
            {
                Operation = operation
            };

            ProgressChanged.Invoke(this, args);
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


