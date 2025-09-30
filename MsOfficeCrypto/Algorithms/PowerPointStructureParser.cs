using System.Collections.Generic;
using System.IO;
using MsOfficeCrypto.Structures;

namespace MsOfficeCrypto.Algorithms
{
    /// <summary>
    /// Helper class for parsing PowerPoint document structure
    /// Identifies persist objects and their encryption requirements
    /// Based on MS-PPT Section 2.3.7
    /// </summary>
    public class PowerPointStructureParser
    {
        // Record type constants
        private const ushort USER_EDIT_ATOM = 0x0FF5;
        private const ushort PERSIST_DIRECTORY_ATOM = 0x1772;
        private const ushort CRYPT_SESSION10_CONTAINER = 0x2F14;

        /// <summary>
        /// Reads a record header from the stream
        /// MS-PPT Section 2.3.1 - RecordHeader
        /// </summary>
        public static RecordHeader? ReadRecordHeader(BinaryReader reader)
        {
            if (reader.BaseStream.Position + 8 > reader.BaseStream.Length)
                return null;

            long offset = reader.BaseStream.Position;
            
            // Read recVer (4 bits) and recInstance (12 bits) as ushort
            ushort verAndInstance = reader.ReadUInt16();
            ushort recType = reader.ReadUInt16();
            uint recLen = reader.ReadUInt32();

            return new RecordHeader
            {
                RecVer = (ushort)(verAndInstance & 0x0F),
                RecInstance = (ushort)((verAndInstance >> 4) & 0x0FFF),
                RecType = recType,
                RecLen = recLen,
                FileOffset = offset
            };
        }

        /// <summary>
        /// Finds the UserEditAtom record in the PowerPoint Document stream
        /// MS-PPT Section 2.3.3 - UserEditAtom
        /// </summary>
        public static long FindUserEditAtom(byte[] documentStream)
        {
            using var reader = new BinaryReader(new MemoryStream(documentStream));
            
            while (reader.BaseStream.Position < reader.BaseStream.Length - 8)
            {
                RecordHeader? header = ReadRecordHeader(reader);
                if (header == null)
                    break;

                if (header.RecType == USER_EDIT_ATOM)
                    return header.FileOffset;

                // Skip to the next record
                if (header.RecLen > 0 && 
                    reader.BaseStream.Position + header.RecLen <= reader.BaseStream.Length)
                {
                    reader.BaseStream.Position += header.RecLen;
                }
                else
                {
                    break;
                }
            }

            return -1; // Not found
        }

        /// <summary>
        /// Parses the PersistDirectoryAtom to get persist object locations
        /// MS-PPT Section 2.3.4 - PersistDirectoryAtom
        /// </summary>
        public static Dictionary<uint, PersistObjectEntry> ParsePersistDirectory(
            BinaryReader reader, 
            long userEditAtomOffset)
        {
            var persistObjects = new Dictionary<uint, PersistObjectEntry>();

            // UserEditAtom contains the offset to PersistDirectoryAtom
            reader.BaseStream.Position = userEditAtomOffset;
            
            RecordHeader? header = ReadRecordHeader(reader);
            if (header is null || header.RecType != USER_EDIT_ATOM) return persistObjects;

            // Read UserEditAtom fields to find persist directory offset
            // This is simplified - full implementation would parse all fields
            // Skip to persistDirectoryOffset field (at offset +22 in atom data)
            reader.BaseStream.Position += 22;
            uint persistDirectoryOffset = reader.ReadUInt32();

            // Go to the persist directory
            reader.BaseStream.Position = persistDirectoryOffset;
            RecordHeader? dirHeader = ReadRecordHeader(reader);
            
            if (dirHeader is null || dirHeader.RecType != PERSIST_DIRECTORY_ATOM)
                return persistObjects;

            // Parse persist directory entries
            long endPosition = reader.BaseStream.Position + dirHeader.RecLen;
            
            while (reader.BaseStream.Position < endPosition)
            {
                uint persistId = reader.ReadUInt32();
                uint offset = reader.ReadUInt32();
                
                persistObjects[persistId] = new PersistObjectEntry
                {
                    PersistId = persistId,
                    FileOffset = offset,
                    IsEncrypted = true // Most of the persist objects are encrypted
                };
            }

            return persistObjects;
        }

        /// <summary>
        /// Determines if a specific record should be encrypted
        /// MS-PPT Section 2.3.7: UserEditAtom and PersistDirectoryAtom are NOT encrypted
        /// </summary>
        public static bool IsRecordEncrypted(ushort recordType)
        {
            return recordType switch
            {
                USER_EDIT_ATOM => false,
                PERSIST_DIRECTORY_ATOM => false,
                _ => true
            };
        }

        /// <summary>
        /// Gets the CryptSession10Container persist object ID from UserEditAtom
        /// MS-PPT Section 2.3.3: encryptSessionPersistIdRef field
        /// </summary>
        public static uint GetCryptSessionPersistId(BinaryReader reader, long userEditAtomOffset)
        {
            reader.BaseStream.Position = userEditAtomOffset;
            
            RecordHeader? header = ReadRecordHeader(reader);
            if (header?.RecType != USER_EDIT_ATOM)
                return 0;

            // Skip to encryptSessionPersistIdRef field
            // This is at offset +16 in the UserEditAtom data
            reader.BaseStream.Position += 16;
            return reader.ReadUInt32();
        }
    }
}