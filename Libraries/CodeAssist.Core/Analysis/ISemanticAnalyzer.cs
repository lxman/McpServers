using CodeAssist.Core.Models;

namespace CodeAssist.Core.Analysis;

/// <summary>
/// Contract for language-specific semantic analyzers.
/// Each implementation wraps a language's semantic tooling (Roslyn for C#, TypeScript Compiler API, etc.)
/// to enrich tree-sitter chunks with resolved type information, qualified names, and data flow data.
/// </summary>
public interface ISemanticAnalyzer
{
    /// <summary>
    /// Languages this analyzer handles (e.g., "csharp", "typescript").
    /// </summary>
    IReadOnlySet<string> SupportedLanguages { get; }

    /// <summary>
    /// Initialize the analyzer for a project (load workspace, compilation, etc.).
    /// This may be slow — runs once at startup, in parallel with tree-sitter indexing.
    /// </summary>
    Task InitializeAsync(string projectPath, CancellationToken cancellationToken);

    /// <summary>
    /// Enrich tree-sitter chunks with semantic data for a single file.
    /// Called after tree-sitter produces initial chunks. Returns enriched copies.
    /// </summary>
    Task<IReadOnlyList<CodeChunk>> EnrichChunksAsync(
        string filePath,
        IReadOnlyList<CodeChunk> treeSitterChunks,
        CancellationToken cancellationToken);

    /// <summary>
    /// Extract DI / IoC container registrations (interface → implementation mappings).
    /// Returns empty if the language/framework doesn't use DI or the analyzer doesn't support it.
    /// </summary>
    Task<IReadOnlyList<DependencyMapping>> ExtractDependencyMappingsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Extract HTTP endpoints this project exposes or consumes.
    /// Servers return endpoints they serve; clients return endpoints they call.
    /// The graph links matching endpoints across tiers by route template + HTTP method.
    /// </summary>
    Task<IReadOnlyList<HttpEndpointInfo>> ExtractHttpEndpointsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Handle a file change incrementally (update internal compilation state).
    /// </summary>
    Task OnFileChangedAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the analyzer has finished initialization and is ready to enrich.
    /// </summary>
    bool IsReady { get; }
}
