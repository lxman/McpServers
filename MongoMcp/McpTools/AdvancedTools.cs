using System.ComponentModel;
using System.Text.Json;
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
    MongoDbService mongoService)
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
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(pipelineJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Pipeline JSON array is required" }, _jsonOptions);
            }

            string result = await mongoService.AggregateAsync(serverName, collectionName, pipelineJson);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing aggregation on collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
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
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
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
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
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
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
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
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Command JSON is required" }, _jsonOptions);
            }

            string result = await mongoService.ExecuteCommandAsync(serverName, command);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command on server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
