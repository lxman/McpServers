using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;

namespace MsOfficeCrypto.Decryption
{
    /// <summary>
    /// High-performance streaming document decryptor for large encrypted Office files
    /// Provides memory-efficient decryption with progress reporting and cancellation support
    /// </summary>
    public class StreamingDocumentDecryptor : IDisposable
    {
        /// <summary>
        /// Progress reporting event
        /// </summary>
        public event EventHandler<DecryptionProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// The buffer size to use for decryption
        /// </summary>
        public int BufferSize { get; set; } = DEFAULT_BUFFER_SIZE;
        
        /// <summary>
        /// Total bytes to decrypt
        /// </summary>
        public long TotalBytesToDecrypt { get; private set; }
        
        /// <summary>
        /// Bytes decrypted so far
        /// </summary>
        public long BytesDecrypted { get; private set; }

        private readonly EncryptionInfo _encryptionInfo;
        private readonly Stream _encryptedStream;
        private readonly bool _disposeStream;
        private bool _disposed;

        // Streaming configuration
        private const int DEFAULT_BUFFER_SIZE = 64 * 1024; // 64KB chunks
        private const int MIN_BUFFER_SIZE = 4 * 1024; // 4KB minimum
        private const int MAX_BUFFER_SIZE = 4 * 1024 * 1024; // 4MB maximum

        /// <summary>
        /// Creates a streaming decryptor for large encrypted documents
        /// </summary>
        /// <param name="encryptionInfo">Parsed encryption information</param>
        /// <param name="encryptedStream">Stream containing encrypted data</param>
        /// <param name="disposeStream">Whether to dispose the stream when this decryptor is disposed</param>
        public StreamingDocumentDecryptor(EncryptionInfo encryptionInfo, Stream encryptedStream, bool disposeStream = true)
        {
            _encryptionInfo = encryptionInfo ?? throw new ArgumentNullException(nameof(encryptionInfo));
            _encryptedStream = encryptedStream ?? throw new ArgumentNullException(nameof(encryptedStream));
            _disposeStream = disposeStream;

            if (!encryptedStream.CanRead)
                throw new ArgumentException("Stream must be readable", nameof(encryptedStream));

            TotalBytesToDecrypt = encryptedStream.CanSeek ? encryptedStream.Length : -1;
            ValidateBufferSize();
        }
        
        #region Public methods

        /// <summary>
        /// Decrypts the document to the specified output stream asynchronously
        /// </summary>
        /// <param name="password">Decryption password</param>
        /// <param name="outputStream">Output stream for decrypted data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the decryption operation</returns>
        public async Task DecryptToStreamAsync(string password, Stream outputStream, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            
            if (!outputStream.CanWrite)
                throw new ArgumentException("Output stream must be writable", nameof(outputStream));

            ThrowIfDisposed();

            // Verify password first
            if (!VerifyPassword(password))
                throw new InvalidPasswordException("Invalid password provided");

            try
            {
                await PerformStreamingDecryptionAsync(password, outputStream, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is InvalidPasswordException))
            {
                throw new DecryptionException($"Streaming decryption failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decrypts the document to a file asynchronously
        /// </summary>
        /// <param name="password">Decryption password</param>
        /// <param name="outputPath">Output file path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the decryption operation</returns>
        public async Task DecryptToFileAsync(string password, string outputPath, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, 
                FileShare.None, BufferSize, FileOptions.SequentialScan);
            
            await DecryptToStreamAsync(password, outputStream, cancellationToken);
        }

        /// <summary>
        /// Verifies if the provided password is correct
        /// </summary>
        /// <param name="password">Password to verify</param>
        /// <returns>True if password is correct</returns>
        public bool VerifyPassword(string password)
        {
            ThrowIfDisposed();

            if (_encryptionInfo.Header == null || _encryptionInfo.Verifier == null)
                throw new InvalidOperationException("Encryption info is incomplete");

            return _encryptionInfo.Verifier.VerifyPassword(password, _encryptionInfo.Header);
        }

        /// <summary>
        /// Gets decryption information and estimates
        /// </summary>
        /// <returns>Streaming decryption information</returns>
        public StreamingDecryptionInfo GetDecryptionInfo()
        {
            ThrowIfDisposed();

            return new StreamingDecryptionInfo
            {
                Algorithm = _encryptionInfo.Header?.GetAlgorithmName() ?? "Unknown",
                HashAlgorithm = _encryptionInfo.Header?.GetHashAlgorithmName() ?? "Unknown",
                KeySize = _encryptionInfo.Header?.KeySize ?? 0,
                TotalBytesToDecrypt = TotalBytesToDecrypt,
                BufferSize = BufferSize,
                EncryptionType = _encryptionInfo.GetEncryptionTypeName(),
                SupportsProgress = TotalBytesToDecrypt > 0,
                EstimatedMemoryUsage = EstimateMemoryUsage()
            };
        }

        /// <summary>
        /// Creates a streaming decryptor from an encrypted file
        /// </summary>
        /// <param name="filePath">Path to the encrypted file</param>
        /// <returns>Streaming decryptor instance</returns>
        public static StreamingDocumentDecryptor FromFile(string filePath)
        {
            if (!OfficeCryptoDetector.IsEncryptedOfficeDocument(filePath))
                throw new NotEncryptedException($"Document is not encrypted: {filePath}");

            EncryptionInfo encryptionInfo = OfficeCryptoDetector.GetEncryptionInfo(filePath);
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            // TODO: Position stream to EncryptedPackage data start
            // This would require additional compound file navigation
            
            return new StreamingDocumentDecryptor(encryptionInfo, fileStream, true);
        }
        
        #endregion

        #region Private methods

        /// <summary>
        /// Performs the actual streaming decryption
        /// </summary>
        private async Task PerformStreamingDecryptionAsync(string password, Stream outputStream, 
            CancellationToken cancellationToken)
        {
            // Derive decryption key
            byte[] decryptionKey = _encryptionInfo.Verifier!.DeriveKey(password, _encryptionInfo.Header!);

            // Create crypto transform based on algorithm
            using ICryptoTransform decryptor = CreateDecryptor(decryptionKey);
            
            // Reset position and counters
            if (_encryptedStream.CanSeek)
                _encryptedStream.Position = 0;
            
            BytesDecrypted = 0;

            var buffer = new byte[BufferSize];
            var decryptedBuffer = new byte[BufferSize * 2]; // Account for padding variations

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesRead = await _encryptedStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead == 0)
                    break; // End of stream

                // Decrypt the chunk
                if (bytesRead < BufferSize)
                {
                    // Final block - use TransformFinalBlock
                    byte[] finalBlock = decryptor.TransformFinalBlock(buffer, 0, bytesRead);
                    await outputStream.WriteAsync(finalBlock, 0, finalBlock.Length, cancellationToken);
                    BytesDecrypted += bytesRead;
                    break;
                }
                else
                {
                    // Intermediate block - use TransformBlock
                    int decryptedBytes = decryptor.TransformBlock(buffer, 0, bytesRead, decryptedBuffer, 0);
                    await outputStream.WriteAsync(decryptedBuffer, 0, decryptedBytes, cancellationToken);
                }

                BytesDecrypted += bytesRead;
                
                // Report progress
                ReportProgress();
            }

            await outputStream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Creates the appropriate crypto transform for decryption
        /// </summary>
        private ICryptoTransform CreateDecryptor(byte[] key)
        {
            string algorithm = _encryptionInfo.Header!.GetAlgorithmName();
            
            return algorithm switch
            {
                "AES-128" => CreateAesDecryptor(key, 128),
                "AES-192" => CreateAesDecryptor(key, 192),
                "AES-256" => CreateAesDecryptor(key, 256),
                _ => throw new UnsupportedEncryptionException($"Unsupported algorithm for streaming: {algorithm}")
            };
        }

        /// <summary>
        /// Creates AES decryptor for streaming
        /// </summary>
        private static ICryptoTransform CreateAesDecryptor(byte[] key, int keySize)
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.ECB; // MS-OFFCRYPTO typically uses ECB
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = keySize;
            aes.Key = EnsureKeyLength(key, keySize);
            
            return aes.CreateDecryptor();
        }

        /// <summary>
        /// Ensures key is the correct length
        /// </summary>
        private static byte[] EnsureKeyLength(byte[] key, int keySize)
        {
            int requiredBytes = keySize / 8;
            
            if (key.Length == requiredBytes)
                return key;

            var adjustedKey = new byte[requiredBytes];
            Array.Copy(key, 0, adjustedKey, 0, Math.Min(key.Length, requiredBytes));
            return adjustedKey;
        }

        /// <summary>
        /// Reports decryption progress
        /// </summary>
        private void ReportProgress()
        {
            if (ProgressChanged == null || TotalBytesToDecrypt <= 0)
                return;

            double progressPercentage = (double)BytesDecrypted / TotalBytesToDecrypt * 100;
            var args = new DecryptionProgressEventArgs(BytesDecrypted, TotalBytesToDecrypt, progressPercentage);
            ProgressChanged.Invoke(this, args);
        }

        /// <summary>
        /// Estimates memory usage for the decryption operation
        /// </summary>
        private long EstimateMemoryUsage()
        {
            // Base memory: buffers + crypto transform overhead
            long baseMemory = BufferSize * 3; // Input, output, and transform buffers
            
            // Add encryption structure overhead
            baseMemory += 1024; // Approximate overhead for crypto objects
            
            return baseMemory;
        }

        /// <summary>
        /// Validates buffer size
        /// </summary>
        private void ValidateBufferSize()
        {
            if (BufferSize < MIN_BUFFER_SIZE)
                BufferSize = MIN_BUFFER_SIZE;
            else if (BufferSize > MAX_BUFFER_SIZE)
                BufferSize = MAX_BUFFER_SIZE;
            
            // Ensure the buffer size is multiple of 16 for AES block alignment
            BufferSize = (BufferSize / 16) * 16;
            if (BufferSize == 0)
                BufferSize = 16;
        }

        /// <summary>
        /// Throws if the object has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StreamingDocumentDecryptor));
        }
        
        #endregion

        /// <summary>
        /// Disposes the decryptor and optionally the underlying stream
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            if (_disposeStream)
                _encryptedStream.Dispose();
                
            _disposed = true;
        }
    }
}
