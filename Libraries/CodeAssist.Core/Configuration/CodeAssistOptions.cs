namespace CodeAssist.Core.Configuration;

/// <summary>
/// Configuration options for CodeAssist services.
/// </summary>
public sealed class CodeAssistOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "CodeAssist";

    /// <summary>
    /// Embedding server URL (Ollama-compatible API).
    /// Default: http://localhost:11435 (MLX server)
    /// For Ollama: http://localhost:11434
    /// </summary>
    public string OllamaUrl { get; set; } = "http://localhost:11435";

    /// <summary>
    /// Embedding model to use. Default: nomic-embed-text
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Qdrant server URL. Default: http://localhost:6333
    /// </summary>
    public string QdrantUrl { get; set; } = "http://localhost:6333";

    /// <summary>
    /// Vector dimension for embeddings. Must match the embedding model.
    /// nomic-embed-text: 768, text-embedding-ada-002: 1536
    /// </summary>
    public int VectorDimension { get; set; } = 768;

    /// <summary>
    /// Maximum chunk size in characters.
    /// </summary>
    public int MaxChunkSize { get; set; } = 2000;

    /// <summary>
    /// Overlap between chunks in characters.
    /// </summary>
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// Default file patterns to include when indexing.
    /// </summary>
    public List<string> DefaultIncludePatterns { get; set; } =
    [
        "**/*.cs",
        "**/*.py",
        "**/*.js",
        "**/*.ts",
        "**/*.tsx",
        "**/*.jsx",
        "**/*.go",
        "**/*.rs",
        "**/*.java",
        "**/*.kt",
        "**/*.swift",
        "**/*.cpp",
        "**/*.c",
        "**/*.h",
        "**/*.hpp",
        "**/*.rb",
        "**/*.php",
        "**/*.md",
        "**/*.json",
        "**/*.yaml",
        "**/*.yml",
        "**/*.xml",
        "**/*.sql"
    ];

    /// <summary>
    /// Default file/directory patterns to exclude when indexing.
    /// </summary>
    public List<string> DefaultExcludePatterns { get; set; } =
    [
        "**/bin/**",
        "**/obj/**",
        "**/node_modules/**",
        "**/.git/**",
        "**/packages/**",
        "**/dist/**",
        "**/build/**",
        "**/target/**",
        "**/__pycache__/**",
        "**/.vs/**",
        "**/.idea/**",
        "**/.venv/**",
        "**/skills/**",
        "**/testdata/**",
        "**/test-data/**",
        "**/*.min.js",
        "**/*.min.css",
        "**/package-lock.json",
        "**/yarn.lock",
        "**/*.Designer.cs",
        "**/*.generated.cs"
    ];

    /// <summary>
    /// Number of results to return from searches by default.
    /// </summary>
    public int DefaultSearchLimit { get; set; } = 10;

    /// <summary>
    /// Minimum similarity score (0.0 to 1.0) for search results.
    /// </summary>
    public float MinSimilarityScore { get; set; } = 0.5f;

    /// <summary>
    /// Directory to store index state files.
    /// </summary>
    public string IndexStateDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodeAssist",
        "indexes");
}
