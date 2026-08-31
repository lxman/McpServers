using McpGateway.Configuration;
using McpGateway.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

/// <summary>
/// Stops backends nobody has used lately. Without this, moving fifteen servers to long-lived
/// services would cost more memory than the per-session stdio processes it replaces.
/// </summary>
public sealed class IdleReaper(
    BackendSupervisor supervisor,
    TimeProvider time,
    ILogger<IdleReaper> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await supervisor.EvictExitedAsync(cancellationToken);

        var stopped = 0;
        DateTimeOffset now = time.GetUtcNow();

        foreach (BackendInstance instance in supervisor.All)
        {
            ServerEntry entry = supervisor.ResolveEntry(instance.Key.Server);

            if (instance.InFlight > 0) continue;

            // Checked before the idle rules, and independently of them. A per-session backend
            // whose session has exited will never be called again, but the idle timeout knows
            // nothing about that and keeps it alive for the full window -- holding whatever the
            // session held, which for ssh-mcp is open connections rather than a few megabytes.
            // The pool key IS the owner's pid and start time, so this costs one process lookup.
            //
            // Deliberately ahead of the IdleTimeoutMinutes <= 0 guard: "never reap on idleness"
            // should not mean "keep a dead session's backend until the gateway restarts".
            if (entry.IsPerSession && !SessionIdentity.IsOwnerAlive(instance.Key.PoolKey))
            {
                logger.LogInformation(
                    "Stopping {Key}; the session that owns it has exited", instance.Key);

                await supervisor.StopAsync(instance.Key, cancellationToken);
                stopped++;
                continue;
            }

            // Zero means never reap — used for eagerly started servers like code-assist whose
            // startup cost is a graph build.
            if (entry.IdleTimeoutMinutes <= 0) continue;

            if (now - instance.LastUsedAt < TimeSpan.FromMinutes(entry.IdleTimeoutMinutes)) continue;

            logger.LogInformation(
                "Stopping {Key}; idle since {LastUsed}", instance.Key, instance.LastUsedAt);

            await supervisor.StopAsync(instance.Key, cancellationToken);
            stopped++;
        }

        return stopped;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(SweepInterval, time);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Idle sweep failed");
            }
        }
    }
}
