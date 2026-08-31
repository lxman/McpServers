using CodeAssist.Core.Configuration;
using CodeAssist.Core.Models;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public sealed class IndexStateStoreDeleteTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "codeassist-delete-" + Guid.NewGuid().ToString("N"));

    private IndexStateStore MakeStore() => new(
        Options.Create(new CodeAssistOptions { IndexStateDirectory = _directory }),
        NullLogger<IndexStateStore>.Instance);

    // IndexStateFile has nine required members; all of them must be set for this to compile.
    private static IndexStateFile StateFor(string repository) => new()
    {
        RepositoryName = repository,
        RootPath = @"C:\repo",
        CreatedAt = DateTimeOffset.UtcNow,
        LastUpdatedAt = DateTimeOffset.UtcNow,
        EmbeddingModel = "test-model",
        CollectionName = CollectionNaming.ForRepository(repository),
        IncludePatterns = ["**/*.cs"],
        ExcludePatterns = [],
        Files = []
    };

    [Fact]
    public async Task DeleteAsync_RemovesTheStateFile()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("Repo", StateFor("Repo"), TestContext.Current.CancellationToken);

        await store.DeleteAsync("Repo", TestContext.Current.CancellationToken);

        Assert.Null(await store.LoadAsync("Repo", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_StillRejectsAMismatchedRepositoryName()
    {
        IndexStateStore store = MakeStore();
        await store.SaveAsync("Repo", StateFor("Repo"), TestContext.Current.CancellationToken);

        // Rewrite the file's own idea of which repository it belongs to. Deleting by the path's
        // name must refuse rather than destroy another repository's state.
        string path = store.GetStatePath("Repo");
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("\"RepositoryName\": \"Repo\"", "\"RepositoryName\": \"Different\""));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.DeleteAsync("Repo", TestContext.Current.CancellationToken));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotBreakAConcurrentSave()
    {
        IndexStateStore store = MakeStore();
        var saveFailures = new List<string>();

        // Twenty rounds, matching the measurement that established this class's locking rule.
        for (var round = 0; round < 20; round++)
        {
            string repository = $"Repo{round}";
            await store.SaveAsync(
                repository, StateFor(repository), TestContext.Current.CancellationToken);

            Task saving = Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < 10; i++)
                    {
                        await store.SaveAsync(
                            repository, StateFor(repository),
                            TestContext.Current.CancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    lock (saveFailures) saveFailures.Add($"round {round}: {ex.Message}");
                }
            }, TestContext.Current.CancellationToken);

            Task deleting = Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    try
                    {
                        await store.DeleteAsync(
                            repository, TestContext.Current.CancellationToken);
                    }
                    catch (InvalidOperationException)
                    {
                        // Name validation, not a race.
                    }
                }
            }, TestContext.Current.CancellationToken);

            await Task.WhenAll(saving, deleting);
        }

        Assert.True(saveFailures.Count == 0,
            $"{saveFailures.Count} save(s) lost to a concurrent delete: "
            + string.Join("; ", saveFailures.Take(3)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
