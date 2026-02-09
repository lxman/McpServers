using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Models;
using Mcp.ResponseGuard.Services;
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
    CrossServerOperations crossServerOps,
    OutputGuard outputGuard)
{
    [McpServerTool, DisplayName("compare_collections")]
    [Description("Compare collections across two MongoDB servers. See skills/mongo/cross-server/compare-collections.md only when using this tool")]
    public async Task<string> CompareCollections(string server1, string server2, string collectionName, string? filterJson = null)
    {
        try
        {
            logger.LogDebug("Comparing collection {CollectionName} between servers {Server1} and {Server2}", collectionName, server1, server2);

            if (string.IsNullOrWhiteSpace(server1))
            {
                return outputGuard.CreateErrorResponse("Server 1 name is required", errorCode: "INVALID_PARAMETER");
            }

            if (string.IsNullOrWhiteSpace(server2))
            {
                return outputGuard.CreateErrorResponse("Server 2 name is required", errorCode: "INVALID_PARAMETER");
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return outputGuard.CreateErrorResponse("Collection name is required", errorCode: "INVALID_PARAMETER");
            }

            string result = await crossServerOps.CompareCollectionsAsync(server1, server2, collectionName, filterJson ?? "{}");

            // Check response size - comparisons can return large difference reports
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "compare_collections");

            if (!sizeCheck.IsWithinLimit)
            {
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Collection comparison returned results totaling {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Add more specific filter criteria to reduce documents compared\n" +
                    "  2. Use count_documents first to check collection sizes\n" +
                    "  3. Compare smaller subsets of data\n" +
                    "  4. Use sync_data with dryRun=true for a summary instead",
                    new {
                        server1,
                        server2,
                        collectionName
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing collections between {Server1} and {Server2}", server1, server2);
            return ex.ToErrorResponse(outputGuard, errorCode: "COMPARE_COLLECTIONS_FAILED");
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
                return JsonSerializer.Serialize(new { success = false, error = "Source server name is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (string.IsNullOrWhiteSpace(targetServer))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Target server name is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, SerializerOptions.JsonOptionsIndented);
            }

            string result = await crossServerOps.SyncDataAsync(sourceServer, targetServer, collectionName, filterJson ?? "{}", dryRun);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing data from {SourceServer} to {TargetServer}", sourceServer, targetServer);
            return ex.ToErrorResponse(outputGuard, errorCode: "SYNC_DATA_FAILED");
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
                return outputGuard.CreateErrorResponse("At least one server name is required", errorCode: "INVALID_PARAMETER");
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return outputGuard.CreateErrorResponse("Collection name is required", errorCode: "INVALID_PARAMETER");
            }

            string result = await crossServerOps.CrossServerQueryAsync(serverNames, collectionName, filterJson ?? "{}", limitPerServer);

            // Check response size - cross-server queries multiply results across servers
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "cross_server_query");

            if (!sizeCheck.IsWithinLimit)
            {
                int totalLimit = limitPerServer * serverNames.Length;
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Cross-server query across {serverNames.Length} servers returned results totaling {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Reduce limitPerServer parameter (currently {limitPerServer}, try 25 or 10)\n" +
                    "  2. Query fewer servers at once\n" +
                    "  3. Add more specific filter criteria\n" +
                    "  4. Use count_documents first to check total result size\n" +
                    "  5. Query servers individually instead of cross-server",
                    new {
                        serverCount = serverNames.Length,
                        currentLimitPerServer = limitPerServer,
                        suggestedLimitPerServer = Math.Max(5, limitPerServer / 4),
                        totalResults = totalLimit
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing cross-server query");
            return ex.ToErrorResponse(outputGuard, errorCode: "CROSS_SERVER_QUERY_FAILED");
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
                return JsonSerializer.Serialize(new { success = false, error = "Source server name is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (string.IsNullOrWhiteSpace(targetServer))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Target server name is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (collectionNames == null || collectionNames.Length == 0)
            {
                return JsonSerializer.Serialize(new { success = false, error = "At least one collection name is required" }, SerializerOptions.JsonOptionsIndented);
            }

            string result = await crossServerOps.BulkTransferAsync(sourceServer, targetServer, collectionNames, dryRun);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during bulk transfer from {SourceServer} to {TargetServer}", sourceServer, targetServer);
            return ex.ToErrorResponse(outputGuard, errorCode: "BULK_TRANSFER_FAILED");
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
                return outputGuard.CreateErrorResponse("Command JSON is required", errorCode: "INVALID_PARAMETER");
            }

            string result = await crossServerOps.ExecuteOnAllServersAsync(command);

            // Check response size - commands executed on all servers multiply results
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "execute_on_all_servers");

            if (!sizeCheck.IsWithinLimit)
            {
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Command execution across all servers returned results totaling {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Execute the command on fewer servers individually\n" +
                    "  2. Use more specific commands that return less data\n" +
                    "  3. Use get_health_dashboard for server status overview\n" +
                    "  4. Target specific servers instead of all servers",
                    new {
                        suggestion = "Execute command on servers individually instead"
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command on all servers");
            return ex.ToErrorResponse(outputGuard, errorCode: "EXECUTE_ON_ALL_SERVERS_FAILED");
        }
    }

    [McpServerTool, DisplayName("get_health_dashboard")]
    [Description("Get health status of all connected MongoDB servers. See skills/mongo/cross-server/get-health-dashboard.md only when using this tool")]
    public async Task<string> GetHealthDashboard()
    {
        try
        {
            logger.LogDebug("Getting health dashboard for all connected servers");

            string result = await crossServerOps.GetHealthDashboardAsync();

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting health dashboard");
            return ex.ToErrorResponse(outputGuard, errorCode: "GET_HEALTH_DASHBOARD_FAILED");
        }
    }
}
