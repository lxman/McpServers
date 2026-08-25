using CodeAssist.Core.Configuration;
using CodeAssist.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

public sealed class ActiveRepositoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "codeassist-active-" + Guid.NewGuid().ToString("N"));

    private ActiveRepositoryStore MakeStore() => new(
        Options.Create(new CodeAssistOptions { IndexStateDirectory = _directory }),
        NullLogger<ActiveRepositoryStore>.Instance);

    [Fact]
    public void SaveLoadAndClear_RoundTrip()
    {
        ActiveRepositoryStore store = MakeStore();

        Assert.True(store.TrySave("MyRepo"));
        Assert.Equal("MyRepo", store.TryLoad());
        Assert.True(store.TryClear());
        Assert.Null(store.TryLoad());
    }

    [Fact]
    public async Task ActiveRepositoryFile_IsNotReportedAsAnIndexState()
    {
        ActiveRepositoryStore activeStore = MakeStore();
        Assert.True(activeStore.TrySave("MyRepo"));
        var indexStore = new IndexStateStore(
            Options.Create(new CodeAssistOptions { IndexStateDirectory = _directory }),
            NullLogger<IndexStateStore>.Instance);

        List<string> repositories = await indexStore.ListRepositoryNamesAsync(
            TestContext.Current.CancellationToken);

        Assert.Empty(repositories);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
