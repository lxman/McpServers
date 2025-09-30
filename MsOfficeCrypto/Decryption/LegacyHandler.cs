using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MsOfficeCrypto.Algorithms;
using MsOfficeCrypto.Exceptions;
using MsOfficeCrypto.Structures;
using OpenMcdf;

namespace MsOfficeCrypto.Decryption
{
    /// <summary>
    /// Handler for legacy Office documents
    /// </summary>
    public static class LegacyHandler
    {
        /// <summary>
        /// Decrypts a legacy Word binary document (pre-2007) using the provided password.
        /// Returns the decrypted document as a byte array.
        /// </summary>
        /// <param name="encryptionInfo"></param>
        /// <param name="password"></param>
        /// <param name="token"></param>
        /// <param name="encryptedData"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static async Task<byte[]> DecryptWordBinaryAsync(byte[] encryptedData, EncryptionInfo encryptionInfo, string password, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                // Open the compound file
                using var inputStream = new MemoryStream(encryptedData);
                using RootStorage rootStorage = RootStorage.Open(inputStream);

                // Get the WordDocument stream (main document content)
                if (!rootStorage.TryOpenStream("WordDocument", out CfbStream? wordDocStream))
                    throw new CorruptedEncryptionInfoException("WordDocument stream not found in compound file");

                // Read the WordDocument stream
                wordDocStream.Position = 0;
                var wordDocData = new byte[wordDocStream.Length];
                _ = wordDocStream.Read(wordDocData, 0, wordDocData.Length);

                // Decrypt based on the encryption method
                byte[] decryptedWordDoc = DecryptWordDocumentStream(
                    wordDocData, 
                    encryptionInfo, 
                    password, 
                    token);

                // Create output compound file with decrypted WordDocument
                using var outputStream = new MemoryStream();
                using (var outputRoot = RootStorage.Create(outputStream))
                {
                    // Copy all non-WordDocument streams/storages
                    CopyNonWordDocumentContent(rootStorage, outputRoot);

                    // Add decrypted WordDocument stream
                    CfbStream decryptedStream = outputRoot.CreateStream("WordDocument");
                    decryptedStream.Write(decryptedWordDoc, 0, decryptedWordDoc.Length);

                    outputRoot.Commit();
                }

                return outputStream.ToArray();
            }, token);
        }

        /// <summary>
        /// Decrypts a legacy Excel binary document (pre-2007) using the provided password.
        /// Returns the decrypted document as a byte array.
        /// </summary>
        /// <param name="encryptionInfo"></param>
        /// <param name="password"></param>
        /// <param name="token"></param>
        /// <param name="encryptedData"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static async Task<byte[]> DecryptExcelBiffAsync(byte[] encryptedData, EncryptionInfo encryptionInfo, string password, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                // Validate we have the necessary encryption information
                if (encryptionInfo.ExcelFilePassRecord == null)
                    throw new CorruptedEncryptionInfoException("Excel FilePass record not found in encryption info");

                FilePassRecord? filePassRecord = encryptionInfo.ExcelFilePassRecord;

                // Open the compound file
                using var inputStream = new MemoryStream(encryptedData);
                using RootStorage rootStorage = RootStorage.Open(inputStream);

                // Get the Workbook stream (contains BIFF records)
                if (!rootStorage.TryOpenStream("Workbook", out CfbStream? workbookStream))
                    throw new CorruptedEncryptionInfoException("Workbook stream not found in compound file");

                // Read all BIFF records from the workbook stream
                byte[] decryptedRecords = DecryptBiffRecords(workbookStream, filePassRecord, password, token);

                // Create an output compound file with the decrypted workbook
                using var outputStream = new MemoryStream();
                using (var outputRoot = RootStorage.Create(outputStream))
                {
                    // Copy all streams except Workbook
                    CopyNonWorkbookStreams(rootStorage, outputRoot);

                    // Add a decrypted Workbook stream
                    CfbStream decryptedWorkbookStream = outputRoot.CreateStream("Workbook");
                    decryptedWorkbookStream.Write(decryptedRecords, 0, decryptedRecords.Length);

                    outputRoot.Commit();
                }

                return outputStream.ToArray();
            }, token);
        }

        /// <summary>
        /// Decrypts a legacy PowerPoint binary document (pre-2007) using the provided password.
        /// Returns the decrypted document as a byte array.
        /// </summary>
        /// <param name="encryptionInfo"></param>
        /// <param name="password"></param>
        /// <param name="token"></param>
        /// <param name="encryptedData"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static async Task<byte[]> DecryptPowerPointBinaryAsync(byte[] encryptedData, EncryptionInfo encryptionInfo, string password, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // Check encryption method
            string encryptionMethod = encryptionInfo.LegacyEncryptionMethod ?? "Unknown";

            if (encryptionMethod.Contains("RC4", StringComparison.OrdinalIgnoreCase) ||
                encryptionMethod.Contains("CryptoAPI", StringComparison.OrdinalIgnoreCase))
            {
                // Use the new RC4 CryptoAPI handler
                return await DecryptPowerPointRc4CryptoApiAsync(encryptedData, password, token);
            }

            if (!encryptionMethod.Contains("XOR", StringComparison.OrdinalIgnoreCase))
                throw new UnsupportedEncryptionException(
                    $"Unsupported PowerPoint encryption method: {encryptionMethod}");
            // XOR obfuscation - handle separately
            using var inputStream = new MemoryStream(encryptedData);
            using RootStorage rootStorage = RootStorage.Open(inputStream);
        
            using var outputStream = new MemoryStream();
            using (var outputRoot = RootStorage.Create(outputStream))
            {
                DecryptPowerPointStreams(rootStorage, outputRoot, encryptionInfo, password, token);
                outputRoot.Commit();
            }
        
            return outputStream.ToArray();
        }

        #region Word

        /// <summary>
        /// Decrypts the WordDocument stream based on the encryption method
        /// </summary>
        private static byte[] DecryptWordDocumentStream(
            byte[] wordDocData, 
            EncryptionInfo encryptionInfo, 
            string password,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // Determine encryption method from EncryptionInfo
            string encryptionMethod = encryptionInfo.LegacyEncryptionMethod ?? "Unknown";

            if (encryptionMethod.Contains("XOR", StringComparison.OrdinalIgnoreCase))
            {
                // XOR obfuscation
                return XorObfuscationHandler.DecryptWordDocumentStream(wordDocData, password);
            }
            if (encryptionMethod.Contains("RC4", StringComparison.OrdinalIgnoreCase))
            {
                // RC4 CryptoAPI encryption
                // For Word, we need to parse the EncryptionInfo stream if available
                CryptoApiEncryptionInfo cryptoApiInfo;

                if (encryptionInfo.EncryptionInfoData != null && encryptionInfo.EncryptionInfoData.Length > 0)
                {
                    // Parse from EncryptionInfo stream (similar to modern OOXML)
                    cryptoApiInfo = EnhancedRc4CryptoApiHandler.ParseEncryptionInfo(
                        encryptionInfo.EncryptionInfoData);
                }
                else
                {
                    // For older Word documents, encryption info might be in FIB
                    // This is a fallback - create basic encryption info
                    throw new UnsupportedEncryptionException(
                        "Word RC4 encryption requires EncryptionInfo stream data. " +
                        "This may be an older format that stores encryption info in FIB only.");
                }

                return EnhancedRc4CryptoApiHandler.DecryptWordDocument(
                    wordDocData, 
                    password, 
                    cryptoApiInfo);
            }
            else
            {
                throw new UnsupportedEncryptionException(
                    $"Unsupported Word encryption method: {encryptionMethod}");
            }
        }

        /// <summary>
        /// Copies all non-WordDocument content from source to destination
        /// Preserves document structure including tables, data, and VBA projects
        /// </summary>
        private static void CopyNonWordDocumentContent(RootStorage source, RootStorage destination)
        {
            // Common streams in Word documents (besides WordDocument)
            string[] commonStreams = 
            {
                "0Table",                // Table stream (when bit is 0 in FIB)
                "1Table",                // Table stream (when bit is 1 in FIB)
                "Data",                  // Data stream for embedded objects
                "\x01" + "CompObj",           // Compound Object metadata
                "\x05" + "DocumentSummaryInformation", // Document properties
                "\x05" + "SummaryInformation",         // Summary properties
                "WordDocument",          // Skip - will be added separately
            };

            foreach (string streamName in commonStreams)
            {
                if (streamName == "WordDocument")
                    continue; // Skip - this is being decrypted

                try
                {
                    if (source.TryOpenStream(streamName, out CfbStream? sourceStream))
                    {
                        CfbStream destStream = destination.CreateStream(streamName);
                        
                        sourceStream.Position = 0;
                        var buffer = new byte[sourceStream.Length];
                        _ = sourceStream.Read(buffer, 0, buffer.Length);
                        destStream.Write(buffer, 0, buffer.Length);
                    }
                }
                catch
                {
                    // Skip streams that can't be copied
                }
            }

            // Copy VBA Macros storage if present
            CopyVbaProject(source, destination);
        }

        /// <summary>
        /// Copies VBA Macros storage from source to destination
        /// </summary>
        private static void CopyVbaProject(RootStorage source, RootStorage destination)
        {
            try
            {
                // Check if a VBA project exists
                // VBA macros are stored in a "Macros" storage
                if (!source.TryOpenStorage("Macros", out Storage? macrosStorage)) return;
                Storage destMacros = destination.CreateStorage("Macros");
                CopyStorage(macrosStorage, destMacros);
            }
            catch
            {
                // VBA project is not present or can't be copied
            }
        }

        /// <summary>
        /// Recursively copies a storage and all its contents
        /// </summary>
        private static void CopyStorage(Storage source, Storage destination)
        {
            // This is a simplified version - a full implementation would enumerate all entries
            // For now, we'll handle the most common VBA streams
            string[] vbaStreams = 
            {
                "VBA/_VBA_PROJECT",
                "VBA/dir",
                "VBA/PROJECT",
                "VBA/PROJECTwm",
            };

            foreach (string streamPath in vbaStreams)
            {
                try
                {
                    if (!source.TryOpenStream(streamPath, out CfbStream? sourceStream)) continue;
                    CfbStream destStream = destination.CreateStream(streamPath);
                        
                    sourceStream.Position = 0;
                    var buffer = new byte[sourceStream.Length];
                    _ = sourceStream.Read(buffer, 0, buffer.Length);
                    destStream.Write(buffer, 0, buffer.Length);
                }
                catch
                {
                    // Skip streams that can't be copied
                }
            }
        }
        
        #endregion
        
        #region Excel
        
        /// <summary>
        /// Decrypts BIFF records from the Workbook stream
        /// </summary>
        private static byte[] DecryptBiffRecords(
            CfbStream workbookStream, 
            FilePassRecord filePassRecord, 
            string password,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // Read entire workbook stream into memory
            workbookStream.Position = 0;
            var workbookData = new byte[workbookStream.Length];
            _ = workbookStream.Read(workbookData, 0, workbookData.Length);

            // Decrypt based on the encryption type from FilePass record
            if (filePassRecord.IsXorObfuscation)
            {
                // XOR obfuscation - use a dedicated Excel decryption method
                return XorObfuscationHandler.DecryptExcelWorkbookStream(workbookData, password);
            }

            if (!filePassRecord.IsRc4Encryption)
                throw new UnsupportedEncryptionException(
                    $"Unsupported Excel encryption type: {filePassRecord.EncryptionMethod}");
            // RC4 CryptoAPI encryption - parse encryption info from FilePass record
            if (filePassRecord.EncryptionInfo == null)
                throw new CorruptedEncryptionInfoException("FilePass record EncryptionInfo is null");

            // Parse the raw encryption info bytes into structured CryptoApiEncryptionInfo
            CryptoApiEncryptionInfo cryptoApiInfo = EnhancedRc4CryptoApiHandler.ParseEncryptionInfo(filePassRecord.EncryptionInfo);

            // Use EnhancedRc4CryptoApiHandler to decrypt the entire stream
            // The handler handles block-by-block decryption internally
            return EnhancedRc4CryptoApiHandler.DecryptStream(
                workbookData, 
                password, 
                cryptoApiInfo, 
                streamOffset: 0);
        }

        /// <summary>
        /// Copies all non-Workbook streams from source to destination
        /// </summary>
        private static void CopyNonWorkbookStreams(RootStorage source, RootStorage destination)
        {
            // List of common streams in Excel files (besides Workbook)
            string[] commonStreams = 
            {
                "\x01" + "CompObj",           // Compound Object metadata
                "\x05" + "DocumentSummaryInformation", // Document properties
                "\x05" + "SummaryInformation",         // Summary properties
                "_VBA_PROJECT_CUR",      // VBA project (if present)
                "VBA",                   // VBA storage (actually a storage, not stream)
            };

            foreach (string streamName in commonStreams)
            {
                try
                {
                    // Try to get stream from source
                    if (!source.TryOpenStream(streamName, out CfbStream? sourceStream)) continue;
                    CfbStream destStream = destination.CreateStream(streamName);
                        
                    sourceStream.Position = 0;
                    var buffer = new byte[sourceStream.Length];
                    _ = sourceStream.Read(buffer, 0, buffer.Length);
                    destStream.Write(buffer, 0, buffer.Length);
                }
                catch
                {
                    // Skip streams that can't be copied
                    // This is normal - not all files have all streams
                }
            }

            // Note: For full fidelity, you might need to list all entries
            // This requires OpenMcdf v2.x API with VisitEntries, or reflection
            // For basic Excel decryption, Workbook stream is enough
        }
        
        #endregion
        
        #region PowerPoint
        
        /// <summary>
        /// Decrypts a PowerPoint (.ppt) file encrypted with RC4 CryptoAPI
        /// Based on MS-PPT Section 2.3.7
        /// </summary>
        private static async Task<byte[]> DecryptPowerPointRc4CryptoApiAsync(
            byte[] encryptedData,
            string password,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Open the encrypted compound file
                using var inputStream = new MemoryStream(encryptedData);
                using RootStorage sourceRoot = RootStorage.Open(inputStream);

                // Step 1: Read Current User stream
                if (!sourceRoot.TryOpenStream("Current User", out CfbStream? currentUserStream))
                    throw new CorruptedEncryptionInfoException("Current User stream not found in PowerPoint file");

                byte[] currentUserData = ReadCfbStreamData(currentUserStream);

                // Step 2: Check if encrypted and parse encryption info
                if (!PowerPointRc4CryptoApiHandler.IsRc4CryptoApiEncrypted(currentUserData))
                    throw new NotEncryptedException("PowerPoint file is not encrypted with RC4 CryptoAPI");

                CryptoApiEncryptionInfo encryptionInfo = 
                    PowerPointRc4CryptoApiHandler.ParseCryptSessionContainer(currentUserData);

                // Step 3: Verify password
                if (!EnhancedRc4CryptoApiHandler.VerifyPassword(password, encryptionInfo))
                    throw new InvalidPasswordException("Invalid password for PowerPoint document");

                // Step 4: Create a decrypted compound file
                using var outputStream = new MemoryStream();
                using (var destRoot = RootStorage.Create(outputStream))
                {
                    // Copy Current User stream (not encrypted)
                    CfbStream decryptedCurrentUser = destRoot.CreateStream("Current User");
                    decryptedCurrentUser.Write(currentUserData, 0, currentUserData.Length);

                    // Step 5: Decrypt the PowerPoint Document stream
                    if (sourceRoot.TryOpenStream("PowerPoint Document", out CfbStream? pptDocStream))
                    {
                        byte[] encryptedDocData = ReadCfbStreamData(pptDocStream);
                        
                        // Parse document structure to get persist objects
                        byte[] decryptedDocData = await Task.Run(() => 
                            DecryptPowerPointDocumentStructure(
                                encryptedDocData, 
                                password, 
                                encryptionInfo), 
                            cancellationToken);

                        CfbStream decryptedDocStream = destRoot.CreateStream("PowerPoint Document");
                        decryptedDocStream.Write(decryptedDocData, 0, decryptedDocData.Length);
                    }

                    // Step 6: Decrypt Pictures stream
                    if (sourceRoot.TryOpenStream("Pictures", out CfbStream? picturesStream))
                    {
                        byte[] encryptedPictures = ReadCfbStreamData(picturesStream);
                        
                        byte[] decryptedPictures = await Task.Run(() =>
                            PowerPointRc4CryptoApiHandler.DecryptPicturesStream(
                                encryptedPictures,
                                password,
                                encryptionInfo),
                            cancellationToken);

                        CfbStream decryptedPicturesStream = destRoot.CreateStream("Pictures");
                        decryptedPicturesStream.Write(decryptedPictures, 0, decryptedPictures.Length);
                    }

                    // Step 7: Handle Summary Information
                    DecryptPowerPointSummaryInfo(sourceRoot, destRoot, password, encryptionInfo);

                    // Step 8: Copy other streams (VBA, CompObj, etc.)
                    CopyPowerPointUnencryptedStreams(sourceRoot, destRoot);

                    destRoot.Commit();
                }

                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new LegacyDecryptionException($"Failed to decrypt PowerPoint file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decrypts PowerPoint Document stream by parsing persist object structure
        /// </summary>
        private static byte[] DecryptPowerPointDocumentStructure(
            byte[] encryptedDocData,
            string password,
            CryptoApiEncryptionInfo encryptionInfo)
        {
            using var reader = new BinaryReader(new MemoryStream(encryptedDocData));
            using var decryptedStream = new MemoryStream();
            using var writer = new BinaryWriter(decryptedStream);

            // Find UserEditAtom (NOT encrypted)
            long userEditOffset = PowerPointStructureParser.FindUserEditAtom(encryptedDocData);
            
            if (userEditOffset < 0)
                throw new CorruptedEncryptionInfoException("UserEditAtom not found in PowerPoint Document stream");

            // Parse the persist directory to get object locations
            Dictionary<uint, PersistObjectEntry> persistObjects = PowerPointStructureParser.ParsePersistDirectory(reader, userEditOffset);

            // Process each record in the stream
            reader.BaseStream.Position = 0;
            
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                long recordStart = reader.BaseStream.Position;
                RecordHeader? header = PowerPointStructureParser.ReadRecordHeader(reader);
                
                if (header == null)
                    break;

                // Check if this record should be encrypted
                if (PowerPointStructureParser.IsRecordEncrypted(header.RecType))
                {
                    // Find persist object ID for this record
                    uint? persistId = FindPersistIdForOffset(persistObjects, recordStart);
                    if (persistId is null)
                        continue;
                    
                    // Read encrypted data
                    byte[] encryptedData = reader.ReadBytes((int)header.RecLen);
                    
                    // Decrypt using persist object ID as the block number
                    byte[] decryptedData = PowerPointRc4CryptoApiHandler.DecryptPowerPointPersistObject(
                        encryptedData,
                        password,
                        encryptionInfo,
                        persistId.Value);
                    
                    WriteRecordHeader(writer, header);
                    
                    // Write decrypted data
                    writer.Write(decryptedData);
                }
                else
                {
                    // Copy unencrypted record as-is
                    reader.BaseStream.Position = recordStart;
                    byte[] recordData = reader.ReadBytes(8 + (int)header.RecLen);
                    writer.Write(recordData);
                }
            }

            return decryptedStream.ToArray();
        }
        
        /// <summary>
        /// Writes a RecordHeader to the output stream
        /// MS-PPT Section 2.3.1: RecordHeader structure
        /// </summary>
        private static void WriteRecordHeader(BinaryWriter writer, RecordHeader header)
        {
            // Reconstruct the 16-bit recVer and recInstance field
            var verAndInstance = (ushort)((header.RecInstance << 4) | header.RecVer);
            writer.Write(verAndInstance);
            writer.Write(header.RecType);
            writer.Write(header.RecLen);
        }


        /// <summary>
        /// Finds the persist object ID for a given file offset
        /// </summary>
        private static uint? FindPersistIdForOffset(
            Dictionary<uint, PersistObjectEntry> persistObjects,
            long fileOffset)
        {
            var entry = persistObjects.Values.FirstOrDefault(e => e.FileOffset == fileOffset);
            return entry?.PersistId;  // ✅ Returns null if not found
        }

        /// <summary>
        /// Decrypts PowerPoint Summary Information streams
        /// </summary>
        private static void DecryptPowerPointSummaryInfo(
            RootStorage sourceRoot,
            RootStorage destRoot,
            string password,
            CryptoApiEncryptionInfo encryptionInfo)
        {
            // Check for Encrypted Summary stream
            if (sourceRoot.TryOpenStream("EncryptedSummary", out CfbStream? encryptedSummaryStream))
            {
                byte[] encryptedData = ReadCfbStreamData(encryptedSummaryStream);
                
                byte[] decryptedData = PowerPointRc4CryptoApiHandler.DecryptEncryptedSummaryStream(
                    encryptedData,
                    password,
                    encryptionInfo);

                // Write as regular Summary Information
                CfbStream summaryStream = destRoot.CreateStream("\x05" + "SummaryInformation");
                summaryStream.Write(decryptedData, 0, decryptedData.Length);
            }
            
            // Copy Document Summary Info if it exists and is not encrypted
            if (!sourceRoot.TryOpenStream("\x05" + "DocumentSummaryInformation", out CfbStream? docSummaryStream))
                return;
            byte[] docSummaryData = ReadCfbStreamData(docSummaryStream);
            CfbStream targetDocSummary = destRoot.CreateStream("\x05" + "DocumentSummaryInformation");
            targetDocSummary.Write(docSummaryData, 0, docSummaryData.Length);
        }

        /// <summary>
        /// Copies unencrypted streams from source to target
        /// </summary>
        private static void CopyPowerPointUnencryptedStreams(RootStorage sourceRoot, RootStorage destRoot)
        {
            // Copy VBA project if exists
            if (sourceRoot.TryOpenStorage("_VBA_PROJECT_CUR", out Storage? vbaStorage))
            {
                // VBA streams are typically not encrypted in PowerPoint
                Storage targetVba = destRoot.CreateStorage("_VBA_PROJECT_CUR");
                CopyStorageRecursivePowerPoint(vbaStorage, targetVba);
            }

            // Copy other common streams that are not encrypted
            string[] unencryptedStreamNames = 
            {
                "CompObj",
                "\x01" + "CompObj",
                "Ole",
            };

            foreach (string streamName in unencryptedStreamNames)
            {
                CopyStreamIfExistsPowerPoint(sourceRoot, destRoot, streamName);
            }
        }

        /// <summary>
        /// Recursively copies storage and its contents (PowerPoint version)
        /// </summary>
        private static void CopyStorageRecursivePowerPoint(Storage source, Storage target)
        {
            // List common VBA streams to copy
            string[] vbaStreams = 
            {
                "VBA/_VBA_PROJECT",
                "VBA/dir",
                "VBA/PROJECT",
                "VBA/PROJECTwm",
                "_VBA_PROJECT",
                "dir",
                "PROJECT",
                "PROJECTwm",
            };

            foreach (string streamPath in vbaStreams)
            {
                try
                {
                    if (!source.TryOpenStream(streamPath, out CfbStream? sourceStream)) continue;
                    byte[] data = ReadCfbStreamData(sourceStream);
                    CfbStream targetStream = target.CreateStream(streamPath);
                    targetStream.Write(data, 0, data.Length);
                }
                catch
                {
                    // Stream doesn't exist or can't be copied
                }
            }
    
            // Try to copy common VBA sub-storages
            if (!source.TryOpenStorage("VBA", out Storage? vbaStorage)) return;
            Storage targetVba = target.CreateStorage("VBA");
            // Copy streams within VBA storage
            string[] vbaSubStreams = { "_VBA_PROJECT", "dir", "PROJECT", "PROJECTwm" };
            foreach (string streamName in vbaSubStreams)
            {
                CopyStreamBetweenStorages(vbaStorage, targetVba, streamName);
            }
        }

        /// <summary>
        /// Helper to copy a stream from one storage to another
        /// </summary>
        private static void CopyStreamBetweenStorages(Storage source, Storage target, string streamName)
        {
            try
            {
                if (!source.TryOpenStream(streamName, out CfbStream? sourceStream)) return;
                byte[] data = ReadCfbStreamData(sourceStream);
                CfbStream targetStream = target.CreateStream(streamName);
                targetStream.Write(data, 0, data.Length);
            }
            catch
            {
                // Stream doesn't exist or can't be copied
            }
        }

        /// <summary>
        /// Copies a stream from source to destination if it exists (PowerPoint version)
        /// </summary>
        private static void CopyStreamIfExistsPowerPoint(RootStorage source, RootStorage dest, string streamName)
        {
            try
            {
                if (!source.TryOpenStream(streamName, out CfbStream? sourceStream)) return;
                byte[] data = ReadCfbStreamData(sourceStream);
                CfbStream destStream = dest.CreateStream(streamName);
                destStream.Write(data, 0, data.Length);
            }
            catch
            {
                // Stream doesn't exist or can't be copied - this is normal
            }
        }

        /// <summary>
        /// Helper to read all data from a CfbStream
        /// </summary>
        private static byte[] ReadCfbStreamData(CfbStream stream)
        {
            stream.Position = 0;
            var data = new byte[stream.Length];
            _ = stream.Read(data, 0, (int)stream.Length);
            return data;
        }
        
        /// <summary>
        /// Decrypts PowerPoint streams based on encryption info
        /// </summary>
        private static void DecryptPowerPointStreams(
            RootStorage source, 
            RootStorage destination,
            EncryptionInfo encryptionInfo,
            string password,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            PowerPointEncryptionInfo pptInfo = encryptionInfo.PowerPointEncryptionInfo!;
            string encryptionMethod = encryptionInfo.LegacyEncryptionMethod ?? "RC4 CryptoAPI";

            // Handle Current User stream (primary indicator of encryption)
            if (pptInfo.HasEncryptedCurrentUser)
            {
                DecryptCurrentUserStream(source, destination, encryptionMethod, password, token);
            }
            else
            {
                // Copy Current User stream as-is if not encrypted
                CopyStreamIfExists(source, destination, "Current User");
            }

            // Handle PowerPoint Document stream (main content)
            DecryptPowerPointDocumentStream(source, destination, encryptionMethod, password, token);

            // Handle Summary Information if encrypted
            if (pptInfo.HasEncryptedSummaryInfo)
            {
                DecryptSummaryInfoStream(source, destination, encryptionMethod, password, token);
            }
            else
            {
                CopyStreamIfExists(source, destination, "\x05" + "SummaryInformation");
            }

            // Copy other common PowerPoint streams
            CopyCommonPowerPointStreams(source, destination);
        }

        /// <summary>
        /// Decrypts the Current User stream
        /// </summary>
        private static void DecryptCurrentUserStream(
            RootStorage source,
            RootStorage destination,
            string encryptionMethod,
            string password,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!source.TryOpenStream("Current User", out CfbStream? currentUserStream))
            {
                // Current User stream missing - this is unusual but not fatal
                return;
            }

            currentUserStream.Position = 0;
            var encryptedData = new byte[currentUserStream.Length];
            _ = currentUserStream.Read(encryptedData, 0, encryptedData.Length);

            byte[] decryptedData = DecryptPowerPointData(encryptedData, encryptionMethod, password);

            CfbStream destStream = destination.CreateStream("Current User");
            destStream.Write(decryptedData, 0, decryptedData.Length);
        }

        /// <summary>
        /// Decrypts the main PowerPoint Document stream
        /// </summary>
        private static void DecryptPowerPointDocumentStream(
            RootStorage source,
            RootStorage destination,
            string encryptionMethod,
            string password,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // PowerPoint Document stream might be named "PowerPoint Document" or just "Document"
            if (!source.TryOpenStream("PowerPoint Document", out CfbStream? docStream))
            {
                source.TryOpenStream("Document", out docStream);
            }

            if (docStream == null)
            {
                throw new CorruptedEncryptionInfoException(
                    "PowerPoint Document stream not found in compound file");
            }

            docStream.Position = 0;
            var encryptedData = new byte[docStream.Length];
            _ = docStream.Read(encryptedData, 0, encryptedData.Length);

            byte[] decryptedData = DecryptPowerPointData(encryptedData, encryptionMethod, password);

            CfbStream destStream = destination.CreateStream("PowerPoint Document");
            destStream.Write(decryptedData, 0, decryptedData.Length);
        }

        /// <summary>
        /// Decrypts the Summary Information stream if encrypted
        /// </summary>
        private static void DecryptSummaryInfoStream(
            RootStorage source,
            RootStorage destination,
            string encryptionMethod,
            string password,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!source.TryOpenStream("\x05" + "SummaryInformation", out CfbStream? summaryStream))
            {
                return;
            }

            summaryStream.Position = 0;
            var encryptedData = new byte[summaryStream.Length];
            _ = summaryStream.Read(encryptedData, 0, encryptedData.Length);

            byte[] decryptedData = DecryptPowerPointData(encryptedData, encryptionMethod, password);

            CfbStream destStream = destination.CreateStream("\x05" + "SummaryInformation");
            destStream.Write(decryptedData, 0, decryptedData.Length);
        }

        /// <summary>
        /// Decrypts PowerPoint data using the appropriate method
        /// </summary>
        private static byte[] DecryptPowerPointData(
            byte[] encryptedData,
            string encryptionMethod,
            string password)
        {
            if (encryptionMethod.Contains("XOR", StringComparison.OrdinalIgnoreCase))
            {
                // XOR obfuscation
                return XorObfuscationHandler.DecryptPowerPointStream(encryptedData, password);
            }
            if (encryptionMethod.Contains("RC4", StringComparison.OrdinalIgnoreCase))
            {
                // RC4 CryptoAPI encryption
                // For PowerPoint, we need CryptoApiEncryptionInfo
                // This is typically stored in the Current User stream or document structure
                
                // TODO: Parse PowerPoint-specific encryption info from Current User stream
                // For now, throw an exception indicating this needs implementation
                throw new UnsupportedEncryptionException(
                    "PowerPoint RC4 CryptoAPI decryption requires parsing CryptSession containers. " +
                    "This is not yet implemented. XOR obfuscation is supported.");
            }
            throw new UnsupportedEncryptionException(
                $"Unsupported PowerPoint encryption method: {encryptionMethod}");
        }

        /// <summary>
        /// Copies common PowerPoint streams that are typically not encrypted
        /// </summary>
        private static void CopyCommonPowerPointStreams(RootStorage source, RootStorage destination)
        {
            // Common non-encrypted PowerPoint streams
            string[] commonStreams = 
            {
                "\x01" + "CompObj",                        // Compound Object metadata
                "\x05" + "DocumentSummaryInformation",     // Document properties
                "Pictures",                           // Embedded images (might be a storage)
            };

            foreach (string streamName in commonStreams)
            {
                CopyStreamIfExists(source, destination, streamName);
            }

            // Copy Pictures storage if present (contains embedded images)
            CopyPicturesStorage(source, destination);
        }

        /// <summary>
        /// Copies the Pictures storage which contains embedded images
        /// </summary>
        private static void CopyPicturesStorage(RootStorage source, RootStorage destination)
        {
            try
            {
                if (source.TryOpenStorage("Pictures", out Storage? picturesStorage))
                {
                    Storage destPictures = destination.CreateStorage("Pictures");
                    // In a full implementation, we'd recursively copy all picture streams
                    // For now this is a placeholder for basic structure preservation
                }
            }
            catch
            {
                // Pictures storage is not present or can't be copied
            }
        }

        /// <summary>
        /// Copies a stream from source to destination if it exists
        /// </summary>
        private static void CopyStreamIfExists(RootStorage source, RootStorage destination, string streamName)
        {
            try
            {
                if (!source.TryOpenStream(streamName, out CfbStream? sourceStream)) return;
                CfbStream destStream = destination.CreateStream(streamName);
                    
                sourceStream.Position = 0;
                var buffer = new byte[sourceStream.Length];
                _ = sourceStream.Read(buffer, 0, buffer.Length);
                destStream.Write(buffer, 0, buffer.Length);
            }
            catch
            {
                // Stream doesn't exist or can't be copied - this is normal
            }
        }
        
        #endregion
    }
}