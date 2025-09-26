using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DesktopDriver.Services.DocumentSearching;

public class PasswordManager(ILogger<PasswordManager> logger)
{
    private readonly Dictionary<Regex, string> _patternPasswords = new();
    private readonly Dictionary<string, string> _specificPasswords = new();

    public void RegisterPasswordPattern(string pattern, string password)
    {
        try
        {
            // Convert glob pattern to regex
            string regexPattern = GlobToRegex(pattern);
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _patternPasswords[regex] = password;
            
            logger.LogInformation("Registered password pattern: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register password pattern: {Pattern}", pattern);
        }
    }

    public void RegisterSpecificPassword(string filePath, string password)
    {
        string normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
        _specificPasswords[normalizedPath] = password;
        
        logger.LogInformation("Registered specific password for file: {FilePath}", filePath);
    }

    public async Task AutoDetectPasswordFiles(string rootPath)
    {
        try
        {
            IEnumerable<string> passwordFiles = Directory.GetFiles(rootPath, "*password*.txt", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(rootPath, "*pword*.txt", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(rootPath, "*.pwd", SearchOption.AllDirectories));

            foreach (string passwordFile in passwordFiles)
            {
                try
                {
                    string content = await File.ReadAllTextAsync(passwordFile);
                    string password = content.Trim();
                    
                    if (IsValidPassword(password))
                    {
                        string directory = Path.GetDirectoryName(passwordFile)!;
                        string pattern = Path.Combine(directory, "**", "*").Replace("\\", "/");
                        RegisterPasswordPattern(pattern, password);
                        
                        logger.LogInformation("Auto-detected password from file: {PasswordFile}", passwordFile);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read password file: {PasswordFile}", passwordFile);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-detect password files in: {RootPath}", rootPath);
        }
    }

    public string? GetPasswordForFile(string filePath)
    {
        string normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();
        
        // Check specific passwords first
        if (_specificPasswords.TryGetValue(normalizedPath, out string? specificPassword))
        {
            return specificPassword;
        }

        // Check pattern-based passwords
        string unixPath = filePath.Replace('\\', '/');
        foreach ((Regex regex, string password) in _patternPasswords)
        {
            if (regex.IsMatch(unixPath) || regex.IsMatch(filePath))
            {
                return password;
            }
        }

        return null;
    }

    public void ClearPasswords()
    {
        _patternPasswords.Clear();
        _specificPasswords.Clear();
        logger.LogInformation("Cleared all registered passwords");
    }

    public Dictionary<string, string> GetRegisteredPatterns()
    {
        return _patternPasswords.ToDictionary(kv => kv.Key.ToString(), kv => "***");
    }

    private static bool IsValidPassword(string password)
    {
        return !string.IsNullOrWhiteSpace(password) && 
               password.Length >= 3 && 
               password.Length <= 256 &&
               !password.Contains('\n') &&
               !password.Contains('\r');
    }

    private static string GlobToRegex(string glob)
    {
        string regex = "^" + Regex.Escape(glob)
                               .Replace("\\*\\*", ".*")  // ** matches any number of directories
                               .Replace("\\*", "[^/\\\\]*")  // * matches anything except directory separators
                               .Replace("\\?", ".")  // ? matches any single character
                           + "$";
        
        return regex;
    }
}