using McpCodeEditor.Models.Refactoring;

namespace McpCodeEditor.Services.Refactoring;

/// <summary>
/// Service responsible for detecting the programming language of source code files
/// based on file extensions and content analysis.
/// </summary>
public class LanguageDetectionService
{
    private static readonly Dictionary<string, LanguageType> ExtensionToLanguage = new()
    {
        // C# files
        { ".cs", LanguageType.CSharp },
        
        // TypeScript files
        { ".ts", LanguageType.TypeScript },
        { ".tsx", LanguageType.TypeScript },
        
        // JavaScript files
        { ".js", LanguageType.JavaScript },
        { ".jsx", LanguageType.JavaScript },
        { ".mjs", LanguageType.JavaScript },
        { ".cjs", LanguageType.JavaScript }
    };

    private static readonly Dictionary<LanguageType, string[]> LanguageToExtensions = new()
    {
        { LanguageType.CSharp, [".cs"] },
        { LanguageType.TypeScript, [".ts", ".tsx"] },
        { LanguageType.JavaScript, [".js", ".jsx", ".mjs", ".cjs"] }
    };

    /// <summary>
    /// Detects the programming language of a file based on its extension.
    /// </summary>
    /// <param name="filePath">Path to the file to analyze</param>
    /// <returns>The detected language type</returns>
    public static LanguageType DetectLanguage(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return LanguageType.Unknown;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return ExtensionToLanguage.TryGetValue(extension, out var languageType) 
            ? languageType 
            : LanguageType.Unknown;
    }

    /// <summary>
    /// Detects the programming language of multiple files.
    /// </summary>
    /// <param name="filePaths">Paths to the files to analyze</param>
    /// <returns>Dictionary mapping file paths to their detected language types</returns>
    public static Dictionary<string, LanguageType> DetectLanguages(IEnumerable<string> filePaths)
    {
        var result = new Dictionary<string, LanguageType>();
        
        foreach (var filePath in filePaths)
        {
            result[filePath] = DetectLanguage(filePath);
        }
        
        return result;
    }

    /// <summary>
    /// Gets all supported file extensions for a specific language.
    /// </summary>
    /// <param name="languageType">The language type to get extensions for</param>
    /// <returns>Array of file extensions for the language</returns>
    public static string[] GetSupportedExtensions(LanguageType languageType)
    {
        return LanguageToExtensions.TryGetValue(languageType, out var extensions) 
            ? extensions 
            : [];
    }

    /// <summary>
    /// Gets all supported languages.
    /// </summary>
    /// <returns>Array of all supported language types (excluding Unknown)</returns>
    public static LanguageType[] GetSupportedLanguages()
    {
        return LanguageToExtensions.Keys.ToArray();
    }

    /// <summary>
    /// Checks if a file extension is supported for refactoring operations.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if the file type is supported for refactoring</returns>
    public static bool IsFileSupported(string filePath)
    {
        return DetectLanguage(filePath) != LanguageType.Unknown;
    }

    /// <summary>
    /// Gets a user-friendly name for a language type.
    /// </summary>
    /// <param name="languageType">The language type</param>
    /// <returns>Human-readable name for the language</returns>
    public static string GetLanguageName(LanguageType languageType)
    {
        return languageType switch
        {
            LanguageType.CSharp => "C#",
            LanguageType.TypeScript => "TypeScript",
            LanguageType.JavaScript => "JavaScript",
            LanguageType.Unknown => "Unknown",
            _ => languageType.ToString()
        };
    }

    /// <summary>
    /// Analyzes a directory to identify the primary language used.
    /// </summary>
    /// <param name="directoryPath">Path to the directory to analyze</param>
    /// <param name="recursive">Whether to search subdirectories</param>
    /// <returns>The most common language type found in the directory</returns>
    public static LanguageType DetectPrimaryLanguage(string directoryPath, bool recursive = false)
    {
        if (!Directory.Exists(directoryPath))
        {
            return LanguageType.Unknown;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, "*", searchOption);
        
        var languageCounts = new Dictionary<LanguageType, int>();
        
        foreach (var file in files)
        {
            var language = DetectLanguage(file);
            if (language != LanguageType.Unknown)
            {
                languageCounts[language] = languageCounts.GetValueOrDefault(language, 0) + 1;
            }
        }

        return languageCounts.Count > 0 
            ? languageCounts.OrderByDescending(kvp => kvp.Value).First().Key
            : LanguageType.Unknown;
    }

    /// <summary>
    /// Gets statistics about language distribution in a directory.
    /// </summary>
    /// <param name="directoryPath">Path to the directory to analyze</param>
    /// <param name="recursive">Whether to search subdirectories</param>
    /// <returns>Dictionary with language types and their file counts</returns>
    public static Dictionary<LanguageType, int> GetLanguageStatistics(string directoryPath, bool recursive = false)
    {
        var result = new Dictionary<LanguageType, int>();
        
        if (!Directory.Exists(directoryPath))
        {
            return result;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, "*", searchOption);
        
        foreach (var file in files)
        {
            var language = DetectLanguage(file);
            result[language] = result.GetValueOrDefault(language, 0) + 1;
        }

        return result;
    }
}
