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

            // Start and health-gate EVERY replacement before touching a single live backend.
            // Swapping as we go would leave earlier backends on the new version with their old
            // instances already stopped, while a later failure skipped the manifest write -- a
            // fleet running one version and a manifest claiming another, which everything
            // downstream reads.
            var replacements = new List<(BackendInstance Old, BackendInstance New)>();

            try
            {
                foreach (BackendInstance old in live)
                {
                    replacements.Add(
                        (old, await supervisor.StartDetachedAsync(old.Key, version, cancellationToken)));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex, "Could not start {Server} at {Version}; nothing was swapped", server, version);

                foreach ((_, BackendInstance started) in replacements)
                {
                    await started.StopAsync(cancellationToken);
                }

                return new ActivationResult(
                    false, server, from, version, 0, false,
                    $"New version failed to start: {ex.Message}");
            }

            var drainTimedOut = false;

            foreach ((BackendInstance old, BackendInstance replacement) in replacements)
            {
                supervisor.Replace(old.Key, replacement);

                if (!await old.WaitForDrainAsync(DrainTimeout, cancellationToken))
                {
                    logger.LogWarning(
                        "{Key} still had {InFlight} request(s) after {Timeout}; stopping anyway",
                        old.Key, old.InFlight, DrainTimeout);
                    drainTimedOut = true;
                }

                await old.StopAsync(cancellationToken);
            }

            int swapped = replacements.Count;

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
