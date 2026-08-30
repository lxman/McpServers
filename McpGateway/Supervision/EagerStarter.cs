using McpGateway.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

/// <summary>
/// Starts the servers whose first-call latency would otherwise be paid by a user request. Only
/// servers marked eagerStart — everything else stays lazy, which is what keeps fifteen HTTP
/// services cheaper than fifteen stdio processes.
/// </summary>
public sealed class EagerStarter(
    BackendSupervisor supervisor,
    ManifestStore manifest,
    ILogger<EagerStarter> logger) : IHostedService
{
    public async Task StartEagerServersAsync(CancellationToken cancellationToken)
    {
        foreach ((string name, ServerEntry entry) in manifest.Entries)
        {
            if (!entry.EagerStart) continue;

            // Shared servers have one backend with an empty pool key. A per-client server has no
            // client to start for yet, so eager start only makes sense for shared ones.
            if (!entry.IsShared)
            {
                logger.LogWarning(
                    "{Server} is marked eagerStart but pooled per-client; skipping", name);
                continue;
            }

            try
            {
                await supervisor.GetOrStartAsync(new BackendKey(name, string.Empty), cancellationToken);
                logger.LogInformation("Eagerly started {Server}", name);
            }
            catch (Exception ex)
            {
                // The gateway must come up even if one backend cannot.
                logger.LogError(ex, "Could not eagerly start {Server}", name);
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Fire and forget: the gateway must accept requests immediately, and anything not yet
        // started simply starts lazily on its first call.
        _ = Task.Run(() => StartEagerServersAsync(CancellationToken.None), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
