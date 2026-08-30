namespace McpGateway.Supervision;

public sealed class BackendInstance(
    BackendKey key,
    string version,
    int port,
    IBackendHandle handle,
    string shutdownToken,
    TimeProvider time)
{
    private readonly object _gate = new();
    private TaskCompletionSource _drained = CreateDrainedSource(signalled: true);
    private int _inFlight;

    public BackendKey Key { get; } = key;
    public string Version { get; } = version;
    public int Port { get; } = port;
    public IBackendHandle Handle { get; } = handle;
    public string DestinationPrefix { get; } = $"http://127.0.0.1:{port}";
    public DateTimeOffset LastUsedAt { get; private set; } = time.GetUtcNow();

    public int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>Marks a request as in flight. Disposing the lease releases it.</summary>
    public IDisposable BeginRequest()
    {
        lock (_gate)
        {
            if (_inFlight++ == 0) _drained = CreateDrainedSource(signalled: false);
            LastUsedAt = time.GetUtcNow();
        }

        return new Lease(this);
    }

    /// <summary>True if the backend went quiet within the timeout; false if requests outlasted it.</summary>
    public async Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task drained;
        lock (_gate) { drained = _drained.Task; }

        Task finished = await Task.WhenAny(drained, Task.Delay(timeout, cancellationToken));
        return ReferenceEquals(finished, drained);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{DestinationPrefix}/admin/shutdown");
            request.Headers.Add("Authorization", $"Bearer {shutdownToken}");

            await client.SendAsync(request, cancellationToken);
        }
        catch (Exception)
        {
            // A backend that won't answer its shutdown endpoint still gets disposed below.
        }

        await Handle.DisposeAsync();
    }

    private void Release()
    {
        lock (_gate)
        {
            if (--_inFlight == 0) _drained.TrySetResult();
            LastUsedAt = time.GetUtcNow();
        }
    }

    private static TaskCompletionSource CreateDrainedSource(bool signalled)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (signalled) source.SetResult();
        return source;
    }

    private sealed class Lease(BackendInstance owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) owner.Release();
        }
    }
}
