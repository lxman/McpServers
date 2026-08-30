using McpGateway.Configuration;
using McpGateway.Supervision;
using McpGateway.Upgrade;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace McpGateway.Endpoints;

public sealed record ActivateRequest(string Version);

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/servers", (ManifestStore manifest, BackendSupervisor supervisor) =>
        {
            IReadOnlyCollection<BackendInstance> live = supervisor.All;

            return Results.Json(manifest.Entries.ToDictionary(
                pair => pair.Key,
                pair => new
                {
                    pair.Value.Pool,
                    pair.Value.ActiveVersion,
                    pair.Value.OverlapAllowed,
                    pair.Value.EagerStart,
                    pair.Value.IdleTimeoutMinutes,
                    backends = live
                        .Where(instance => instance.Key.Server == pair.Key)
                        .Select(instance => new
                        {
                            poolKey = instance.Key.PoolKey,
                            instance.Version,
                            instance.Port,
                            pid = instance.Handle.ProcessId,
                            instance.InFlight,
                            lastUsedAt = instance.LastUsedAt
                        })
                        .ToList()
                }));
        });

        app.MapPost("/admin/servers/{name}/activate", async (
            string name,
            ActivateRequest body,
            ActivationService activation,
            ManifestStore manifest,
            CancellationToken cancellationToken) =>
        {
            if (!manifest.TryGet(name, out _)) return Results.NotFound($"No server named '{name}'.");

            ActivationResult result = await activation.ActivateAsync(
                name, body.Version, cancellationToken);

            return result.Succeeded
                ? Results.Json(result)
                : Results.Json(result, statusCode: StatusCodes.Status409Conflict);
        });

        app.MapPost("/admin/servers/{name}/stop", async (
            string name,
            BackendSupervisor supervisor,
            ManifestStore manifest,
            CancellationToken cancellationToken) =>
        {
            if (!manifest.TryGet(name, out _)) return Results.NotFound($"No server named '{name}'.");

            List<BackendKey> keys = supervisor.All
                .Where(instance => instance.Key.Server == name)
                .Select(instance => instance.Key)
                .ToList();

            foreach (BackendKey key in keys) await supervisor.StopAsync(key, cancellationToken);

            return Results.Json(new { stopped = keys.Count });
        });

        // A restart is just an activation to the server's own current version. Routing it through
        // ActivateAsync -- rather than a hand-rolled stop/start loop -- inherits the gate, the
        // hold, the ordering guarantee and the error reporting for free, and closes a real race: a
        // hand-rolled restart could stop-and-restart a key while a concurrent activation held a
        // stale reference to the very same key.
        app.MapPost("/admin/servers/{name}/restart", async (
            string name,
            ActivationService activation,
            ManifestStore manifest,
            CancellationToken cancellationToken) =>
        {
            if (!manifest.TryGet(name, out ServerEntry? entry))
            {
                return Results.NotFound($"No server named '{name}'.");
            }

            ActivationResult result = await activation.ActivateAsync(
                name, entry!.ActiveVersion, cancellationToken);

            return result.Succeeded
                ? Results.Json(result)
                : Results.Json(result, statusCode: StatusCodes.Status409Conflict);
        });

        // Pruning deletes deploy directories, so it goes through ActivationService.PruneAsync
        // rather than BackendSupervisor.PruneVersionsAsync directly -- taking the same gate an
        // activation holds closes the window where a concurrent prune could delete the directory a
        // mid-flight swap is about to start from.
        app.MapPost("/admin/prune", async (
            ManifestStore manifest,
            ActivationService activation,
            CancellationToken cancellationToken) =>
        {
            var pruned = new Dictionary<string, IReadOnlyList<string>>();

            foreach (string server in manifest.Entries.Keys)
            {
                pruned[server] = await activation.PruneAsync(server, cancellationToken);
            }

            return Results.Json(pruned);
        });

        return app;
    }
}
