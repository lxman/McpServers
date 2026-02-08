using System.ComponentModel;
using System.Text.Json;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Models;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MongoServer.Core;

namespace MongoMcp.McpTools;

/// <summary>
/// MCP tools for advanced MongoDB operations (aggregation, indexing, commands)
/// </summary>
[McpServerToolType]
public class AdvancedTools(
    ILogger<AdvancedTools> logger,
    MongoDbService mongoService,
    OutputGuard outputGuard)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("aggregate")]
    [Description("Execute MongoDB aggregation pipeline. See skills/mongo/advanced/aggregate.md only when using this tool")]
    public async Task<string> Aggregate(string serverName, string collectionName, string pipelineJson)
    {
        try
        {
            logger.LogDebug("Executing aggregation pipeline on collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return outputGuard.CreateErrorResponse("Server name is required", errorCode: "INVALID_PARAMETER");
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return outputGuard.CreateErrorResponse("Collection name is required", errorCode: "INVALID_PARAMETER");
            }

            if (string.IsNullOrWhiteSpace(pipelineJson))
            {
                return outputGuard.CreateErrorResponse("Pipeline JSON array is required", errorCode: "INVALID_PARAMETER");
            }

            string result = await mongoService.AggregateAsync(serverName, collectionName, pipelineJson);

            // Check response size before returning - aggregation can produce large result sets
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "aggregate");

            if (!sizeCheck.IsWithinLimit)
            {
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Aggregation pipeline returned results totaling {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Add $limit stage to pipeline to restrict result count\n" +
                    "  2. Add $project stage to select fewer fields\n" +
                    "  3. Use more selective $match stages earlier in pipeline\n" +
                    "  4. Break complex aggregations into multiple simpler queries",
                    new {
                        serverName,
                        collectionName,
                        suggestion = "Add { $limit: 50 } to the end of your pipeline"
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing aggregation on collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return ex.ToErrorResponse(outputGuard, errorCode: "AGGREGATE_FAILED");
        }
    }

    [McpServerTool, DisplayName("count_documents")]
    [Description("Count documents in a collection. See skills/mongo/advanced/count-documents.md only when using this tool")]
    public async Task<string> CountDocuments(string serverName, string collectionName, string? filterJson = null)
    {
        try
        {
            logger.LogDebug("Counting documents in collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            string result = await mongoService.CountDocumentsAsync(serverName, collectionName, filterJson ?? "{}");

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error counting documents in collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return ex.ToErrorResponse(outputGuard, errorCode: "COUNT_DOCUMENTS_FAILED");
        }
    }

    [McpServerTool, DisplayName("create_index")]
    [Description("Create an index on a collection. See skills/mongo/advanced/create-index.md only when using this tool")]
    public async Task<string> CreateIndex(string serverName, string collectionName, string indexJson, string? indexName = null)
    {
        try
        {
            logger.LogDebug("Creating index on collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(indexJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Index JSON is required" }, _jsonOptions);
            }

            string result = await mongoService.CreateIndexAsync(serverName, collectionName, indexJson, indexName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating index on collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return ex.ToErrorResponse(outputGuard, errorCode: "CREATE_INDEX_FAILED");
        }
    }

    [McpServerTool, DisplayName("drop_collection")]
    [Description("Drop a collection from the database. See skills/mongo/advanced/drop-collection.md only when using this tool")]
    public async Task<string> DropCollection(string serverName, string collectionName)
    {
        try
        {
            logger.LogDebug("Dropping collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            string result = await mongoService.DropCollectionAsync(serverName, collectionName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dropping collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return ex.ToErrorResponse(outputGuard, errorCode: "DROP_COLLECTION_FAILED");
        }
    }

    [McpServerTool, DisplayName("execute_command")]
    [Description("Execute a MongoDB database command. See skills/mongo/advanced/execute-command.md only when using this tool")]
    public async Task<string> ExecuteCommand(string serverName, string command)
    {
        try
        {
            logger.LogDebug("Executing MongoDB command on server {ServerName}", serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return outputGuard.CreateErrorResponse("Server name is required", errorCode: "INVALID_PARAMETER");
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                return outputGuard.CreateErrorResponse("Command JSON is required", errorCode: "INVALID_PARAMETER");
            }

            string result = await mongoService.ExecuteCommandAsync(serverName, command);

            // Check response size - commands can return large results
            ResponseSizeCheck sizeCheck = outputGuard.CheckStringSize(result, "execute_command");

            if (!sizeCheck.IsWithinLimit)
            {
                return outputGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Command returned results totaling {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. If using 'find' command, add a limit parameter\n" +
                    "  2. If using 'listCollections', filter by specific names\n" +
                    "  3. Use more specific commands that return less data\n" +
                    "  4. Consider using specialized tools instead of raw commands",
                    new {
                        serverName,
                        suggestion = "Use query, aggregate, or count_documents tools instead"
                    });
            }

            return sizeCheck.SerializedJson!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command on server {ServerName}", serverName);
            return ex.ToErrorResponse(outputGuard, errorCode: "EXECUTE_COMMAND_FAILED");
        }
    }
}
