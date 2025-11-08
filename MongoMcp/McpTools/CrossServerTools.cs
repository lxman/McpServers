using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MongoServer.Core.Services;

namespace MongoMcp.McpTools;

/// <summary>
/// MCP tools for cross-server MongoDB operations
/// </summary>
[McpServerToolType]
public class CrossServerTools(
    ILogger<CrossServerTools> logger,
    CrossServerOperations crossServerOps)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("compare_collections")]
    [Description("Compare collections across two MongoDB servers. See skills/mongo/cross-server/compare-collections.md only when using this tool")]
    public async Task<string> CompareCollections(string server1, string server2, string collectionName, string? filterJson = null)
    {
        try
        {
            logger.LogDebug("Comparing collection {CollectionName} between servers {Server1} and {Server2}", collectionName, server1, server2);

            if (string.IsNullOrWhiteSpace(server1))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server 1 name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(server2))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server 2 name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            var result = await crossServerOps.CompareCollectionsAsync(server1, server2, collectionName, filterJson ?? "{}");

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing collections between {Server1} and {Server2}", server1, server2);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("sync_data")]
    [Description("Sync data from source to target MongoDB server. See skills/mongo/cross-server/sync-data.md only when using this tool")]
    public async Task<string> SyncData(string sourceServer, string targetServer, string collectionName, string? filterJson = null, bool dryRun = true)
    {
        try
        {
            logger.LogDebug("Syncing data from {SourceServer} to {TargetServer} for collection {CollectionName} (DryRun: {DryRun})",
                sourceServer, targetServer, collectionName, dryRun);

            if (string.IsNullOrWhiteSpace(sourceServer))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Source server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(targetServer))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Target server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            var result = await crossServerOps.SyncDataAsync(sourceServer, targetServer, collectionName, filterJson ?? "{}", dryRun);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing data from {SourceServer} to {TargetServer}", sourceServer, targetServer);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("cross_server_query")]
    [Description("Execute query across multiple MongoDB servers. See skills/mongo/cross-server/cross-server-query.md only when using this tool")]
    public async Task<string> CrossServerQuery(string[] serverNames, string collectionName, string? filterJson = null, int limitPerServer = 50)
    {
        try
        {
            logger.LogDebug("Executing cross-server query on collection {CollectionName} across {ServerCount} servers",
                collectionName, serverNames?.Length ?? 0);

            if (serverNames == null || serverNames.Length == 0)
            {
                return JsonSerializer.Serialize(new { success = false, error = "At least one server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            var result = await crossServerOps.CrossServerQueryAsync(serverNames, collectionName, filterJson ?? "{}", limitPerServer);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing cross-server query");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("bulk_transfer")]
    [Description("Bulk transfer collections from source to target server. See skills/mongo/cross-server/bulk-transfer.md only when using this tool")]
    public async Task<string> BulkTransfer(string sourceServer, string targetServer, string[] collectionNames, bool dryRun = true)
    {
        try
        {
            logger.LogDebug("Bulk transferring {CollectionCount} collections from {SourceServer} to {TargetServer} (DryRun: {DryRun})",
                collectionNames?.Length ?? 0, sourceServer, targetServer, dryRun);

            if (string.IsNullOrWhiteSpace(sourceServer))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Source server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(targetServer))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Target server name is required" }, _jsonOptions);
            }

            if (collectionNames == null || collectionNames.Length == 0)
            {
                return JsonSerializer.Serialize(new { success = false, error = "At least one collection name is required" }, _jsonOptions);
            }

            var result = await crossServerOps.BulkTransferAsync(sourceServer, targetServer, collectionNames, dryRun);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during bulk transfer from {SourceServer} to {TargetServer}", sourceServer, targetServer);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("execute_on_all_servers")]
    [Description("Execute a MongoDB command on all connected servers. See skills/mongo/cross-server/execute-on-all-servers.md only when using this tool")]
    public async Task<string> ExecuteOnAllServers(string command)
    {
        try
        {
            logger.LogDebug("Executing command on all connected servers");

            if (string.IsNullOrWhiteSpace(command))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Command JSON is required" }, _jsonOptions);
            }

            var result = await crossServerOps.ExecuteOnAllServersAsync(command);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command on all servers");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_health_dashboard")]
    [Description("Get health status of all connected MongoDB servers. See skills/mongo/cross-server/get-health-dashboard.md only when using this tool")]
    public async Task<string> GetHealthDashboard()
    {
        try
        {
            logger.LogDebug("Getting health dashboard for all connected servers");

            var result = await crossServerOps.GetHealthDashboardAsync();

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting health dashboard");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
