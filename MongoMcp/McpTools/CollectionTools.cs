using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MongoServer.Core;

namespace MongoMcp.McpTools;

/// <summary>
/// MCP tools for MongoDB collection operations (CRUD)
/// </summary>
[McpServerToolType]
public class CollectionTools(
    ILogger<CollectionTools> logger,
    MongoDbService mongoService)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("insert_one")]
    [Description("Insert a single document into a collection. See skills/mongo/collection/insert-one.md only when using this tool")]
    public async Task<string> InsertOne(string serverName, string collectionName, string documentJson)
    {
        try
        {
            logger.LogDebug("Inserting one document into collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(documentJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Document JSON is required" }, _jsonOptions);
            }

            string result = await mongoService.InsertOneAsync(serverName, collectionName, documentJson);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inserting document into collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("insert_many")]
    [Description("Insert multiple documents into a collection. See skills/mongo/collection/insert-many.md only when using this tool")]
    public async Task<string> InsertMany(string serverName, string collectionName, string documentsJson)
    {
        try
        {
            logger.LogDebug("Inserting multiple documents into collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(documentsJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Documents JSON array is required" }, _jsonOptions);
            }

            string result = await mongoService.InsertManyAsync(serverName, collectionName, documentsJson);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inserting documents into collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("query")]
    [Description("Query documents from a collection. See skills/mongo/collection/query.md only when using this tool")]
    public async Task<string> Query(string serverName, string collectionName, string filterJson, int limit = 100, int skip = 0)
    {
        try
        {
            logger.LogDebug("Querying collection {CollectionName} on server {ServerName} with limit {Limit} and skip {Skip}",
                collectionName, serverName, limit, skip);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            string result = await mongoService.QueryAsync(serverName, collectionName, filterJson ?? "{}", limit, skip);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("update_one")]
    [Description("Update a single document in a collection. See skills/mongo/collection/update-one.md only when using this tool")]
    public async Task<string> UpdateOne(string serverName, string collectionName, string filterJson, string updateJson)
    {
        try
        {
            logger.LogDebug("Updating one document in collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(filterJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Filter JSON is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(updateJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Update JSON is required" }, _jsonOptions);
            }

            string result = await mongoService.UpdateOneAsync(serverName, collectionName, filterJson, updateJson);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating document in collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("update_many")]
    [Description("Update multiple documents in a collection. See skills/mongo/collection/update-many.md only when using this tool")]
    public async Task<string> UpdateMany(string serverName, string collectionName, string filterJson, string updateJson)
    {
        try
        {
            logger.LogDebug("Updating multiple documents in collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(filterJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Filter JSON is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(updateJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Update JSON is required" }, _jsonOptions);
            }

            string result = await mongoService.UpdateManyAsync(serverName, collectionName, filterJson, updateJson);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating documents in collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_one")]
    [Description("Delete a single document from a collection. See skills/mongo/collection/delete-one.md only when using this tool")]
    public async Task<string> DeleteOne(string serverName, string collectionName, string filterJson)
    {
        try
        {
            logger.LogDebug("Deleting one document from collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(filterJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Filter JSON is required" }, _jsonOptions);
            }

            string result = await mongoService.DeleteOneAsync(serverName, collectionName, filterJson);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting document from collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_many")]
    [Description("Delete multiple documents from a collection. See skills/mongo/collection/delete-many.md only when using this tool")]
    public async Task<string> DeleteMany(string serverName, string collectionName, string filterJson)
    {
        try
        {
            logger.LogDebug("Deleting multiple documents from collection {CollectionName} on server {ServerName}", collectionName, serverName);

            if (string.IsNullOrWhiteSpace(serverName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Server name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Collection name is required" }, _jsonOptions);
            }

            if (string.IsNullOrWhiteSpace(filterJson))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Filter JSON is required" }, _jsonOptions);
            }

            string result = await mongoService.DeleteManyAsync(serverName, collectionName, filterJson);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting documents from collection {CollectionName} on server {ServerName}", collectionName, serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
