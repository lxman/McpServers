using CodeAssist.Core.Caching;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Xunit;

namespace CodeAssist.Core.Tests.Caching;

public class UnifiedSearchFailureTests
{
    [Fact]
    public async Task SearchL2Async_PropagatesBackendFailure()
    {
        var backend = new ThrowingSearchBackend();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UnifiedSearchService.SearchL2Async(
                backend,
                "repo",
                [0.1f, 0.2f],
                10,
                0.5f,
                TestContext.Current.CancellationToken));

        Assert.Equal("Qdrant unavailable", exception.Message);
    }

    private sealed class ThrowingSearchBackend : ISemanticSearchBackend
    {
        public Task<List<SearchResult>> SearchAsync(
            string collectionName,
            float[] queryEmbedding,
            int limit = 10,
            float minScore = 0.5f,
            string? filePathFilter = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Qdrant unavailable");

        public Task<List<SearchResult>> SearchBySymbolNamesAsync(
            string collectionName,
            IReadOnlyList<string> symbolNames,
            CancellationToken cancellationToken = default) => Task.FromResult<List<SearchResult>>([]);

        public Task<List<SearchResult>> SearchCallersOfAsync(
            string collectionName,
            string symbolName,
            CancellationToken cancellationToken = default) => Task.FromResult<List<SearchResult>>([]);
    }
}
