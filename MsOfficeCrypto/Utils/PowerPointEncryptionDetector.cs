using System;
using System.IO;
using MsOfficeCrypto.Structures;
using OpenMcdf;

namespace MsOfficeCrypto.Utils
{
    /// <summary>
    /// PowerPoint binary format encryption detector
    /// Detects a CryptSession10Container which indicates RC4 CryptoAPI encryption
    /// </summary>
    public static class PowerPointEncryptionDetector
    {
        // PowerPoint record type constants
        private const ushort CURRENT_USER_ATOM = 0x0FF6;
        private const ushort CRYPT_SESSION10_CONTAINER = 0x2F14;
        private const uint ENCRYPTED_HEADER_TOKEN = 0xF3D1C4DF;

        /// <summary>
        /// Detects encryption in a PowerPoint binary document
        /// </summary>
        /// <param name="root">Root storage of the compound file</param>
        /// <returns>True if encryption is detected</returns>
        public static bool DetectPowerPointEncryption(RootStorage root)
        {
            try
            {
                // Check for encrypted header token in Current User stream
                if (HasEncryptedCurrentUserStream(root))
                    return true;

                // Check for CryptSession10Container in the PowerPoint Document stream
                if (HasCryptSessionContainer(root))
                    return true;
            }
            catch (Exception)
            {
                // If we can't read the streams, assume it's not encrypted
                return false;
            }

            return false;
        }

        /// <summary>
        /// Extracts encryption information from the PowerPoint document
        /// </summary>
        /// <param name="root">Root storage of the compound file</param>
        /// <returns>PowerPoint encryption info or null if not encrypted</returns>
        public static PowerPointEncryptionInfo? ExtractEncryptionInfo(RootStorage root)
        {
            if (!DetectPowerPointEncryption(root))
                return null;

            var info = new PowerPointEncryptionInfo();

            try
            {
                // Check Current User stream for encryption indicator
                if (TryOpenStream(root, "Current User"))
                {
                    using CfbStream currentUserStream = root.OpenStream("Current User");
                    info.HasEncryptedCurrentUser = CheckCurrentUserEncryption(currentUserStream);
                }

                // Look for CryptSession10Container in the PowerPoint Document stream
                if (TryOpenStream(root, "PowerPoint Document"))
                {
                    using CfbStream pptDocStream = root.OpenStream("PowerPoint Document");
                    info.HasCryptSessionContainer = SearchForCryptSessionContainer(pptDocStream);
                }

                // Check for encrypted summary streams
                info.HasEncryptedSummaryInfo = TryOpenStream(root, "EncryptedSummaryInformation");
            }
            catch (Exception)
            {
                return null;
            }

            return info.IsEncrypted ? info : null;
        }

        private static bool HasEncryptedCurrentUserStream(RootStorage root)
        {
            if (!TryOpenStream(root, "Current User"))
                return false;

            try
            {
                using CfbStream currentUserStream = root.OpenStream("Current User");
                return CheckCurrentUserEncryption(currentUserStream);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckCurrentUserEncryption(CfbStream stream)
        {
            if (stream.Length < 20) // Minimum size for CurrentUserAtom
                return false;

            try
            {
                stream.Position = 0;
                
                // Read the record header
                ushort recordType = ReadUInt16(stream);
                ushort recordVersion = ReadUInt16(stream);
                uint recordLength = ReadUInt32(stream);

                if (recordType != CURRENT_USER_ATOM)
                    return false;

                // Read the header token (next 4 bytes)
                uint headerToken = ReadUInt32(stream);
                
                // Check for encrypted header token
                return headerToken == ENCRYPTED_HEADER_TOKEN;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasCryptSessionContainer(RootStorage root)
        {
            if (!TryOpenStream(root, "PowerPoint Document"))
                return false;

            try
            {
                using CfbStream pptDocStream = root.OpenStream("PowerPoint Document");
                return SearchForCryptSessionContainer(pptDocStream);
            }
            catch
            {
                return false;
            }
        }

        private static bool SearchForCryptSessionContainer(CfbStream stream)
        {
            if (stream.Length < 8)
                return false;

            try
            {
                stream.Position = 0;
                long maxSearchBytes = Math.Min(stream.Length, 8192); // Search first 8KB

                while (stream.Position < maxSearchBytes - 8)
                {
                    ushort recordType = ReadUInt16(stream);
                    ushort recordVersion = ReadUInt16(stream);
                    uint recordLength = ReadUInt32(stream);

                    if (recordType == CRYPT_SESSION10_CONTAINER)
                    {
                        return true; // Found CryptSession10Container
                    }

                    // Skip to next potential record
                    if (recordLength > 0 && recordLength < 1048576 && // Reasonable size limit
                        stream.Position + recordLength <= stream.Length)
                    {
                        stream.Position += recordLength;
                    }
                    else
                    {
                        // Move forward by small amount to search for next record
                        stream.Position -= 6; // Back up and try next byte
                        stream.Position += 1;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

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

        private static ushort ReadUInt16(CfbStream stream)
        {
            var bytes = new byte[2];
            int bytesRead = stream.Read(bytes, 0, 2);
            return bytesRead != 2
                ? throw new EndOfStreamException("Unexpected end of stream")
                : BitConverter.ToUInt16(bytes, 0);
        }

        private static uint ReadUInt32(CfbStream stream)
        {
            var bytes = new byte[4];
            int bytesRead = stream.Read(bytes, 0, 4);
            return bytesRead != 4
                ? throw new EndOfStreamException("Unexpected end of stream")
                : BitConverter.ToUInt32(bytes, 0);
        }
    }
}