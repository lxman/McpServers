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

        app.MapPost("/admin/servers/{name}/restart", async (
            string name,
            BackendSupervisor supervisor,
            ManifestStore manifest,
            CancellationToken cancellationToken) =>
        {
            if (!manifest.TryGet(name, out ServerEntry? entry))
            {
                return Results.NotFound($"No server named '{name}'.");
            }

            List<BackendKey> keys = supervisor.All
                .Where(instance => instance.Key.Server == name)
                .Select(instance => instance.Key)
                .ToList();

            foreach (BackendKey key in keys)
            {
                await supervisor.StopAsync(key, cancellationToken);
                await supervisor.GetOrStartAsync(key, cancellationToken);
            }

            return Results.Json(new { restarted = keys.Count, entry!.ActiveVersion });
        });

        app.MapPost("/admin/prune", async (
            ManifestStore manifest,
            BackendSupervisor supervisor,
            CancellationToken cancellationToken) =>
        {
            var pruned = new Dictionary<string, IReadOnlyList<string>>();

            foreach (string server in manifest.Entries.Keys)
            {
                pruned[server] = await supervisor.PruneVersionsAsync(server, cancellationToken);
            }

            return Results.Json(pruned);
        });

        return app;
    }
}
