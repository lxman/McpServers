using System;
using System.Text;

namespace MsOfficeCrypto.Utils
{
    /// <summary>
    /// Hex conversion utilities compatible with .NET Standard 2.1
    /// </summary>
    public static class HexUtils
    {
        /// <summary>
        /// Converts byte array to hex string (replacement for Convert.ToHexString in .NET 5+)
        /// </summary>
        /// <param name="bytes">Byte array to convert</param>
        /// <returns>Uppercase hex string without separators</returns>
        public static string ToHexString(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// Converts byte array to hex string with custom formatting
        /// </summary>
        /// <param name="bytes">Byte array to convert</param>
        /// <param name="separator">Separator between bytes (default: none)</param>
        /// <param name="uppercase">Use uppercase letters (default: true)</param>
        /// <returns>Formatted hex string</returns>
        public static string ToHexString(byte[]? bytes, string separator = "", bool uppercase = true)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var result = BitConverter.ToString(bytes);
            
            if (separator != "-")
            {
                result = result.Replace("-", separator);
            }

            return uppercase ? result : result.ToLowerInvariant();
        }

        /// <summary>
        /// Converts hex string back to byte array
        /// </summary>
        /// <param name="hex">Hex string to convert</param>
        /// <returns>Byte array</returns>
        public static byte[] FromHexString(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Array.Empty<byte>();

            // Remove any separators
            hex = hex.Replace("-", "").Replace(" ", "").Replace(":", "");

            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have even length", nameof(hex));

            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            return bytes;
        }

        /// <summary>
        /// Creates a formatted hex dump with addresses and ASCII representation
        /// </summary>
        /// <param name="data">Data to dump</param>
        /// <param name="maxBytes">Maximum bytes to include</param>
        /// <param name="bytesPerLine">Bytes per line (default: 16)</param>
        /// <returns>Formatted hex dump string</returns>
        public static string CreateHexDump(byte[]? data, int maxBytes = 256, int bytesPerLine = 16)
        {
            if (data == null || data.Length == 0)
                return "No data";
            
            var sb = new StringBuilder();
            int bytesToDump = Math.Min(data.Length, maxBytes);
            
            for (var i = 0; i < bytesToDump; i += bytesPerLine)
            {
                // Address
                sb.AppendFormat("{0:X8}: ", i);
                
                // Hex bytes
                for (var j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < bytesToDump)
                        sb.AppendFormat("{0:X2} ", data[i + j]);
                    else
                        sb.Append("   ");
                }
                
                sb.Append(" ");
                
                // ASCII representation
                for (var j = 0; j < bytesPerLine && i + j < bytesToDump; j++)
                {
                    byte b = data[i + j];
                    sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }
                
                sb.AppendLine();
            }
            
            if (data.Length > maxBytes)
                sb.AppendLine($"... ({data.Length - maxBytes} more bytes)");
                
            return sb.ToString();
        }

        /// <summary>
        /// Safely displays hex string with length limit for console output
        /// </summary>
        /// <param name="bytes">Bytes to display</param>
        /// <param name="maxDisplayBytes">Maximum bytes to show (default: 32)</param>
        /// <returns>Hex string with "..." if truncated</returns>
        public static string ToDisplayHex(byte[]? bytes, int maxDisplayBytes = 32)
        {
            if (bytes == null || bytes.Length == 0)
                return "null";

            if (bytes.Length <= maxDisplayBytes)
                return ToHexString(bytes);

            var truncated = new byte[maxDisplayBytes];
            Array.Copy(bytes, truncated, maxDisplayBytes);
            return ToHexString(truncated) + $"... ({bytes.Length} total bytes)";
        }
    }
}