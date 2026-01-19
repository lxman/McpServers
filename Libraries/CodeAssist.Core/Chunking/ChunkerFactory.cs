namespace CodeAssist.Core.Chunking;

/// <summary>
/// Factory for selecting the appropriate code chunker based on file extension.
/// Uses TreeSitterChunker for languages with AST support, falls back to DefaultChunker.
/// </summary>
public sealed class ChunkerFactory
{
    private readonly TreeSitterChunker _treeSitterChunker;
    private readonly DefaultChunker _defaultChunker;

    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        // C#
        [".cs"] = "csharp",

        // Python
        [".py"] = "python",
        [".pyw"] = "python",
        [".pyi"] = "python",

        // JavaScript/TypeScript
        [".js"] = "javascript",
        [".jsx"] = "javascript",
        [".ts"] = "typescript",
        [".tsx"] = "typescript",
        [".mjs"] = "javascript",
        [".cjs"] = "javascript",

        // Go
        [".go"] = "go",

        // Rust
        [".rs"] = "rust",

        // Java/Kotlin
        [".java"] = "java",
        [".kt"] = "kotlin",
        [".kts"] = "kotlin",

        // Swift
        [".swift"] = "swift",

        // C/C++
        [".c"] = "c",
        [".h"] = "c",
        [".cpp"] = "cpp",
        [".cc"] = "cpp",
        [".cxx"] = "cpp",
        [".hpp"] = "cpp",
        [".hxx"] = "cpp",

        // Ruby
        [".rb"] = "ruby",
        [".rake"] = "ruby",

        // PHP
        [".php"] = "php",

        // Shell
        [".sh"] = "shell",
        [".bash"] = "shell",
        [".zsh"] = "shell",

        // Data/Config
        [".json"] = "json",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".xml"] = "xml",
        [".toml"] = "toml",

        // Markup
        [".md"] = "markdown",
        [".markdown"] = "markdown",
        [".html"] = "html",
        [".htm"] = "html",
        [".css"] = "css",
        [".scss"] = "scss",
        [".less"] = "less",

        // Database
        [".sql"] = "sql",

        // Other
        [".dockerfile"] = "dockerfile",
        [".proto"] = "protobuf",
        [".graphql"] = "graphql",
        [".gql"] = "graphql"
    };

    public ChunkerFactory(TreeSitterChunker treeSitterChunker, DefaultChunker defaultChunker)
    {
        _treeSitterChunker = treeSitterChunker;
        _defaultChunker = defaultChunker;
    }

    /// <summary>
    /// Get the language for a file extension.
    /// </summary>
    public string GetLanguage(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
        {
            // Check for special filenames
            var fileName = Path.GetFileName(filePath);
            return fileName.ToLowerInvariant() switch
            {
                "dockerfile" => "dockerfile",
                "makefile" => "makefile",
                "cmakelists.txt" => "cmake",
                _ => "text"
            };
        }

        return ExtensionToLanguage.TryGetValue(extension, out var language)
            ? language
            : "text";
    }

    /// <summary>
    /// Get the chunker for a file based on its language.
    /// Uses TreeSitterChunker for supported languages, falls back to DefaultChunker.
    /// </summary>
    public ICodeChunker GetChunker(string filePath)
    {
        var language = GetLanguage(filePath);
        return _treeSitterChunker.SupportsLanguage(language)
            ? _treeSitterChunker
            : _defaultChunker;
    }

    /// <summary>
    /// Check if a file extension is supported for indexing.
    /// </summary>
    public bool IsSupportedExtension(string extension)
    {
        return ExtensionToLanguage.ContainsKey(extension);
    }
}
