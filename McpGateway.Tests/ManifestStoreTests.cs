using McpGateway.Configuration;
using Xunit;

namespace McpGateway.Tests;

public sealed class ManifestStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "mcp-manifest-" + Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "servers.json");

    public ManifestStoreTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ManifestPath, """
        {
          "code-assist": {
            "project": "CodeAssistMcp/CodeAssistMcp.csproj",
            "assembly": "CodeAssistMcp.dll",
            "deployRoot": "deploy/code-assist",
            "activeVersion": "v-146874c-20260830T1214",
            "pool": "shared",
            "overlapAllowed": false,
            "eagerStart": true,
            "idleTimeoutMinutes": 0,
            "startupTimeoutSeconds": 120
          },
          "sql": {
            "project": "SqlMcp/SqlMcp.csproj",
            "assembly": "SqlMcp.dll",
            "deployRoot": "deploy/sql",
            "activeVersion": "v-146874c-20260830T1214",
            "pool": "per-client",
            "overlapAllowed": true,
            "eagerStart": false,
            "idleTimeoutMinutes": 30,
            "startupTimeoutSeconds": 30
          }
        }
        """);
    }

    [Fact]
    public void Load_ReadsEveryEntry()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        Assert.Equal(2, store.Entries.Count);
        Assert.True(store.TryGet("code-assist", out ServerEntry? codeAssist));
        Assert.Equal("shared", codeAssist!.Pool);
        Assert.True(codeAssist.IsShared);
        Assert.False(codeAssist.OverlapAllowed);
        Assert.True(codeAssist.EagerStart);
        Assert.Equal(0, codeAssist.IdleTimeoutMinutes);
        Assert.Equal(120, codeAssist.StartupTimeoutSeconds);
    }

    [Fact]
    public void Load_MarksPerClientEntriesAsNotShared()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.False(sql!.IsShared);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownServer()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        Assert.False(store.TryGet("nope", out _));
    }

    [Fact]
    public async Task SetActiveVersionAsync_PersistsToDisk()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        await store.SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        ManifestStore reloaded = ManifestStore.Load(ManifestPath);
        Assert.True(reloaded.TryGet("sql", out ServerEntry? sql));
        Assert.Equal("v-abc1234-20260901T0900", sql!.ActiveVersion);
        Assert.Equal("per-client", sql.Pool);
    }

    [Fact]
    public async Task SetActiveVersionAsync_UpdatesTheInMemoryView()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        await store.SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.Equal("v-abc1234-20260901T0900", sql!.ActiveVersion);
    }

    [Fact]
    public async Task SetActiveVersionAsync_Throws_ForUnknownServer()
    {
        ManifestStore store = ManifestStore.Load(ManifestPath);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.SetActiveVersionAsync(
                "nope", "v-1", TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (DirectoryNotFoundException) { }
    }
}
