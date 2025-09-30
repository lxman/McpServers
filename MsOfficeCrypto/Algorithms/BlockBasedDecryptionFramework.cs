using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MsOfficeCrypto.Structures;
using OpenMcdf;

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// Generic framework for block-based decryption of legacy Office documents
    /// Supports both XOR obfuscation and RC4 CryptoAPI methods
    /// </summary>
    public class BlockBasedDecryptionFramework
    {


        /// <summary>
        /// Decrypts a complete legacy Office document
        /// </summary>
        /// <param name="filePath">Path to encrypted document</param>
        /// <param name="context">Decryption context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Decryption result</returns>
        public static async Task<Dictionary<string, StreamDecryptionResult>> DecryptDocumentAsync(
            string filePath, 
            DecryptionContext context,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var results = new Dictionary<string, StreamDecryptionResult>();

            try
            {
                using RootStorage root = RootStorage.OpenRead(filePath);
                
                // Get streams to decrypt based on the document format
                List<string> streamsToDecrypt = GetStreamsToDecrypt(root, context.OfficeDocumentFormat);

                foreach (string? streamName in streamsToDecrypt)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    StreamDecryptionResult result = await DecryptStreamAsync(root, streamName, context, cancellationToken);
                    results[streamName] = result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorResult = new StreamDecryptionResult
                {
                    Success = false,
                    ErrorMessage = $"Document decryption failed: {ex.Message}"
                };
                results["Error"] = errorResult;
            }

            return results;
        }

        /// <summary>
        /// Decrypts a single stream from a compound document
        /// </summary>
        /// <param name="root">Root storage</param>
        /// <param name="streamName">Stream name to decrypt</param>
        /// <param name="context">Decryption context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream decryption result</returns>
        public static async Task<StreamDecryptionResult> DecryptStreamAsync(
            RootStorage root,
            string streamName,
            DecryptionContext context,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new StreamDecryptionResult();

            try
            {
                // Check if the stream exists
                if (!TryOpenStream(root, streamName))
                {
                    result.Success = false;
                    result.ErrorMessage = $"Stream '{streamName}' not found";
                    return result;
                }

                await using CfbStream stream = root.OpenStream(streamName);
                
                // Read stream data
                var streamData = new byte[stream.Length];
                _ = await stream.ReadAsync(streamData, 0, streamData.Length, cancellationToken);

                // Decrypt based on method and format
                result.DecryptedData = context.EncryptionMethod switch
                {
                    LegacyEncryptionMethod.XorObfuscation => DecryptWithXorObfuscation(
                        streamData, context, streamName),
                    LegacyEncryptionMethod.Rc4CryptoApi => DecryptWithRc4CryptoApi(
                        streamData, context, streamName),
                    _ => throw new NotSupportedException($"Encryption method not supported: {context.EncryptionMethod}")
                };

                result.Success = true;
                result.BlocksProcessed = (streamData.Length + context.BlockSize - 1) / context.BlockSize;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                result.DecryptionTime = stopwatch.Elapsed;
            }

            return result;
        }

        /// <summary>
        /// Decrypts stream data using XOR obfuscation
        /// </summary>
        private static byte[] DecryptWithXorObfuscation(byte[] streamData, DecryptionContext context, string streamName)
        {
            return context.OfficeDocumentFormat switch
            {
                OfficeDocumentFormat.Word when streamName == "WordDocument" => 
                    XorObfuscationHandler.DecryptWordDocumentStream(streamData, context.Password),
                OfficeDocumentFormat.Excel when streamName == "Workbook" => 
                    XorObfuscationHandler.DecryptExcelWorkbookStream(streamData, context.Password),
                OfficeDocumentFormat.PowerPoint => 
                    XorObfuscationHandler.DecryptPowerPointStream(streamData, context.Password),
                _ => XorObfuscationHandler.DecryptData(streamData, context.Password, 0)
            };
        }

        /// <summary>
        /// Decrypts stream data using RC4 CryptoAPI
        /// </summary>
        private static byte[] DecryptWithRc4CryptoApi(byte[] streamData, DecryptionContext context, string streamName)
        {
            if (context.CryptoApiInfo == null)
                throw new InvalidOperationException("CryptoAPI encryption info is required for RC4 decryption");

            return context.OfficeDocumentFormat switch
            {
                OfficeDocumentFormat.Word when streamName == "WordDocument" => 
                    EnhancedRc4CryptoApiHandler.DecryptWordDocument(streamData, context.Password, context.CryptoApiInfo),
                _ => EnhancedRc4CryptoApiHandler.DecryptStream(streamData, context.Password, context.CryptoApiInfo, 0)
            };
        }

        /// <summary>
        /// Gets a list of streams to decrypt based on the document format
        /// </summary>
        private static List<string> GetStreamsToDecrypt(RootStorage root, OfficeDocumentFormat format)
        {
            var streams = new List<string>();

            switch (format)
            {
                case OfficeDocumentFormat.Word:
                    if (TryOpenStream(root, "WordDocument")) streams.Add("WordDocument");
                    if (TryOpenStream(root, "Data")) streams.Add("Data");
                    if (TryOpenStream(root, "0Table")) streams.Add("0Table");
                    if (TryOpenStream(root, "1Table")) streams.Add("1Table");
                    break;

                case OfficeDocumentFormat.Excel:
                    if (TryOpenStream(root, "Workbook")) streams.Add("Workbook");
                    if (TryOpenStream(root, "Book")) streams.Add("Book"); // Excel 5.0/95
                    break;

                case OfficeDocumentFormat.PowerPoint:
                    if (TryOpenStream(root, "PowerPoint Document")) streams.Add("PowerPoint Document");
                    if (TryOpenStream(root, "PP40")) streams.Add("PP40"); // PowerPoint 4.0
                    if (TryOpenStream(root, "Current User")) streams.Add("Current User");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            return streams;
        }

        /// <summary>
        /// Creates decryption context from encryption info
        /// </summary>
        /// <param name="encryptionInfo">Detected encryption information</param>
        /// <param name="password">Password for decryption</param>
        /// <returns>Configured decryption context</returns>
        public static DecryptionContext CreateDecryptionContext(EncryptionInfo encryptionInfo, string password)
        {
            var context = new DecryptionContext
            {
                Password = password
            };

            // Determine document format
            if (!string.IsNullOrEmpty(encryptionInfo.LegacyEncryptionType))
            {
                context.OfficeDocumentFormat = encryptionInfo.LegacyEncryptionType.ToLowerInvariant() switch
                {
                    var type when type.Contains("word") => OfficeDocumentFormat.Word,
                    var type when type.Contains("excel") => OfficeDocumentFormat.Excel,
                    var type when type.Contains("powerpoint") => OfficeDocumentFormat.PowerPoint,
                    _ => OfficeDocumentFormat.Word // Default fallback
                };
            }

            // Determine encryption method
            if (encryptionInfo.ExcelFilePassRecord != null)
            {
                context.EncryptionMethod = encryptionInfo.ExcelFilePassRecord.IsXorObfuscation 
                    ? LegacyEncryptionMethod.XorObfuscation 
                    : LegacyEncryptionMethod.Rc4CryptoApi;

                // Parse CryptoAPI info if needed
                if (context.EncryptionMethod == LegacyEncryptionMethod.Rc4CryptoApi && 
                    encryptionInfo.ExcelFilePassRecord.EncryptionInfo != null)
                {
                    try
                    {
                        context.CryptoApiInfo = EnhancedRc4CryptoApiHandler.ParseEncryptionInfo(
                            encryptionInfo.ExcelFilePassRecord.EncryptionInfo);
                    }
                    catch
                    {
                        // Fallback to XOR if CryptoAPI parsing fails
                        context.EncryptionMethod = LegacyEncryptionMethod.XorObfuscation;
                    }
                }
            }
            else
            {
                // Default to XOR for other formats
                context.EncryptionMethod = LegacyEncryptionMethod.XorObfuscation;
            }

            // Set format-specific properties
            ConfigureFormatSpecificProperties(context);

            return context;
        }

        /// <summary>
        /// Configures format-specific decryption properties
        /// </summary>
        private static void ConfigureFormatSpecificProperties(DecryptionContext context)
        {
            switch (context.OfficeDocumentFormat)
            {
                case OfficeDocumentFormat.Word:
                    context.HeaderSize = 68; // FIB size
                    context.SkipHeader = true;
                    context.BlockSize = 512;
                    break;

                case OfficeDocumentFormat.Excel:
                    context.HeaderSize = 0;
                    context.SkipHeader = false; // BIFF records handle encryption differently
                    context.BlockSize = 1024; // Larger blocks for Excel
                    break;

                case OfficeDocumentFormat.PowerPoint:
                    context.HeaderSize = 0;
                    context.SkipHeader = false;
                    context.BlockSize = 512;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Verifies password against legacy document
        /// </summary>
        /// <param name="encryptionInfo">Encryption information</param>
        /// <param name="password">Password to verify</param>
        /// <returns>True if the password is correct</returns>
        public static bool VerifyPassword(EncryptionInfo encryptionInfo, string password)
        {
            try
            {
                if (encryptionInfo.ExcelFilePassRecord == null)
                    return XorObfuscationHandler.VerifyPassword(password, 0); // Simplified check
                FilePassRecord? filePass = encryptionInfo.ExcelFilePassRecord;
                    
                if (filePass.IsXorObfuscation)
                {
                    // For XOR, we need to extract verifier from encryption info
                    if (filePass.EncryptionInfo == null || filePass.EncryptionInfo.Length < 2)
                        return XorObfuscationHandler.VerifyPassword(password, 0); // Simplified check
                    var verifier = BitConverter.ToUInt16(filePass.EncryptionInfo, 0);
                    return XorObfuscationHandler.VerifyPassword(password, verifier);
                }

                if (!filePass.IsRc4Encryption || filePass.EncryptionInfo == null)
                    return XorObfuscationHandler.VerifyPassword(password, 0); // Simplified check
                try
                {
                    CryptoApiEncryptionInfo cryptoApiInfo = EnhancedRc4CryptoApiHandler.ParseEncryptionInfo(filePass.EncryptionInfo);
                    return EnhancedRc4CryptoApiHandler.VerifyPassword(password, cryptoApiInfo);
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Helper method to safely check if a stream exists
        /// </summary>
        private static bool TryOpenStream(RootStorage root, string streamName)
        {
            try
            {
                using CfbStream stream = root.OpenStream(streamName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reconstructs a complete Office document from decrypted streams
        /// </summary>
        /// <param name="root">Original root storage</param>
        /// <param name="decryptedStreams">Decrypted stream data</param>
        /// <param name="outputPath">Path for the reconstructed document</param>
        public static void ReconstructDocument(
            RootStorage root,
            Dictionary<string, StreamDecryptionResult> decryptedStreams,
            string outputPath)
        {
            // Create a new compound file with decrypted streams
            using var outputFile = new FileStream(outputPath, FileMode.Create);
            using var newRoot = RootStorage.Create(outputFile);

            // Copy all streams, replacing encrypted ones with decrypted versions
            CopyStreamRecursive(root, newRoot, decryptedStreams);

            newRoot.Commit();
        }

        /// <summary>
        /// Recursively copies streams from source to destination, using decrypted versions where available
        /// </summary>
        private static void CopyStreamRecursive(
            RootStorage source,
            RootStorage destination,
            Dictionary<string, StreamDecryptionResult> decryptedStreams)
        {
            // This is a simplified implementation
            // A full implementation would need to handle the complete compound file structure
            foreach (string? streamName in decryptedStreams.Keys)
            {
                if (!decryptedStreams[streamName].Success) continue;
                using CfbStream destStream = destination.CreateStream(streamName);
                destStream.Write(decryptedStreams[streamName].DecryptedData, 0, 
                    decryptedStreams[streamName].DecryptedData.Length);
            }
        }
    }
}