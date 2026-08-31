using McpGateway.Configuration;
using Xunit;

namespace McpGateway.Tests;

public sealed class ManifestStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "mcp-manifest-" + Guid.NewGuid().ToString("N"));

    private string ManifestPath => Path.Combine(_directory, "servers.json");

    /// <summary>
    /// Deliberately not under the manifest's directory: in production servers.json is in the repo
    /// and this is under %LOCALAPPDATA%, and the point of the split is that the two move
    /// independently.
    /// </summary>
    private string StatePath => Path.Combine(_directory, "state", "state.json");

    private ManifestStore Load() => ManifestStore.Load(ManifestPath, StatePath);

    public ManifestStoreTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ManifestPath, """
        {
          "code-assist": {
            "project": "CodeAssistMcp/CodeAssistMcp.csproj",
            "assembly": "CodeAssistMcp.dll",
            "deployRoot": "deploy/code-assist",
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
            "pool": "per-client",
            "overlapAllowed": true,
            "eagerStart": false,
            "idleTimeoutMinutes": 30,
            "startupTimeoutSeconds": 30
          }
        }
        """);

        TestState.Write(
            Path.GetDirectoryName(StatePath)!,
            ("code-assist", "v-146874c-20260830T1214"),
            ("sql", "v-146874c-20260830T1214"));
    }

    [Fact]
    public void Load_ReadsEveryEntry()
    {
        ManifestStore store = Load();

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
    public void Load_MergesTheActiveVersionFromTheStateFile()
    {
        ManifestStore store = Load();

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.Equal("v-146874c-20260830T1214", sql!.ActiveVersion);
    }

    /// <summary>
    /// The whole point of the split. servers.json is committed and must never carry a version, so
    /// a value left there by hand -- or by an older gateway, or restored by a git checkout -- is
    /// not what gets started. The state file deliberately says nothing about this server: with a
    /// recorded version present the merge would mask a manifest value that was being read anyway.
    /// </summary>
    [Fact]
    public void Load_IgnoresAnActiveVersionWrittenIntoTheManifest()
    {
        File.WriteAllText(ManifestPath, """
        {
          "sql": {
            "project": "SqlMcp/SqlMcp.csproj", "assembly": "SqlMcp.dll",
            "deployRoot": "deploy/sql", "activeVersion": "v-from-the-manifest",
            "pool": "per-client"
          }
        }
        """);

        TestState.Write(Path.GetDirectoryName(StatePath)!);

        ManifestStore store = Load();

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.Null(sql!.ActiveVersion);
    }

    [Fact]
    public void Load_PrefersTheStateFile_OverAnActiveVersionInTheManifest()
    {
        File.WriteAllText(ManifestPath, """
        {
          "sql": {
            "project": "SqlMcp/SqlMcp.csproj", "assembly": "SqlMcp.dll",
            "deployRoot": "deploy/sql", "activeVersion": "v-from-the-manifest",
            "pool": "per-client"
          }
        }
        """);

        TestState.Write(Path.GetDirectoryName(StatePath)!, ("sql", "v-from-the-state-file"));

        ManifestStore store = Load();

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.Equal("v-from-the-state-file", sql!.ActiveVersion);
    }

    /// <summary>
    /// Nothing deployed yet. It has to read as "no version", not as some placeholder that later
    /// resolves to a deploy directory nobody ever published.
    /// </summary>
    [Fact]
    public void Load_LeavesTheActiveVersionNull_WhenTheStateFileIsMissing()
    {
        File.Delete(StatePath);

        ManifestStore store = Load();

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.Null(sql!.ActiveVersion);
    }

    [Fact]
    public void Load_LeavesTheActiveVersionNull_ForAServerTheStateFileDoesNotMention()
    {
        TestState.Write(Path.GetDirectoryName(StatePath)!, ("sql", "v-only-sql"));

        ManifestStore store = Load();

        Assert.True(store.TryGet("code-assist", out ServerEntry? codeAssist));
        Assert.Null(codeAssist!.ActiveVersion);
    }

    /// <summary>
    /// Removing a server from servers.json is deliberate; a leftover runtime record must not
    /// resurrect it.
    /// </summary>
    [Fact]
    public void Load_IgnoresStateForAServerTheManifestNoLongerLists()
    {
        TestState.Write(
            Path.GetDirectoryName(StatePath)!, ("sql", "v-one"), ("retired", "v-nine"));

        ManifestStore store = Load();

        Assert.Equal(2, store.Entries.Count);
        Assert.False(store.TryGet("retired", out _));
    }

    [Fact]
    public void Load_Throws_WhenTheStateFileIsCorrupt()
    {
        File.WriteAllText(StatePath, "{ not json");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Load());

        Assert.Contains(StatePath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MarksPerClientEntriesAsNotShared()
    {
        ManifestStore store = Load();

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.False(sql!.IsShared);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownServer()
    {
        Assert.False(Load().TryGet("nope", out _));
    }

    [Fact]
    public async Task SetActiveVersionAsync_PersistsToDisk()
    {
        ManifestStore store = Load();

        await store.SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        ManifestStore reloaded = Load();
        Assert.True(reloaded.TryGet("sql", out ServerEntry? sql));
        Assert.Equal("v-abc1234-20260901T0900", sql!.ActiveVersion);
        Assert.Equal("per-client", sql.Pool);
    }

    /// <summary>
    /// The finding itself: an activation used to rewrite the git-tracked manifest, leaving the
    /// working tree dirty after every deploy and letting a checkout silently revert a live server's
    /// version.
    /// </summary>
    [Fact]
    public async Task SetActiveVersionAsync_LeavesTheManifestFileUntouched()
    {
        string before = File.ReadAllText(ManifestPath);
        DateTime writtenAt = File.GetLastWriteTimeUtc(ManifestPath);

        await Load().SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        Assert.Equal(before, File.ReadAllText(ManifestPath));
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(ManifestPath));
    }

    [Fact]
    public async Task SetActiveVersionAsync_KeepsTheOtherServersRecordedVersions()
    {
        await Load().SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        ManifestStore reloaded = Load();
        Assert.True(reloaded.TryGet("code-assist", out ServerEntry? codeAssist));
        Assert.Equal("v-146874c-20260830T1214", codeAssist!.ActiveVersion);
    }

    [Fact]
    public async Task SetActiveVersionAsync_UpdatesTheInMemoryView()
    {
        ManifestStore store = Load();

        await store.SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        Assert.True(store.TryGet("sql", out ServerEntry? sql));
        Assert.Equal("v-abc1234-20260901T0900", sql!.ActiveVersion);
    }

    [Fact]
    public async Task SetActiveVersionAsync_CreatesTheStateDirectory_WhenItDoesNotExist()
    {
        Directory.Delete(Path.GetDirectoryName(StatePath)!, recursive: true);

        await Load().SetActiveVersionAsync(
            "sql", "v-abc1234-20260901T0900", TestContext.Current.CancellationToken);

        Assert.True(File.Exists(StatePath));
    }

    [Fact]
    public async Task SetActiveVersionAsync_Throws_ForUnknownServer()
    {
        ManifestStore store = Load();

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
