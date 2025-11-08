using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DocumentServer.Core.Services.Core;

/// <summary>
/// Manages passwords for encrypted documents using pattern matching and specific file paths
/// </summary>
public class PasswordManager
{
    private readonly ILogger<PasswordManager> _logger;
    private readonly Dictionary<Regex, string> _patternPasswords = new();
    private readonly Dictionary<string, string> _specificPasswords = new();

    /// <summary>
    /// Initializes a new instance of the PasswordManager
    /// </summary>
    public PasswordManager(ILogger<PasswordManager> logger)
    {
        _logger = logger;
        _logger.LogInformation("PasswordManager initialized");
    }

    /// <summary>
    /// Register a password for files matching a glob pattern
    /// </summary>
    /// <param name="pattern">Glob pattern (e.g., "*sensitive*.pdf", "**/*.xlsx")</param>
    /// <param name="password">Password to use for matching files</param>
    /// <remarks>
    /// Glob patterns support:
    /// - * matches any characters except directory separators
    /// - ** matches any number of directories
    /// - ? matches any single character
    /// </remarks>
    public void RegisterPasswordPattern(string pattern, string password)
    {
        try
        {
            _logger.LogDebug("Registering password pattern: {Pattern}", pattern);
            
            // Convert glob pattern to regex
            var regexPattern = GlobToRegex(pattern);
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _patternPasswords[regex] = password;
            
            _logger.LogInformation("Successfully registered password pattern: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register password pattern: {Pattern}", pattern);
            throw;
        }
    }

    /// <summary>
    /// Register a password for a specific file path
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Password for the document</param>
    public void RegisterSpecificPassword(string filePath, string password)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
            _specificPasswords[normalizedPath] = password;
            
            _logger.LogInformation("Registered password for specific file: {FilePath}", filePath);
            _logger.LogDebug("Normalized path: {NormalizedPath}", normalizedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register password for file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Register multiple passwords from a dictionary mapping file paths to passwords
    /// </summary>
    /// <param name="passwordMap">Dictionary mapping file paths to passwords</param>
    /// <returns>Number of passwords successfully registered</returns>
    public int BulkRegisterPasswords(Dictionary<string, string> passwordMap)
    {
        _logger.LogInformation("Bulk registering {Count} passwords", passwordMap.Count);
        
        var successCount = 0;
        var failureCount = 0;

        foreach ((var filePath, var password) in passwordMap)
        {
            try
            {
                RegisterSpecificPassword(filePath, password);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register password for: {FilePath}", filePath);
                failureCount++;
            }
        }

        _logger.LogInformation("Bulk registration complete: {Success} succeeded, {Failed} failed", 
            successCount, failureCount);
        
        return successCount;
    }

    /// <summary>
    /// Automatically detect and register passwords from password files in a directory tree
    /// </summary>
    /// <param name="rootPath">Root directory to search for password files</param>
    /// <returns>Number of passwords auto-detected</returns>
    /// <remarks>
    /// Looks for files named *password*.txt, *pword*.txt, or *.pwd
    /// The password is read from the file and registered as a pattern for all files in that directory tree
    /// </remarks>
    public async Task<int> AutoDetectPasswordFilesAsync(string rootPath)
    {
        _logger.LogInformation("Auto-detecting password files in: {RootPath}", rootPath);
        
        var detectedCount = 0;

        try
        {
            var passwordFiles = Directory.GetFiles(rootPath, "*password*.txt", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(rootPath, "*pword*.txt", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(rootPath, "*.pwd", SearchOption.AllDirectories));

            _logger.LogDebug("Found {Count} potential password files", passwordFiles.Count());

            foreach (var passwordFile in passwordFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(passwordFile);
                    var password = content.Trim();
                    
                    if (IsValidPassword(password))
                    {
                        var directory = Path.GetDirectoryName(passwordFile)!;
                        var pattern = Path.Combine(directory, "**", "*").Replace("\\", "/");
                        RegisterPasswordPattern(pattern, password);
                        detectedCount++;
                        
                        _logger.LogInformation("Auto-detected password from file: {PasswordFile}", passwordFile);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid password content in file: {PasswordFile}", passwordFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read password file: {PasswordFile}", passwordFile);
                }
            }

            _logger.LogInformation("Auto-detection complete: {Count} passwords registered", detectedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-detect password files in: {RootPath}", rootPath);
        }

        return detectedCount;
    }

    /// <summary>
    /// Retrieve the password for a specific file, if one is registered
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <returns>The password if found, otherwise null</returns>
    /// <remarks>
    /// Checks specific passwords first, then pattern-based passwords
    /// </remarks>
    public string? GetPasswordForFile(string filePath)
    {
        _logger.LogDebug("Looking up password for file: {FilePath}", filePath);
        
        var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
        
        // Check specific passwords first
        if (_specificPasswords.TryGetValue(normalizedPath, out var specificPassword))
        {
            _logger.LogDebug("Found specific password for: {FilePath}", filePath);
            return specificPassword;
        }

        // Check pattern-based passwords
        var unixPath = filePath.Replace('\\', '/');
        foreach ((var regex, var password) in _patternPasswords)
        {
            if (regex.IsMatch(unixPath) || regex.IsMatch(filePath))
            {
                _logger.LogDebug("Found pattern-based password for: {FilePath}", filePath);
                return password;
            }
        }

        _logger.LogDebug("No password found for: {FilePath}", filePath);
        return null;
    }

    /// <summary>
    /// Check if a password is registered for a specific file
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <returns>True if a password is registered, otherwise false</returns>
    public bool HasPasswordForFile(string filePath)
    {
        return GetPasswordForFile(filePath) is not null;
    }

    /// <summary>
    /// Clear all registered passwords (both specific and pattern-based)
    /// </summary>
    public void ClearPasswords()
    {
        var patternCount = _patternPasswords.Count;
        var specificCount = _specificPasswords.Count;
        
        _patternPasswords.Clear();
        _specificPasswords.Clear();
        
        _logger.LogInformation("Cleared all registered passwords: {PatternCount} patterns, {SpecificCount} specific files", 
            patternCount, specificCount);
    }

    /// <summary>
    /// Get information about registered password patterns (for debugging)
    /// </summary>
    /// <returns>Dictionary mapping pattern strings to masked passwords</returns>
    public Dictionary<string, string> GetRegisteredPatterns()
    {
        _logger.LogDebug("Retrieving registered patterns: {Count} patterns", _patternPasswords.Count);
        return _patternPasswords.ToDictionary(kv => kv.Key.ToString(), kv => "***");
    }

    /// <summary>
    /// Get count of registered specific file passwords (for debugging)
    /// </summary>
    /// <returns>Number of specific file passwords registered</returns>
    public int GetSpecificPasswordCount()
    {
        return _specificPasswords.Count;
    }

    /// <summary>
    /// Get count of registered password patterns (for debugging)
    /// </summary>
    /// <returns>Number of password patterns registered</returns>
    public int GetPatternCount()
    {
        return _patternPasswords.Count;
    }

    /// <summary>
    /// Validates if a password string meets minimum requirements
    /// </summary>
    private static bool IsValidPassword(string password)
    {
        var isValid = !string.IsNullOrWhiteSpace(password) && 
                      password.Length is >= 3 and <= 256 &&
                      !password.Contains('\n') &&
                      !password.Contains('\r');
        
        return isValid;
    }

    /// <summary>
    /// Converts a glob pattern to a regular expression pattern
    /// </summary>
    private static string GlobToRegex(string glob)
    {
        var regex = "^" + Regex.Escape(glob)
                            .Replace("\\*\\*", ".*")  // ** matches any number of directories
                            .Replace("\\*", "[^/\\\\]*")  // * matches anything except directory separators
                            .Replace("\\?", ".")  // ? matches any single character
                        + "$";
        
        return regex;
    }
}
