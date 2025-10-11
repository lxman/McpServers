using System.Security.Cryptography;
using System.Text;
using DesktopCommander.Exceptions;

namespace DesktopCommander.Services
{
    /// <summary>
    /// Service for computing and validating file version tokens using SHA256 hashing.
    /// Used for optimistic locking to prevent concurrent edit conflicts.
    /// </summary>
    public class FileVersionService
    {
        /// <summary>
        /// Computes a SHA256 hash of the file content and returns it as a version token.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Version token in format "sha256:hexstring"</returns>
        public string ComputeVersionToken(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            using var sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(stream);
            string hashString = Convert.ToHexStringLower(hashBytes);
            return $"sha256:{hashString}";
        }

        /// <summary>
        /// Computes a SHA256 hash of the provided content.
        /// </summary>
        /// <param name="content">Content to hash</param>
        /// <returns>Version token in format "sha256:hexstring"</returns>
        public static string ComputeVersionTokenFromContent(string content)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            byte[] hashBytes = SHA256.HashData(contentBytes);
            string hashString = Convert.ToHexStringLower(hashBytes);
            return $"sha256:{hashString}";
        }

        /// <summary>
        /// Validates that the provided version token matches the current file state.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="expectedToken">Expected version token from previous read</param>
        /// <returns>True if tokens match, false otherwise</returns>
        public bool ValidateVersionToken(string filePath, string expectedToken)
        {
            if (string.IsNullOrWhiteSpace(expectedToken))
            {
                throw new ArgumentException("Version token cannot be null or empty", nameof(expectedToken));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            string currentToken = ComputeVersionToken(filePath);
            return string.Equals(currentToken, expectedToken, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates version token and throws detailed exception if mismatch occurs.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="expectedToken">Expected version token from previous read</param>
        /// <exception cref="FileConflictException">Thrown when version tokens don't match</exception>
        public void ValidateVersionTokenOrThrow(string filePath, string expectedToken)
        {
            if (string.IsNullOrWhiteSpace(expectedToken))
            {
                throw new ArgumentException(
                    "Version token is required. You must read the file first to obtain a version token before editing.",
                    nameof(expectedToken));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            string currentToken = ComputeVersionToken(filePath);
            
            if (!string.Equals(currentToken, expectedToken, StringComparison.OrdinalIgnoreCase))
            {
                throw new FileConflictException(
                    $"FILE_CONFLICT: File has been modified since last read. " +
                    $"You must re-read the file to get the current state and version token before attempting another edit. " +
                    $"Expected version: {expectedToken}, Current version: {currentToken}",
                    expectedToken,
                    currentToken,
                    filePath);
            }
        }
    }
}