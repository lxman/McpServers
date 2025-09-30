using System;
using System.IO;
using MsOfficeCrypto.Structures;
using OpenMcdf;

namespace MsOfficeCrypto.Utils
{
    /// <summary>
    /// BIFF (Binary Interchange File Format) parser for Excel legacy files
    /// Implements detection of FilePass record (0x2F) which indicates encryption
    /// </summary>
    public static class BiffParser
    {
        private const ushort FILEPASS_RECORD_TYPE = 0x002F;
        private const ushort BOF_RECORD_TYPE = 0x0809; // BIFF8 BOF
        private const ushort EOF_RECORD_TYPE = 0x000A;

        /// <summary>
        /// Checks if an Excel BIFF stream contains encryption indicators
        /// </summary>
        /// <param name="stream">The Workbook stream from the compound file</param>
        /// <returns>True if encryption is detected</returns>
        public static bool DetectExcelEncryption(CfbStream stream)
        {
            if (stream.Length < 8)
                return false;

            try
            {
                stream.Position = 0;
                
                // Read and validate BOF record first
                if (!ValidateBiffHeader(stream))
                    return false;

                // Search for FilePass record within the first 1KB (typical location)
                long maxSearchBytes = Math.Min(stream.Length, 1024);
                stream.Position = 0;

                while (stream.Position < maxSearchBytes - 4)
                {
                    ushort recordType = ReadUInt16(stream);
                    ushort recordLength = ReadUInt16(stream);

                    if (recordType == FILEPASS_RECORD_TYPE)
                    {
                        // Found FilePass record - this indicates encryption
                        return ValidateFilePassRecord(stream, recordLength);
                    }

                    if (recordType == EOF_RECORD_TYPE)
                    {
                        // Reached the end of records without finding FilePass
                        break;
                    }

                    // Skip to the next record
                    if (recordLength > 0 && stream.Position + recordLength <= stream.Length)
                    {
                        stream.Position += recordLength;
                    }
                    else
                    {
                        break; // Invalid record length
                    }
                }
            }
            catch (Exception)
            {
                // If we can't parse the BIFF structure, assume it's not encrypted
                return false;
            }

            return false;
        }

        /// <summary>
        /// Extracts encryption information from a FilePass record
        /// </summary>
        /// <param name="stream">Stream positioned at the start of Workbook</param>
        /// <returns>FilePass encryption details or null if not found</returns>
        public static FilePassRecord? ExtractFilePassRecord(CfbStream stream)
        {
            if (!DetectExcelEncryption(stream))
                return null;

            try
            {
                stream.Position = 0;

                while (stream.Position < stream.Length - 4)
                {
                    ushort recordType = ReadUInt16(stream);
                    ushort recordLength = ReadUInt16(stream);

                    if (recordType == FILEPASS_RECORD_TYPE)
                    {
                        return ParseFilePassRecord(stream, recordLength);
                    }

                    if (recordType == EOF_RECORD_TYPE)
                        break;

                    // Skip to the next record
                    if (recordLength > 0 && stream.Position + recordLength <= stream.Length)
                    {
                        stream.Position += recordLength;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        private static bool ValidateBiffHeader(CfbStream stream)
        {
            if (stream.Length < 8)
                return false;

            stream.Position = 0;
            ushort recordType = ReadUInt16(stream);
            ushort recordLength = ReadUInt16(stream);

            // Check for BIFF8 BOF record
            return recordType == BOF_RECORD_TYPE && recordLength >= 8;
        }

        private static bool ValidateFilePassRecord(CfbStream stream, ushort recordLength)
        {
            if (recordLength < 2)
                return false;

            long currentPos = stream.Position;
            try
            {
                ushort encryptionType = ReadUInt16(stream);
                
                // Valid encryption types from MS-XLS specification
                return encryptionType == 0x0000 ||  // XOR obfuscation
                       encryptionType == 0x0001;    // RC4 encryption
            }
            catch
            {
                return false;
            }
            finally
            {
                stream.Position = currentPos;
            }
        }

        private static FilePassRecord ParseFilePassRecord(CfbStream stream, ushort recordLength)
        {
            long startPosition = stream.Position;
            ushort encryptionType = ReadUInt16(stream);
            
            var record = new FilePassRecord
            {
                EncryptionType = encryptionType,
                RecordLength = recordLength
            };

            if (recordLength <= 2) return record;
            int remainingBytes = recordLength - 2;
            record.EncryptionInfo = new byte[remainingBytes];
            _ = stream.Read(record.EncryptionInfo, 0, remainingBytes);

            return record;
        }

        private static ushort ReadUInt16(CfbStream stream)
        {
            var bytes = new byte[2];
            int bytesRead = stream.Read(bytes, 0, 2);
            return bytesRead != 2
                ? throw new EndOfStreamException("Unexpected end of stream")
                : BitConverter.ToUInt16(bytes, 0);
        }
    }
}