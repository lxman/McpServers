using System.Collections.Concurrent;
using McpGateway.Configuration;
using Mcp.Hosting.Core;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

public sealed class BackendSupervisor(
    ManifestStore manifest,
    IBackendLauncher launcher,
    HealthProbe healthProbe,
    GatewayBuildOptions options,
    string shutdownToken,
    ILogger<BackendSupervisor> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<BackendKey, Lazy<Task<BackendInstance>>> _pool = new();

    public IReadOnlyCollection<BackendInstance> All => _pool.Values
        .Where(entry => entry.IsValueCreated && entry.Value.IsCompletedSuccessfully)
        .Select(entry => entry.Value.Result)
        .ToList();

    /// <summary>
    /// Returns the running backend for this key, starting it if needed. Concurrent callers for the
    /// same key await the same start rather than racing to spawn duplicates.
    /// </summary>
    public async Task<BackendInstance> GetOrStartAsync(
        BackendKey key, CancellationToken cancellationToken)
    {
        while (true)
        {
            Lazy<Task<BackendInstance>> entry = _pool.GetOrAdd(key, k => new Lazy<Task<BackendInstance>>(
                () => StartAsync(k, ResolveEntry(k.Server).ActiveVersion, CancellationToken.None)));

            try
            {
                BackendInstance instance = await entry.Value.WaitAsync(cancellationToken);

                // A crashed backend is evicted and restarted on the next request rather than
                // handed out dead.
                if (instance.Handle.HasExited)
                {
                    RemoveIfSame(key, entry);
                    continue;
                }

                return instance;
            }
            catch
            {
                // Evict only a genuinely failed start, so the key is not poisoned forever.
                // Task.WaitAsync throws when THIS caller's token fires while the shared start is
                // still running — the start itself is unaffected and will finish. Evicting on that
                // would hand the next caller a duplicate spawn and orphan the process still coming
                // up, with nothing holding a handle to stop it.
                if (entry.Value.IsFaulted) RemoveIfSame(key, entry);
                throw;
            }
        }
    }

    /// <summary>Starts a backend that the pool does not own, for a blue/green swap.</summary>
    public Task<BackendInstance> StartDetachedAsync(
        BackendKey key, string version, CancellationToken cancellationToken) =>
        StartAsync(key, version, cancellationToken);

    public bool TryGet(BackendKey key, out BackendInstance? instance)
    {
        instance = null;

        if (!_pool.TryGetValue(key, out Lazy<Task<BackendInstance>>? entry)) return false;
        if (!entry.IsValueCreated || !entry.Value.IsCompletedSuccessfully) return false;

        instance = entry.Value.Result;
        return true;
    }

    /// <summary>Swaps in an already-started backend. Returns the one it displaced, if any.</summary>
    public BackendInstance? Replace(BackendKey key, BackendInstance instance)
    {
        TryGet(key, out BackendInstance? previous);

        _pool[key] = new Lazy<Task<BackendInstance>>(() => Task.FromResult(instance));

        return previous;
    }

    public async Task StopAsync(BackendKey key, CancellationToken cancellationToken)
    {
        if (!_pool.TryRemove(key, out Lazy<Task<BackendInstance>>? entry)) return;
        if (!entry.IsValueCreated) return;

        BackendInstance instance;
        try
        {
            // A start still in flight has to be awaited rather than abandoned: its process would
            // otherwise finish coming up with nothing left holding a handle to stop it.
            instance = await entry.Value;
        }
        catch (Exception)
        {
            // The start failed, so there is no process to stop.
            return;
        }

        await instance.StopAsync(cancellationToken);
    }

    public ServerEntry ResolveEntry(string server) =>
        manifest.TryGet(server, out ServerEntry? entry)
            ? entry!
            : throw new KeyNotFoundException($"No server named '{server}' in the manifest.");

    private async Task<BackendInstance> StartAsync(
        BackendKey key, string version, CancellationToken cancellationToken)
    {
        ServerEntry entry = ResolveEntry(key.Server);

        string assemblyPath = Path.Combine(
            options.RepoRoot, entry.DeployRoot, version, entry.Assembly);

        string portFilePath = Path.Combine(Path.GetTempPath(), "mcp-gateway-ports",
            $"{key.Server}-{Guid.NewGuid():N}.json");

        logger.LogInformation("Starting {Key} version {Version}", key, version);

        IBackendHandle handle = launcher.Start(new BackendLaunchRequest(
            key.Server, version, assemblyPath, portFilePath, shutdownToken));

        var timeout = TimeSpan.FromSeconds(entry.StartupTimeoutSeconds);

        try
        {
            PortFileContent port = await WaitForPortFileAsync(
                portFilePath, handle, timeout, cancellationToken);

            if (!await healthProbe.WaitUntilHealthyAsync(port.Port, timeout, cancellationToken))
            {
                throw new BackendStartupException(
                    $"{key} started on port {port.Port} but never reported healthy within {timeout}.",
                    ReadLogTail(key.Server));
            }

            logger.LogInformation(
                "{Key} healthy on port {Port} (pid {Pid})", key, port.Port, port.Pid);

            return new BackendInstance(key, version, port.Port, handle, shutdownToken);
        }
        catch
        {
            await handle.DisposeAsync();
            throw;
        }
        finally
        {
            try { File.Delete(portFilePath); } catch (IOException) { }
        }
    }

    private static async Task<PortFileContent> WaitForPortFileAsync(
        string path, IBackendHandle handle, TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (PortFile.TryRead(path, out PortFileContent content)) return content;

            if (handle.HasExited)
            {
                throw new BackendStartupException(
                    "Backend exited before writing its port file.", string.Empty);
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new BackendStartupException(
            $"Backend did not write its port file at {path} within {timeout}.", string.Empty);
    }

    private static string ReadLogTail(string serverName)
    {
        try
        {
            string directory = Path.GetDirectoryName(McpHttpHost.LogPathFor(serverName))!;
            string? newest = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.log")
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;

            if (newest is null) return string.Empty;

            using var stream = new FileStream(
                newest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            return string.Join(Environment.NewLine,
                reader.ReadToEnd().Split(Environment.NewLine).TakeLast(20));
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private void RemoveIfSame(BackendKey key, Lazy<Task<BackendInstance>> entry) =>
        // Atomic compare-and-remove. The check-then-act form could delete a healthy replacement
        // that Replace() installed between the read and the remove — and Replace is exactly what
        // the blue/green swap in later tasks uses.
        _pool.TryRemove(new KeyValuePair<BackendKey, Lazy<Task<BackendInstance>>>(key, entry));

    public async ValueTask DisposeAsync()
    {
        foreach (BackendKey key in _pool.Keys.ToList())
        {
            await StopAsync(key, CancellationToken.None);
        }
    }
}
