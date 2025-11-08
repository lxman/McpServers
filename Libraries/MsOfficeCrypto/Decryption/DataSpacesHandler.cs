using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;
using OpenMcdf;

namespace MsOfficeCrypto.Decryption
{
    /// <summary>
    /// DataSpaces transformation handler for MS-OFFCRYPTO specification
    /// Handles DataSpaces-based encryption and transformation chains
    /// </summary>
    public class DataSpacesHandler : IDisposable
    {
        private readonly RootStorage _rootStorage;
        private readonly Storage _dataSpacesStorage;
        private readonly Dictionary<string, DataSpaceDefinition> _dataSpaces;
        private readonly Dictionary<string, TransformDefinition> _transforms;

        /// <summary>
        /// Handles DataSpaces transformation for a given root storage
        /// </summary>
        /// <param name="rootStorage"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="OfficeCryptoException"></exception>
        public DataSpacesHandler(RootStorage rootStorage)
        {
            _rootStorage = rootStorage ?? throw new ArgumentNullException(nameof(rootStorage));
            
            try
            {
                _dataSpacesStorage = rootStorage.OpenStorage("DataSpaces");
            }
            catch (Exception ex)
            {
                throw new OfficeCryptoException("DataSpaces storage not found or corrupted", ex);
            }

            _dataSpaces = new Dictionary<string, DataSpaceDefinition>();
            _transforms = new Dictionary<string, TransformDefinition>();
            
            InitializeDataSpaces();
        }

        /// <summary>
        /// Checks if DataSpaces transformation is available
        /// </summary>
        public static bool IsDataSpacesEncrypted(RootStorage rootStorage)
        {
            try
            {
                var dataSpaces = rootStorage.OpenStorage("DataSpaces");
                
                // Look for common DataSpaces indicators
                var hasDataSpaceMap = HasStream(dataSpaces, "DataSpaceMap");
                var hasTransformInfo = HasStorage(dataSpaces, "TransformInfo");
                
                return hasDataSpaceMap && hasTransformInfo;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Decrypts content using DataSpaces transformation
        /// </summary>
        /// <param name="streamName">Name of the protected stream</param>
        /// <param name="password">Decryption password</param>
        /// <returns>Decrypted content</returns>
        public byte[] DecryptDataSpacesStream(string streamName, string password)
        {
            if (string.IsNullOrEmpty(streamName))
                throw new ArgumentException("Stream name cannot be null or empty", nameof(streamName));

            try
            {
                // Step 1: Find the data space for this stream
                var dataSpace = FindDataSpaceForStream(streamName);
                
                // Step 2: Get the transformation chain
                var transformChain = GetTransformChain(dataSpace.TransformReference);
                
                // Step 3: Read the protected content
                var protectedContent = ReadProtectedContent(streamName);
                
                // Step 4: Apply reverse transformation chain
                var decryptedContent = ApplyReverseTransforms(protectedContent, transformChain, password);
                
                return decryptedContent;
            }
            catch (Exception ex)
            {
                throw new DecryptionException($"DataSpaces decryption failed for stream '{streamName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets information about available DataSpaces
        /// </summary>
        /// <returns>DataSpaces information</returns>
        public DataSpacesInfo GetDataSpacesInfo()
        {
            var info = new DataSpacesInfo
            {
                DataSpaceCount = _dataSpaces.Count,
                TransformCount = _transforms.Count,
                DataSpaces = new List<DataSpaceDefinition>(_dataSpaces.Values),
                Transforms = new List<TransformDefinition>(_transforms.Values),
                HasDataSpaceMap = HasStream(_dataSpacesStorage, "DataSpaceMap"),
                HasDataSpaceInfo = HasStream(_dataSpacesStorage, "DataSpaceInfo"),
                HasTransformInfo = HasStorage(_dataSpacesStorage, "TransformInfo")
            };

            return info;
        }

        /// <summary>
        /// Initializes DataSpaces by parsing the structure
        /// </summary>
        private void InitializeDataSpaces()
        {
            try
            {
                // Parse DataSpaceMap
                if (HasStream(_dataSpacesStorage, "DataSpaceMap"))
                {
                    ParseDataSpaceMap();
                }

                // Parse DataSpaceInfo  
                if (HasStream(_dataSpacesStorage, "DataSpaceInfo"))
                {
                    ParseDataSpaceInfo();
                }

                // Parse TransformInfo
                if (HasStorage(_dataSpacesStorage, "TransformInfo"))
                {
                    ParseTransformInfo();
                }
            }
            catch (Exception ex)
            {
                throw new CorruptedEncryptionInfoException($"Failed to initialize DataSpaces: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses the DataSpaceMap stream
        /// </summary>
        private void ParseDataSpaceMap()
        {
            using var mapStream = _dataSpacesStorage.OpenStream("DataSpaceMap");
            var mapData = ReadStreamData(mapStream);

            // DataSpaceMap format parsing (simplified)
            // This would need to be implemented according to the full specification
            using var reader = new BinaryReader(new MemoryStream(mapData));
            
            try
            {
                // Header length
                var headerLength = reader.ReadUInt32();
                
                // Entry count
                var entryCount = reader.ReadUInt32();
                
                // Parse entries
                for (uint i = 0; i < entryCount; i++)
                {
                    var entry = ParseDataSpaceMapEntry(reader);
                    if (entry != null)
                    {
                        _dataSpaces[entry.Name] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new CorruptedEncryptionInfoException($"DataSpaceMap parsing failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses a DataSpaceMap entry
        /// </summary>
        private DataSpaceDefinition? ParseDataSpaceMapEntry(BinaryReader reader)
        {
            try
            {
                // Length of the entry
                var entryLength = reader.ReadUInt32();
                var startPosition = reader.BaseStream.Position;

                // Reference component count
                var referenceComponentCount = reader.ReadUInt32();
                
                // Read reference components (stream names)
                var referenceComponents = new List<string>();
                for (uint i = 0; i < referenceComponentCount; i++)
                {
                    var componentLength = reader.ReadUInt32();
                    var componentData = reader.ReadBytes((int)componentLength);
                    var component = Encoding.Unicode.GetString(componentData).TrimEnd('\0');
                    referenceComponents.Add(component);
                }

                // Data space name length
                var dataSpaceNameLength = reader.ReadUInt32();
                var dataSpaceNameData = reader.ReadBytes((int)dataSpaceNameLength);
                var dataSpaceName = Encoding.Unicode.GetString(dataSpaceNameData).TrimEnd('\0');

                return new DataSpaceDefinition
                {
                    Name = dataSpaceName,
                    ReferenceComponents = referenceComponents,
                    TransformReference = dataSpaceName // Will be resolved later
                };
            }
            catch (Exception)
            {
                return null; // Skip malformed entries
            }
        }

        /// <summary>
        /// Parses the DataSpaceInfo stream
        /// </summary>
        private void ParseDataSpaceInfo()
        {
            using var infoStream = _dataSpacesStorage.OpenStream("DataSpaceInfo");
            var infoData = ReadStreamData(infoStream);

            // DataSpaceInfo parsing would be implemented here
            // This contains additional metadata about data spaces
        }

        /// <summary>
        /// Parses the TransformInfo storage
        /// </summary>
        private void ParseTransformInfo()
        {
            var transformInfoStorage = _dataSpacesStorage.OpenStorage("TransformInfo");
            
            // Enumerate transform definitions
            try
            {
                // Look for common transform storages
                ParseTransformStorage(transformInfoStorage, "StrongEncryptionDataSpace");
                ParseTransformStorage(transformInfoStorage, "StrongEncryptionTransform");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: TransformInfo parsing incomplete: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses a specific transform storage
        /// </summary>
        private void ParseTransformStorage(Storage transformInfoStorage, string transformName)
        {
            try
            {
                var transformStorage = transformInfoStorage.OpenStorage(transformName);
                
                // Parse primary transform stream
                if (HasStream(transformStorage, "Primary"))
                {
                    using var primaryStream = transformStorage.OpenStream("Primary");
                    var primaryData = ReadStreamData(primaryStream);
                    
                    var transform = ParseTransformDefinition(transformName, primaryData);
                    if (transform != null)
                    {
                        _transforms[transformName] = transform;
                    }
                }
            }
            catch (Exception)
            {
                // Transform storage not found or corrupted - continue
            }
        }

        /// <summary>
        /// Parses a transform definition
        /// </summary>
        private TransformDefinition? ParseTransformDefinition(string name, byte[] data)
        {
            try
            {
                return new TransformDefinition
                {
                    Name = name,
                    TransformType = "Encryption", // Default assumption
                    ConfigurationData = data
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the DataSpace for a given stream
        /// </summary>
        private DataSpaceDefinition FindDataSpaceForStream(string streamName)
        {
            foreach (var dataSpace in _dataSpaces.Values)
            {
                if (dataSpace.ReferenceComponents.Contains(streamName))
                {
                    return dataSpace;
                }
            }

            throw new OfficeCryptoException($"No DataSpace found for stream: {streamName}");
        }

        /// <summary>
        /// Gets the transformation chain for a DataSpace
        /// </summary>
        private List<TransformDefinition> GetTransformChain(string transformReference)
        {
            var chain = new List<TransformDefinition>();
            
            if (_transforms.TryGetValue(transformReference, out var transform))
            {
                chain.Add(transform);
            }

            return chain;
        }

        /// <summary>
        /// Reads protected content from a stream
        /// </summary>
        private byte[] ReadProtectedContent(string streamName)
        {
            try
            {
                using var stream = _rootStorage.OpenStream(streamName);
                return ReadStreamData(stream);
            }
            catch (Exception ex)
            {
                throw new OfficeCryptoException($"Failed to read protected content from stream '{streamName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Applies reverse transformation chain to decrypt content
        /// </summary>
        private byte[] ApplyReverseTransforms(byte[] content, List<TransformDefinition> transforms, string password)
        {
            var currentContent = content;

            // Apply transforms in reverse order
            for (var i = transforms.Count - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                currentContent = ApplyReverseTransform(currentContent, transform, password);
            }

            return currentContent;
        }

        /// <summary>
        /// Applies a single reverse transform
        /// </summary>
        private byte[] ApplyReverseTransform(byte[] content, TransformDefinition transform, string password)
        {
            return transform.TransformType switch
            {
                "Encryption" => DecryptTransformContent(content, transform, password),
                "Compression" => DecompressContent(content, transform),
                _ => throw new UnsupportedEncryptionException($"Unsupported transform type: {transform.TransformType}")
            };
        }

        /// <summary>
        /// Decrypts content using transform configuration
        /// </summary>
        private byte[] DecryptTransformContent(byte[] content, TransformDefinition transform, string password)
        {
            // This would implement the specific decryption based on transform configuration
            // For now, delegate to standard decryption
            try
            {
                // Extract encryption parameters from transform configuration
                // This is a simplified implementation
                
                // Use AES-128 ECB as default for DataSpaces
                using var aes = Aes.Create();
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;
                
                // Derive key from password (simplified)
                var key = DeriveDataSpacesKey(password, transform.ConfigurationData);
                aes.Key = EnsureKeyLength(key, 128);
                
                using var decryptor = aes.CreateDecryptor();
                return decryptor.TransformFinalBlock(content, 0, content.Length);
            }
            catch (Exception ex)
            {
                throw new DecryptionException($"Transform decryption failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decompresses content using transform configuration
        /// </summary>
        private byte[] DecompressContent(byte[] content, TransformDefinition transform)
        {
            // Implement decompression based on transform type
            // This would handle various compression algorithms
            throw new NotImplementedException("Compression transforms not yet implemented");
        }

        /// <summary>
        /// Derives encryption key for DataSpaces
        /// </summary>
        private static byte[] DeriveDataSpacesKey(string password, byte[] configData)
        {
            // Simplified key derivation - real implementation would parse config data
            var passwordBytes = Encoding.Unicode.GetBytes(password);
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(passwordBytes);
            
            // Return first 16 bytes for AES-128
            var key = new byte[16];
            Array.Copy(hash, 0, key, 0, 16);
            return key;
        }

        /// <summary>
        /// Ensures key is the correct length
        /// </summary>
        private static byte[] EnsureKeyLength(byte[] key, int keyBits)
        {
            var requiredBytes = keyBits / 8;
            
            if (key.Length == requiredBytes)
                return key;

            var adjustedKey = new byte[requiredBytes];
            Array.Copy(key, 0, adjustedKey, 0, Math.Min(key.Length, requiredBytes));
            return adjustedKey;
        }

        /// <summary>
        /// Reads all data from a stream
        /// </summary>
        private static byte[] ReadStreamData(CfbStream stream)
        {
            var data = new byte[stream.Length];
            var totalRead = 0;
            
            while (totalRead < data.Length)
            {
                var read = stream.Read(data, totalRead, data.Length - totalRead);
                if (read == 0)
                    break;
                totalRead += read;
            }
            
            return data;
        }

        /// <summary>
        /// Checks if a stream exists in a storage
        /// </summary>
        private static bool HasStream(Storage storage, string streamName)
        {
            try
            {
                using var stream = storage.OpenStream(streamName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a storage exists in a storage
        /// </summary>
        private static bool HasStorage(Storage storage, string storageName)
        {
            try
            {
                var subStorage = storage.OpenStorage(storageName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _rootStorage.Dispose();
        }
    }
}
