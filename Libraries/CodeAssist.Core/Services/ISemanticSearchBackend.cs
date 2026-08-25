using CodeAssist.Core.Models;

namespace CodeAssist.Core.Services;

public interface ISemanticSearchBackend
{
    Task<List<SearchResult>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int limit = 10,
        float minScore = 0.5f,
        string? filePathFilter = null,
        CancellationToken cancellationToken = default);

    Task<List<SearchResult>> SearchBySymbolNamesAsync(
        string collectionName,
        IReadOnlyList<string> symbolNames,
        CancellationToken cancellationToken = default);

    Task<List<SearchResult>> SearchCallersOfAsync(
        string collectionName,
        string symbolName,
        CancellationToken cancellationToken = default);
}
