using McpGateway.Configuration;
using McpGateway.Supervision;
using Microsoft.Extensions.Logging;

namespace McpGateway.Upgrade;

public sealed class ActivationService(
    BackendSupervisor supervisor,
    ManifestStore manifest,
    ILogger<ActivationService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// How long in-flight requests get to finish before the old backend is killed anyway. This is
    /// the only window in which an upgrade can fail a call.
    /// </summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<ActivationResult> ActivateAsync(
        string server, string version, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ServerEntry entry = supervisor.ResolveEntry(server);
            string from = entry.ActiveVersion;

            List<BackendInstance> live = supervisor.All
                .Where(instance => instance.Key.Server == server)
                .ToList();

            var swapped = 0;
            var drainTimedOut = false;

            foreach (BackendInstance old in live)
            {
                BackendInstance replacement;
                try
                {
                    replacement = await supervisor.StartDetachedAsync(
                        old.Key, version, cancellationToken);
                }
                catch (Exception ex)
                {
                    // The old backend is untouched, so the failure costs nothing but the attempt.
                    logger.LogError(ex, "Could not start {Key} at {Version}", old.Key, version);

                    return new ActivationResult(
                        false, server, from, version, swapped, drainTimedOut,
                        $"New version failed to start: {ex.Message}");
                }

                supervisor.Replace(old.Key, replacement);
                swapped++;

                if (!await old.WaitForDrainAsync(DrainTimeout, cancellationToken))
                {
                    logger.LogWarning(
                        "{Key} still had {InFlight} request(s) after {Timeout}; stopping anyway",
                        old.Key, old.InFlight, DrainTimeout);
                    drainTimedOut = true;
                }

                await old.StopAsync(cancellationToken);
            }

            await manifest.SetActiveVersionAsync(server, version, cancellationToken);

            logger.LogInformation(
                "Activated {Server} {From} -> {To}, {Count} backend(s) swapped",
                server, from, version, swapped);

            return new ActivationResult(
                true, server, from, version, swapped, drainTimedOut, null);
        }
        finally
        {
            _gate.Release();
        }
    }
}
