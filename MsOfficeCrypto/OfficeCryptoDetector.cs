using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;
using OpenMcdf;

namespace MsOfficeCrypto
{
    /// <summary>
    /// Detects and analyzes MS-OFFCRYPTO encrypted Office documents
    /// Updated for OpenMcdf v3 API (RootStorage.OpenRead) with enhanced legacy format support
    /// </summary>
    public static class OfficeCryptoDetector
    {
        #region Public methods

        /// <summary>
        /// Determines if a file is an encrypted Office document
        /// </summary>
        /// <param name="filePath">Path to the Office file</param>
        /// <returns>True if the file is encrypted, false otherwise</returns>
        public static bool IsEncryptedOfficeDocument(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                using RootStorage root = RootStorage.OpenRead(filePath);
                return IsEncryptedOleDocument(root);
            }
            catch (Exception)
            {
                // Not a valid OLE compound file or other error
                return false;
            }
        }

        /// <summary>
        /// Determines if a stream contains an encrypted Office document
        /// </summary>
        /// <param name="stream">Stream containing the Office file data</param>
        /// <returns>True if the stream contains an encrypted document</returns>
        public static bool IsEncryptedOfficeDocument(Stream stream)
        {
            if (!stream.CanRead)
                return false;

            long originalPosition = stream.Position;
            var memoryStream = new MemoryStream();
            try
            {
                stream.Position = 0;
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
        
                using RootStorage root = RootStorage.Open(memoryStream);
                return IsEncryptedOleDocument(root);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                // Restore the original stream position
                if (stream.CanSeek)
                    stream.Position = originalPosition;
        
                // Clean up our memory stream
                memoryStream.Dispose();
            }
        }

        /// <summary>
        /// Gets detailed encryption information from an Office document
        /// </summary>
        /// <param name="filePath">Path to the encrypted Office file</param>
        /// <returns>EncryptionInfo structure with detailed information</returns>
        /// <exception cref="FileNotFoundException">File not found</exception>
        /// <exception cref="NotEncryptedException">File is not encrypted</exception>
        public static EncryptionInfo GetEncryptionInfo(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            try
            {
                using RootStorage root = RootStorage.OpenRead(filePath);
                return ExtractEncryptionInfo(root, filePath);
            }
            catch (Exception ex)
            {
                throw new OfficeCryptoException($"Failed to read compound file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets detailed encryption information from a stream
        /// </summary>
        /// <param name="stream">Stream containing the Office file data</param>
        /// <returns>EncryptionInfo structure with detailed information</returns>
        public static EncryptionInfo GetEncryptionInfo(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            // Copy to MemoryStream to avoid RootStorage disposing the original stream
            var memoryStream = new MemoryStream();
            long originalPosition = stream.Position;
            try
            {
                stream.Position = 0;
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
        
                using RootStorage root = RootStorage.Open(memoryStream);
                return ExtractEncryptionInfo(root, "<stream>");
            }
            catch (Exception ex)
            {
                throw new OfficeCryptoException($"Failed to read compound file from stream: {ex.Message}", ex);
            }
            finally
            {
                // Restore the original stream position
                if (stream.CanSeek)
                    stream.Position = originalPosition;
        
                // Clean up our memory stream
                memoryStream.Dispose();
            }
        }

        /// <summary>
        /// Extracts the encrypted package data from an Office document
        /// </summary>
        /// <param name="filePath">Path to the encrypted Office file</param>
        /// <param name="encryptionInfo"></param>
        /// <returns>Encrypted package data bytes</returns>
        /// <exception cref="FileNotFoundException">The file was not found</exception>
        /// <exception cref="NotEncryptedException">File is not encrypted</exception>
        /// <exception cref="OfficeCryptoException">Failed to extract data</exception>
        public static byte[] ExtractEncryptedPackageData(string filePath, EncryptionInfo encryptionInfo)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            if (!IsEncryptedOfficeDocument(filePath))
                throw new NotEncryptedException($"Document is not encrypted: {filePath}");

            try
            {
                using RootStorage root = RootStorage.OpenRead(filePath);
                return ExtractEncryptedPackageData(root, encryptionInfo);
            }
            catch (Exception ex)
            {
                throw new OfficeCryptoException($"Failed to extract encrypted package data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extracts the encrypted package data from a stream
        /// </summary>
        /// <param name="stream">Stream containing the Office file data</param>
        /// <param name="encryptionInfo"></param>
        /// <returns>Encrypted package data bytes</returns>
        /// <exception cref="NotEncryptedException">Stream does not contain encrypted document</exception>
        /// <exception cref="OfficeCryptoException">Failed to extract data</exception>
        public static byte[] ExtractEncryptedPackageData(Stream stream, EncryptionInfo encryptionInfo)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!IsEncryptedOfficeDocument(stream))
                throw new NotEncryptedException("Stream does not contain an encrypted document");

            // Copy to MemoryStream to avoid RootStorage disposing the original stream
            var memoryStream = new MemoryStream();
            long originalPosition = stream.Position;
            try
            {
                stream.Position = 0;
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
        
                using RootStorage root = RootStorage.Open(memoryStream);
                return ExtractEncryptedPackageData(root, encryptionInfo);
            }
            catch (Exception ex)
            {
                throw new OfficeCryptoException($"Failed to extract encrypted package data from stream: {ex.Message}", ex);
            }
            finally
            {
                // Restore original stream position
                if (stream.CanSeek)
                    stream.Position = originalPosition;
        
                // Clean up our memory stream
                memoryStream.Dispose();
            }
        }        
        /// <summary>
        /// Internal method to extract encrypted package data from an open compound file
        /// </summary>
        /// <param name="root">Open root storage</param>
        /// <param name="encryptionInfo"></param>
        /// <returns>Encrypted package data bytes</returns>
        public static byte[] ExtractEncryptedPackageData(RootStorage root, EncryptionInfo encryptionInfo)
        {
            if (!TryOpenStream(root, "EncryptedPackage"))
            {
                throw new OfficeCryptoException("EncryptedPackage stream not found");
            }

            using CfbStream encryptedPackageStream = root.OpenStream("EncryptedPackage");

            if (encryptedPackageStream.Length < 8)
            {
                throw new OfficeCryptoException("EncryptedPackage stream is too small for header");
            }

            var packageSizeBytes = new byte[8];
            _ = encryptedPackageStream.Read(packageSizeBytes, 0, 8);
            encryptionInfo.UnencryptedPackageSize = BitConverter.ToUInt64(packageSizeBytes, 0);

            var encryptedDataLength = (int)(encryptedPackageStream.Length - 8);
            var encryptedData = new byte[encryptedDataLength];
            int bytesRead = encryptedPackageStream.Read(encryptedData, 0, encryptedDataLength);

            return bytesRead != encryptedDataLength
                ? throw new OfficeCryptoException($"Failed to read complete EncryptedPackage data. Expected {encryptedDataLength} bytes, read {bytesRead} bytes")
                : encryptedData;
        }

        /// <summary>
        /// Checks if an OLE compound document is encrypted
        /// </summary>
        /// <param name="root">Root storage of the compound file</param>
        /// <returns>True if encrypted</returns>
        public static bool IsEncryptedOleDocument(RootStorage root)
        {
            // Method 1: Check for EncryptionInfo stream (ECMA-376 Agile/Standard encryption)
            return TryOpenStream(root, "EncryptionInfo") ||
                   // Method 2: Check for encrypted package (ECMA-376)
                   TryOpenStream(root, "EncryptedPackage");
        }

        /// <summary>
        /// Extracts detailed encryption information from the compound file
        /// </summary>
        public static EncryptionInfo ExtractEncryptionInfo(RootStorage root, string source)
        {
            if (!IsEncryptedOleDocument(root))
                throw new NotEncryptedException($"Document is not encrypted: {source}");

            var encInfo = new EncryptionInfo
            {
                Source = source,
                HasDataSpaces = TryOpenStorage(root, "DataSpaces"),
                DetectedAt = DateTime.UtcNow
            };

            // Extract EncryptionInfo stream data (ECMA-376)
            if (TryOpenStream(root, "EncryptionInfo"))
            {
                ExtractModernEncryptionInfo(root, encInfo);
            }

            // Check for EncryptedPackage
            if (TryOpenStream(root, "EncryptedPackage"))
            {
                using CfbStream encPackageStream = root.OpenStream("EncryptedPackage");
                encInfo.EncryptedPackageSize = (int)encPackageStream.Length;
            }

            // Analyze DataSpaces if present
            if (encInfo.HasDataSpaces)
            {
                AnalyzeDataSpaces(root, encInfo);
            }

            return encInfo;
        }

        #endregion
        
        #region Private methods

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
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Helper method to safely check if a storage exists
        /// </summary>
        private static bool TryOpenStorage(RootStorage root, string storageName)
        {
            try
            {
                Storage storage = root.OpenStorage(storageName);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Parses version information from EncryptionInfo data
        /// </summary>
        private static VersionInfo ParseVersionInfo(byte[] data)
        {
            if (data.Length < 4)
                return new VersionInfo { RawVersion = 0 };

            // Version info is stored in little-endian format
            uint versionMajor = BitConverter.ToUInt16(data, 0);
            uint versionMinor = BitConverter.ToUInt16(data, 2);

            return new VersionInfo
            {
                VersionMajor = versionMajor,
                VersionMinor = versionMinor,
                RawVersion = BitConverter.ToUInt32(data, 0)
            };
        }

        /// <summary>
        /// Analyzes DataSpaces storage for transform information
        /// </summary>
        private static void AnalyzeDataSpaces(RootStorage root, EncryptionInfo encInfo)
        {
            try
            {
                Storage dataSpaces = root.OpenStorage("DataSpaces");

                // Look for common DataSpaces streams using try/catch
                try
                {
                    using CfbStream dataSpaceMapStream = dataSpaces.OpenStream("DataSpaceMap");
                    encInfo.HasDataSpaceMap = true;
                }
                catch { encInfo.HasDataSpaceMap = false; }

                try
                {
                    using CfbStream dataSpaceInfoStream = dataSpaces.OpenStream("DataSpaceInfo");
                    encInfo.HasDataSpaceInfo = true;
                }
                catch { encInfo.HasDataSpaceInfo = false; }

                try
                {
                    Storage transformInfoStorage = dataSpaces.OpenStorage("TransformInfo");
                    encInfo.HasTransformInfo = true;
                }
                catch { encInfo.HasTransformInfo = false; }
            }
            catch (Exception)
            {
                // DataSpaces analysis failed - not critical
                encInfo.HasDataSpaceMap = false;
                encInfo.HasDataSpaceInfo = false;
                encInfo.HasTransformInfo = false;
            }
        }
        
        /// <summary>
        /// Extracts modern OOXML encryption information
        /// </summary>
        private static void ExtractModernEncryptionInfo(RootStorage root, EncryptionInfo encInfo)
        {
            using CfbStream encStream = root.OpenStream("EncryptionInfo");
            encInfo.EncryptionInfoSize = (int)encStream.Length;
            encInfo.EncryptionInfoData = new byte[encStream.Length];
            _ = encStream.Read(encInfo.EncryptionInfoData, 0, encInfo.EncryptionInfoData.Length);

            // Parse version info from the first 4 bytes
            if (encInfo.EncryptionInfoData.Length < 4) return;
            encInfo.VersionInfo = ParseVersionInfo(encInfo.EncryptionInfoData);

            // Phase 2: Parse EncryptionHeader and EncryptionVerifier
            try
            {
                (encInfo.Header, encInfo.Verifier) = EncryptionInfoParser.Parse(
                    encInfo.EncryptionInfoData,
                    encInfo.VersionInfo);

                // Validate the parsed structures
                EncryptionInfoParser.ValidateHeader(encInfo.Header, encInfo.VersionInfo);
                EncryptionInfoParser.ValidateVerifier(encInfo.Verifier);
            }
            catch (Exception ex)
            {
                // Parsing failed - log but don't fail the detection
                Console.WriteLine($"Warning: Failed to parse EncryptionInfo structures: {ex.Message}");
                // Keep the raw data available for manual inspection
            }

            // For Agile encryption, extract additional metadata
            if (encInfo.GetEncryptionTypeName() != "Agile") return;
            try
            {
                // Re-parse XML to extract Agile-specific metadata
                var xmlData = new byte[encInfo.EncryptionInfoData.Length - 8];
                Array.Copy(encInfo.EncryptionInfoData, 8, xmlData, 0, xmlData.Length);
                
                string xmlString = Encoding.UTF8.GetString(xmlData);
                XDocument doc = XDocument.Parse(xmlString);
                
                XNamespace encNs = "http://schemas.microsoft.com/office/2006/encryption";
                XNamespace keyEncNs = "http://schemas.microsoft.com/office/2006/keyEncryptor/password";
                
                // Extract keyData
                XElement? keyDataElem = doc.Root?.Element(encNs + "keyData");
                if (keyDataElem != null)
                {
                    encInfo.AgileHashAlgorithm = keyDataElem.Attribute("hashAlgorithm")?.Value;
                    encInfo.AgileCipherAlgorithm = keyDataElem.Attribute("cipherAlgorithm")?.Value;
                    encInfo.AgileCipherChaining = keyDataElem.Attribute("cipherChaining")?.Value;
                    encInfo.AgileBlockSize = int.Parse(keyDataElem.Attribute("blockSize")?.Value ?? "16");
                    
                    string saltValue = keyDataElem.Attribute("saltValue")?.Value ?? string.Empty;
                    if (!string.IsNullOrEmpty(saltValue))
                    {
                        encInfo.AgileKeyData = Convert.FromBase64String(saltValue);
                    }
                }
                
                // Extract encryptedKey
                XElement? encryptedKeyElem = doc.Root?
                    .Element(encNs + "keyEncryptors")?
                    .Elements(encNs + "keyEncryptor")
                    .FirstOrDefault(e => e.Attribute("uri")?.Value?.Contains("password") == true)?
                    .Element(keyEncNs + "encryptedKey");

                if (encryptedKeyElem == null) return;
                encInfo.AgileSpinCount = int.Parse(encryptedKeyElem.Attribute("spinCount")?.Value ?? "100000");
                
                string passwordSaltValue = encryptedKeyElem.Attribute("saltValue")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(passwordSaltValue))
                {
                    encInfo.AgilePasswordSalt = Convert.FromBase64String(passwordSaltValue);
                }
                    
                string verifierHashInput = encryptedKeyElem.Attribute("encryptedVerifierHashInput")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(verifierHashInput))
                {
                    encInfo.AgileVerifierHashInput = Convert.FromBase64String(verifierHashInput);
                }
                    
                string verifierHashValue = encryptedKeyElem.Attribute("encryptedVerifierHashValue")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(verifierHashValue))
                {
                    encInfo.AgileVerifierHashValue = Convert.FromBase64String(verifierHashValue);
                }
                    
                string encryptedKeyValue = encryptedKeyElem.Attribute("encryptedKeyValue")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(encryptedKeyValue))
                {
                    encInfo.AgileEncryptedKeyValue = Convert.FromBase64String(encryptedKeyValue);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not extract Agile metadata: {ex.Message}");
            }
        }
        
        #endregion
    }
}
