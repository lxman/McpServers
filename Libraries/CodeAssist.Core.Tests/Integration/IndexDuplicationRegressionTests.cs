using System.Text;
using CodeAssist.Core.Caching;
using CodeAssist.Core.Chunking;
using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Integration;

public class IndexDuplicationRegressionTests : IAsyncLifetime
{
    private readonly string _repoRoot = Path.Combine(Path.GetTempPath(), "codeassist-dup-" + Guid.NewGuid().ToString("N"));
    private readonly string _repoName = "duptest" + Guid.NewGuid().ToString("N")[..8];
    private QdrantService _qdrant = null!;
    private RepositoryIndexer _indexer = null!;
    private HotCache _hotCache = null!;
    private L2PromotionService _promotion = null!;
    private string _collection = null!;

    private const string Version1 = """
        namespace Sample;
        public class Widget
        {
            public int Measure() { return 1; }
        }
        """;

    private const string Version2 = """
        namespace Sample;
        public class Widget
        {
            // an added line shifts every line number below it
            public int Measure() { return 42; }
        }
        """;

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_repoRoot, "Src"));
        File.WriteAllText(Path.Combine(_repoRoot, "Src", "Widget.cs"), Version1);

        var options = Options.Create(new CodeAssistOptions
        {
            QdrantUrl = Environment.GetEnvironmentVariable("CODEASSIST_TEST_QDRANT_URL")!,
            OllamaUrl = Environment.GetEnvironmentVariable("CODEASSIST_TEST_OLLAMA_URL")!,
            // The class default ("nomic-embed-text") does not match what the live MLX server at
            // CODEASSIST_TEST_OLLAMA_URL actually serves. Since Task 5, the server enforces its model
            // contract and rejects a mismatched name outright rather than silently switching, so this
            // must agree with the model the server has loaded (see CodeAssistMcp/appsettings.json).
            EmbeddingModel = "bge-base-en-v1.5",
            IndexStateDirectory = Path.Combine(_repoRoot, ".state"),
            EnableL2Promotion = true
        });

        _collection = CollectionNaming.ForRepository(_repoName);
        _qdrant = new QdrantService(options, NullLogger<QdrantService>.Instance);
        var ollama = new OllamaService(options, NullLogger<OllamaService>.Instance);
        var chunkers = new ChunkerFactory(
            new TreeSitterChunker(options, NullLogger<TreeSitterChunker>.Instance),
            new DefaultChunker(options, NullLogger<DefaultChunker>.Instance));
        var stateStore = new IndexStateStore(options, NullLogger<IndexStateStore>.Instance);

        // Parameter order matters: RepositoryIndexer takes ollama BEFORE qdrant.
        var writeCoordinator = new CollectionWriteCoordinator();
        _indexer = new RepositoryIndexer(ollama, _qdrant, chunkers, stateStore, writeCoordinator, options, NullLogger<RepositoryIndexer>.Instance);
        _hotCache = new HotCache(ollama, chunkers, options, NullLogger<HotCache>.Instance);
        _promotion = new L2PromotionService(_hotCache, _qdrant, stateStore, writeCoordinator, options, NullLogger<L2PromotionService>.Instance);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _qdrant.DeleteCollectionAsync(_collection); } catch { /* best effort */ }
        _promotion.Dispose();
        _hotCache.Dispose();
        if (Directory.Exists(_repoRoot)) Directory.Delete(_repoRoot, recursive: true);
    }

    private async Task<int> ChunkCountForAsync(string relativePath) =>
        (await _qdrant.SearchByFilePathAsync(_collection, relativePath)).Count;

    private async Task<List<string>> ContentsForAsync(string relativePath) =>
        (await _qdrant.SearchByFilePathAsync(_collection, relativePath))
        .Select(r => r.Chunk.Content).ToList();

    [RequiresLiveServicesFact]
    public async Task ReIndexingAModifiedFile_DoesNotLeaveTheOldVersionBehind()
    {
        string file = Path.Combine(_repoRoot, "Src", "Widget.cs");

        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);
        int before = await ChunkCountForAsync("Src/Widget.cs");
        Assert.True(before > 0, "first index produced no chunks — the test cannot detect duplication");

        await File.WriteAllTextAsync(file, Version2);
        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);

        Assert.Equal(before, await ChunkCountForAsync("Src/Widget.cs"));

        List<string> contents = await ContentsForAsync("Src/Widget.cs");
        Assert.DoesNotContain(contents, c => c.Contains("return 1;", StringComparison.Ordinal));
        Assert.Contains(contents, c => c.Contains("return 42;", StringComparison.Ordinal));
    }

    [RequiresLiveServicesFact]
    public async Task PromotingAModifiedFile_DoesNotLeaveTheOldVersionBehind()
    {
        string file = Path.Combine(_repoRoot, "Src", "Widget.cs");

        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);
        int before = await ChunkCountForAsync("Src/Widget.cs");
        Assert.True(before > 0, "first index produced no chunks — the test cannot detect duplication");

        _promotion.RegisterRepositoryCollection(_repoRoot, _collection);
        await File.WriteAllTextAsync(file, Version2);

        CachedFile? cached = await _hotCache.UpdateFileAsync(file, _repoRoot);
        Assert.NotNull(cached);
        await _promotion.PromoteNowAsync(cached, _collection);

        Assert.Equal(cached.Chunks.Count, await ChunkCountForAsync("Src/Widget.cs"));

        List<string> contents = await ContentsForAsync("Src/Widget.cs");
        Assert.DoesNotContain(contents, c => c.Contains("return 1;", StringComparison.Ordinal));
        Assert.Contains(contents, c => c.Contains("return 42;", StringComparison.Ordinal));
    }

    [RequiresLiveServicesFact]
    public async Task BothWritersUseTheSameRelativePathKey()
    {
        // If the two forms diverge again, one writer's rows become invisible to the other's delete
        // and duplication returns silently.
        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);
        _promotion.RegisterRepositoryCollection(_repoRoot, _collection);

        string file = Path.Combine(_repoRoot, "Src", "Widget.cs");
        CachedFile? cached = await _hotCache.UpdateFileAsync(file, _repoRoot);

        Assert.NotNull(cached);
        Assert.Equal("Src/Widget.cs", cached.RelativePath);
        Assert.All(cached.Chunks, c => Assert.Equal("Src/Widget.cs", c.RelativePath));
    }

    [RequiresLiveServicesFact]
    public async Task AFileWithMoreThanOnePageOfChunks_IsReadBackInFull()
    {
        // Covers Task 4. A scroll returns at most 100 points per page, and files well past that exist
        // in real collections — NetworkingDtos.cs holds 368 — so an unpaged read silently truncated
        // them and the graph was rebuilt from a partial file with no error anywhere.
        var wide = new StringBuilder("namespace Sample;\npublic class Wide\n{\n");
        for (var i = 0; i < 150; i++)
        {
            wide.AppendLine($"    public int Method{i}() {{ return {i}; }}");
        }

        wide.AppendLine("}");

        Directory.CreateDirectory(Path.Combine(_repoRoot, "Wide"));
        await File.WriteAllTextAsync(Path.Combine(_repoRoot, "Wide", "Wide.cs"), wide.ToString());

        await _indexer.IndexRepositoryAsync(_repoRoot, _repoName, ["*.cs"], []);

        int count = await ChunkCountForAsync("Wide/Wide.cs");

        Assert.True(
            count > 100,
            $"expected more than one page of chunks, got {count} — either the scroll is still "
            + "truncating at 100, or the chunker produced fewer than 100 chunks and this test no "
            + "longer proves anything");
    }
}
