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

            if (!entry.OverlapAllowed)
            {
                return await ActivateExclusiveAsync(
                    server, from, version, live, cancellationToken);
            }

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
            var swapped = 0;

            // Once a backend has been Replace()'d there is nothing to roll back to -- the old
            // instance may already be stopped, or stopping it may be what threw. An exception here
            // cannot be undone, so the goal is not recovery: it is a loud, structured, truthful
            // ActivationResult instead of an exception escaping as an unhandled 500. The manifest
            // write stays here, last, inside the same guarded region.
            try
            {
                foreach ((BackendInstance old, BackendInstance replacement) in replacements)
                {
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
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Swap failed for {Server} after {Swapped} backend(s) were already replaced " +
                    "with {Version}; fleet and manifest may disagree", server, swapped, version);

                return new ActivationResult(
                    false, server, from, version, swapped, drainTimedOut,
                    $"Swap failed after {swapped} backend(s) were already replaced; fleet and " +
                    $"manifest may disagree: {ex.Message}");
            }

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

    /// <summary>
    /// For servers whose machine-wide state two live instances would corrupt. Requests arriving
    /// mid-swap are held by the supervisor until the new backend is up.
    /// </summary>
    private async Task<ActivationResult> ActivateExclusiveAsync(
        string server,
        string from,
        string version,
        List<BackendInstance> live,
        CancellationToken cancellationToken)
    {
        await using IAsyncDisposable hold = await supervisor.HoldAsync(server, cancellationToken);

        var drainTimedOut = false;
        var swapped = 0;

        foreach (BackendInstance old in live)
        {
            try
            {
                if (!await old.WaitForDrainAsync(DrainTimeout, cancellationToken)) drainTimedOut = true;

                await supervisor.StopAsync(old.Key, cancellationToken);
            }
            catch (Exception ex)
            {
                // Stopping is the one step here with no undo: BackendSupervisor.StopAsync removes
                // the pool entry before it tries to tear down the process, so a throw from that
                // teardown (a real process kill failing) leaves no entry behind and no way to know
                // whether the old process actually died. Guessing at its state would be worse than
                // reporting -- this is the two-live-instance hazard the whole task exists to
                // prevent, so it is called out explicitly rather than folded into a generic message.
                logger.LogCritical(ex,
                    "Could not stop {Key} while swapping {Server} to {Version}; the old process " +
                    "may still be alive, so {Server} may now be running two instances -- the exact " +
                    "hazard this task exists to prevent", old.Key, server, version, server);

                return new ActivationResult(
                    false, server, from, version, swapped, drainTimedOut,
                    $"Swap failed while stopping the previous version of {old.Key}; the old " +
                    $"process may still be alive, risking two live instances of a non-overlap " +
                    $"server: {ex.Message}");
            }

            try
            {
                BackendInstance replacement = await supervisor.StartDetachedAsync(
                    old.Key, version, cancellationToken);

                supervisor.Replace(old.Key, replacement);
                swapped++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not start {Key} at {Version}; restoring {From}",
                    old.Key, version, from);

                // Nothing to fall back to, so bring the previous version back up. Held requests
                // survive if this succeeds.
                try
                {
                    BackendInstance restored = await supervisor.StartDetachedAsync(
                        old.Key, from, cancellationToken);
                    supervisor.Replace(old.Key, restored);
                }
                catch (Exception restoreFailure)
                {
                    logger.LogCritical(restoreFailure,
                        "Could not restore {Key} at {From}; it will start on the next request",
                        old.Key, from);
                }

                return new ActivationResult(
                    false, server, from, version, swapped, drainTimedOut,
                    $"New version failed to start: {ex.Message}");
            }
        }

        try
        {
            await manifest.SetActiveVersionAsync(server, version, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Swap succeeded for {Server} ({Swapped} backend(s) now on {Version}) but the " +
                "manifest write failed; fleet and manifest may disagree", server, swapped, version);

            return new ActivationResult(
                false, server, from, version, swapped, drainTimedOut,
                $"Swap succeeded but the manifest write failed after {swapped} backend(s) were " +
                $"already replaced; fleet and manifest may disagree: {ex.Message}");
        }

        return new ActivationResult(true, server, from, version, swapped, drainTimedOut, null);
    }
}
