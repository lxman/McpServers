using CodeAssist.Core.Models;

namespace CodeAssist.Core.Chunking;

/// <summary>
/// Interface for code chunking strategies.
/// </summary>
public interface ICodeChunker
{
    /// <summary>
    /// Languages this chunker supports.
    /// </summary>
    IReadOnlySet<string> SupportedLanguages { get; }

    /// <summary>
    /// Check if this chunker supports the given language.
    /// </summary>
    bool SupportsLanguage(string language);

    /// <summary>
    /// Chunk the given code content into meaningful pieces.
    /// </summary>
    /// <param name="content">The source code content.</param>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <param name="relativePath">Path relative to repository root.</param>
    /// <param name="language">Programming language.</param>
    /// <returns>List of code chunks.</returns>
    IReadOnlyList<CodeChunk> ChunkCode(string content, string filePath, string relativePath, string language);
}
