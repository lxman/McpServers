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
    string backendToken,
    LiveBackendRegistry registry,
    ILogger<BackendSupervisor> logger,
    TimeProvider? time = null) : IAsyncDisposable
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ConcurrentDictionary<BackendKey, Lazy<Task<BackendInstance>>> _pool = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _holds =
        new(StringComparer.OrdinalIgnoreCase);

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
            if (_holds.TryGetValue(key.Server, out TaskCompletionSource? hold))
            {
                await hold.Task.WaitAsync(cancellationToken);
            }

            Lazy<Task<BackendInstance>> entry = _pool.GetOrAdd(key, k => new Lazy<Task<BackendInstance>>(
                () => StartAsync(k, RequireActiveVersion(k.Server), CancellationToken.None)));

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

    /// <summary>
    /// Blocks new starts for a server while a swap is in progress. Callers of GetOrStartAsync wait
    /// on the hold instead of getting an error, which is what makes a stop-then-start upgrade cost
    /// latency rather than failed calls.
    /// </summary>
    public Task<IAsyncDisposable> HoldAsync(string server, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_holds.TryAdd(server, source))
        {
            throw new InvalidOperationException($"A swap is already in progress for '{server}'.");
        }

        return Task.FromResult<IAsyncDisposable>(new Hold(this, server, source));
    }

    private sealed class Hold(BackendSupervisor owner, string server, TaskCompletionSource source)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner._holds.TryRemove(server, out _);
            source.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Deletes version directories that are neither active nor backing a live backend. A directory
    /// whose files are still locked is skipped rather than fought.
    /// </summary>
    public Task<IReadOnlyList<string>> PruneVersionsAsync(
        string server, CancellationToken cancellationToken)
    {
        ServerEntry entry = ResolveEntry(server);
        string root = Path.Combine(options.RepoRoot, entry.DeployRoot);

        var pruned = new List<string>();
        if (!Directory.Exists(root)) return Task.FromResult<IReadOnlyList<string>>(pruned);

        HashSet<string> keep = All
            .Where(instance => instance.Key.Server == server)
            .Select(instance => instance.Version)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A server that has never been deployed has no active version to protect; everything
        // under its deploy root that nothing is running is prunable.
        if (entry.ActiveVersion is not null) keep.Add(entry.ActiveVersion);

        foreach (string directory in Directory.GetDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string name = Path.GetFileName(directory);
            if (keep.Contains(name)) continue;

            try
            {
                Directory.Delete(directory, recursive: true);
                pruned.Add(name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogInformation(
                    "Left {Directory} in place; something still holds it", directory);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(pruned);
    }

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

        // Lazy(T value) is already-created, so IsValueCreated is true immediately. A deferred
        // factory here would make the swapped-in backend invisible to TryGet, All and StopAsync,
        // which all short-circuit on !IsValueCreated -- the blue/green swap would install a live
        // backend that /admin/servers never lists and the idle reaper never reaps.
        _pool[key] = new Lazy<Task<BackendInstance>>(Task.FromResult(instance));

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
            instance = await entry.Value.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The caller gave up waiting -- a slow server's startup timeout must not pin a
            // shutdown. Hand the teardown to a continuation so the process is still stopped when
            // the start finally lands, instead of leaking it.
            _ = entry.Value.ContinueWith(
                started => started.Result.StopAsync(CancellationToken.None),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
            return;
        }
        catch (Exception)
        {
            // The start failed, so there is no process to stop.
            return;
        }

        await instance.StopAsync(cancellationToken);
    }

    /// <summary>Drops backends whose process has gone. The next request starts a fresh one.</summary>
    public async Task<int> EvictExitedAsync(CancellationToken cancellationToken)
    {
        var evicted = 0;

        foreach (BackendInstance instance in All)
        {
            if (!instance.Handle.HasExited) continue;

            logger.LogWarning(
                "{Key} exited unexpectedly (pid {Pid}); it will restart on the next request",
                instance.Key, instance.Handle.ProcessId);

            await StopAsync(instance.Key, cancellationToken);
            evicted++;
        }

        return evicted;
    }

    public ServerEntry ResolveEntry(string server) =>
        manifest.TryGet(server, out ServerEntry? entry)
            ? entry!
            : throw new KeyNotFoundException($"No server named '{server}' in the manifest.");

    /// <summary>
    /// The version to start, or a message saying the server was never deployed. Thrown as a
    /// BackendStartupException so it reaches the caller as a 503 that names the problem, rather
    /// than as a path that resolves to a deploy directory nobody ever published.
    /// </summary>
    public string RequireActiveVersion(string server) =>
        ResolveEntry(server).ActiveVersion
        ?? throw new BackendStartupException(
            $"{server} has no active version recorded, so nothing has ever been deployed for it. " +
            $"Publish it and activate the version -- that is what writes {options.StatePath}.",
            string.Empty);

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
            key.Server, version, assemblyPath, portFilePath, backendToken));

        // Recorded before the health gate, not after. code-assist's startup timeout is 120 seconds,
        // and a gateway killed inside that window would otherwise leave a running process that
        // nothing -- not the pool, not the port file, not this registry -- has any record of.
        var record = new LiveBackendRecord(
            key.Server, key.PoolKey, version, handle.ProcessId, Port: 0, handle.StartedAt);

        registry.Record(record);

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

            registry.Record(record with { Port = port.Port });

            return new BackendInstance(
                key, version, port.Port, handle, backendToken, registry, _time);
        }
        catch
        {
            await handle.DisposeAsync();
            registry.Forget(handle.ProcessId);
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
        // Bounded: without this a backend still inside its startup timeout (120s for code-assist)
        // would pin the whole gateway shutdown.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        foreach (BackendKey key in _pool.Keys.ToList())
        {
            await StopAsync(key, timeout.Token);
        }
    }
}
