using System.ComponentModel;
using ModelContextProtocol.Server;
using MongoIntegration.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using MongoIntegration.Common;

namespace MongoIntegration;

[McpServerToolType]
public class MongoDbTools
{
    private readonly MongoDbService _mongoDbService;
    private readonly CrossServerOperations _crossServerOperations;

    public MongoDbTools(MongoDbService mongoDbService)
    {
        _mongoDbService = mongoDbService;
        
        // Create cross-server operations with logger
        ILogger<CrossServerOperations> logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<CrossServerOperations>();
        
        // Access the connection manager through the public property
        _crossServerOperations = new CrossServerOperations(_mongoDbService.ConnectionManager, logger);
    }

    #region Basic Connection Operations

    [McpServerTool]
    [Description("Connect to MongoDB as your primary database for CRUD operations. This establishes the main connection that supports insert, query, update, and delete operations.")]
    public async Task<string> ConnectPrimary(
        [Description("MongoDB connection string (e.g., 'mongodb://localhost:27017')")]
        string connectionString,
        [Description("Name of the database to connect to")]
        string databaseName)
    {
        try
        {
            string result = await _mongoDbService.ConnectAsync(connectionString, databaseName);
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = result,
                connectionType = "primary",
                supportsCrud = true,
                operatedOn = "default"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                connectionType = "primary",
                suggestion = "Verify your connection string format and network connectivity"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Connect to an additional MongoDB server for multi-database operations like comparisons or data migration. Use ConnectPrimary first for basic CRUD operations.")]
    public async Task<string> ConnectAdditional(
        [Description("Server name for this connection (e.g., 'local', 'production', 'staging')")]
        string serverName,
        [Description("MongoDB connection string (e.g., 'mongodb://localhost:27017')")]
        string connectionString,
        [Description("Name of the database to connect to")]
        string databaseName)
    {
        try
        {
            string result = await _mongoDbService.ConnectToServerAsync(serverName, connectionString, databaseName);
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = result,
                connectionType = "additional",
                serverName = serverName,
                supportsCrud = true,
                operatedOn = serverName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                connectionType = "additional",
                serverName = serverName,
                suggestion = "Verify connection string and ensure server name is unique"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Disconnect from the primary MongoDB connection.")]
    public string DisconnectPrimary()
    {
        try
        {
            string result = _mongoDbService.Disconnect();
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = result,
                operatedOn = "default"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operatedOn = "default"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Disconnect from a specific additional MongoDB server.")]
    public string DisconnectFromServer(
        [Description("Server name to disconnect from")]
        string serverName)
    {
        try
        {
            string result = _mongoDbService.DisconnectFromServer(serverName);
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = result,
                operatedOn = serverName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operatedOn = serverName
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Show comprehensive connection status including primary connection, all additional servers, and their CRUD capabilities.")]
    public string GetConnectionStatus()
    {
        try
        {
            string primaryStatus = _mongoDbService.GetConnectionStatus();
            string allConnections = _mongoDbService.ListActiveConnections();
            
            // Parse the connections JSON to enhance it
            var connectionsObj = JsonSerializer.Deserialize<dynamic>(allConnections);
            
            return JsonSerializer.Serialize(new
            {
                primaryConnection = new
                {
                    status = primaryStatus,
                    supportsCrud = primaryStatus != "Not connected to MongoDB",
                    serverName = "default"
                },
                allConnections = connectionsObj,
                summary = new
                {
                    totalConnections = _mongoDbService.ConnectionManager.GetServerNames().Count,
                    healthyConnections = _mongoDbService.ConnectionManager.GetServerNames()
                        .Count(name => _mongoDbService.ConnectionManager.IsConnected(name)),
                    supportsBasicCrud = primaryStatus != "Not connected to MongoDB",
                    supportsMultiServer = _mongoDbService.ConnectionManager.GetServerNames().Count > 1
                },
                nextSteps = primaryStatus == "Not connected to MongoDB" 
                    ? "Run mongodb:connect_primary to establish your main database connection for CRUD operations"
                    : "Connection ready for CRUD operations. Use mongodb:connect_additional for multi-server features."
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                suggestion = "Check connection manager status"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region NEW: Database Management Tools

    [McpServerTool]
    [Description("List all databases available on a connected MongoDB server.")]
    public async Task<string> ListDatabases(
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        try
        {
            return await _mongoDbService.ListDatabasesAsync(serverName);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "list_databases",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = $"Ensure server '{serverName}' is connected. Use mongodb:get_connection_status to check."
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Switch to a different database on the same MongoDB server. This allows you to browse multiple databases after connecting with a profile.")]
    public async Task<string> SwitchDatabase(
        [Description("Name of the database to switch to")]
        string databaseName,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        try
        {
            return await _mongoDbService.SwitchDatabaseAsync(serverName, databaseName);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "switch_database",
                error = ex.Message,
                operatedOn = serverName,
                targetDatabase = databaseName,
                suggestion = "Use mongodb:list_databases to see available databases on this server"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Get current database information and switching capabilities for a connected server.")]
    public string GetCurrentDatabaseInfo(
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        try
        {
            return _mongoDbService.GetCurrentDatabaseInfo(serverName);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "get_current_database_info",
                error = ex.Message,
                operatedOn = serverName
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Query a specific database and collection on a connected server (allows cross-database queries without switching).")]
    public async Task<string> QueryDatabase(
        [Description("Name of the database to query")]
        string databaseName,
        [Description("Name of the collection to query")]
        string collectionName,
        [Description("MongoDB filter in JSON format (e.g., '{\"name\": \"John\"}' or '{}' for all)")]
        string filter = "{}",
        [Description("Maximum number of documents to return (default: 100)")]
        int limit = 100,
        [Description("Number of documents to skip (default: 0)")]
        int skip = 0,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        try
        {
            return await _mongoDbService.QueryDatabaseAsync(serverName, databaseName, collectionName, filter, limit, skip);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "query_database",
                error = ex.Message,
                operatedOn = serverName,
                targetDatabase = databaseName,
                suggestion = "Verify server connection and use mongodb:list_databases to see available databases"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("List collections in a specific database on a connected server (allows browsing any database without switching).")]
    public async Task<string> ListCollectionsByDatabase(
        [Description("Name of the database to examine")]
        string databaseName,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        try
        {
            return await _mongoDbService.ListCollectionsByDatabaseAsync(serverName, databaseName);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "list_collections_by_database",
                error = ex.Message,
                operatedOn = serverName,
                targetDatabase = databaseName,
                suggestion = "Use mongodb:list_databases first to see available databases"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Connect with profile and immediately show available databases for browsing. This solves the 'database lock-in' issue.")]
    public async Task<string> ConnectWithProfileAndExplore(
        [Description("Name of the connection profile to use")]
        string profileName)
    {
        try
        {
            // First connect with the profile
            string connectResult = await _mongoDbService.ConnectWithProfileAsync(profileName);
            
            // Parse the connect result to check if it was successful
            var connectObj = JsonSerializer.Deserialize<JsonElement>(connectResult);
            bool wasSuccessful = !connectResult.Contains("not found") && !connectResult.Contains("incomplete");
            
            if (!wasSuccessful)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    operation = "connect_with_profile_and_explore",
                    error = connectResult,
                    profileName = profileName,
                    suggestion = "Check that the profile exists using mongodb:list_connection_profiles"
                }, SerializerOptions.JsonOptionsIndented);
            }
            
            // Get the server name that was connected
            string serverName = profileName.Replace(" ", "_").ToLowerInvariant();
            
            // Now list available databases
            string databasesResult = await _mongoDbService.ListDatabasesAsync(serverName);
            var databasesObj = JsonSerializer.Deserialize<JsonElement>(databasesResult);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "connect_with_profile_and_explore",
                profileName = profileName,
                serverName = serverName,
                connectionResult = connectResult,
                availableDatabases = databasesObj,
                nextSteps = new[]
                {
                    $"Use mongodb:switch_database to change to any database",
                    $"Use mongodb:list_collections_by_database to browse specific databases",
                    $"Use mongodb:query_database to query any database directly"
                },
                workflow = new
                {
                    switchDatabase = $"mongodb:switch_database \"target_database_name\" \"{serverName}\"",
                    browseDatabase = $"mongodb:list_collections_by_database \"database_name\" \"{serverName}\"",
                    queryAnyDatabase = $"mongodb:query_database \"database_name\" \"collection_name\" \"{{}}\" 10 0 \"{serverName}\""
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "connect_with_profile_and_explore",
                error = ex.Message,
                profileName = profileName,
                suggestion = "Check that the profile exists using mongodb:list_connection_profiles"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region CRUD Operations with Enhanced Error Handling

    private string ValidateDefaultConnection(string operation)
    {
        if (!_mongoDbService.ConnectionManager.IsConnected("default"))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"No primary connection established for {operation} operation",
                solution = "Run mongodb:connect_primary to establish your main database connection",
                operatedOn = "none",
                suggestion = "Primary connection is required for CRUD operations"
            }, SerializerOptions.JsonOptionsIndented);
        }
        return null; // Connection is valid
    }

    [McpServerTool]
    [Description("Query documents from a collection. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> Query(
        [Description("Name of the collection to query")]
        string collectionName,
        [Description("MongoDB filter in JSON format (e.g., '{\"name\": \"John\"}' or '{}' for all)")]
        string filter = "{}",
        [Description("Maximum number of documents to return (default: 100)")]
        int limit = 100,
        [Description("Number of documents to skip (default: 0)")]
        int skip = 0,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("query");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.QueryAsync(serverName, collectionName, filter, limit, skip);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "query",
                operatedOn = serverName,
                collection = collectionName,
                serverName = resultObj.GetProperty("serverName").GetString(),
                count = resultObj.GetProperty("count").GetInt32(),
                documents = resultObj.GetProperty("documents")
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "query",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = serverName == "default" 
                    ? "Ensure primary connection is established and healthy"
                    : $"Verify server '{serverName}' is connected and accessible"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Insert a single document into a collection. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> InsertOne(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Document to insert in JSON format (e.g., '{\"name\": \"John\", \"age\": 30}')")]
        string document,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("insert");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.InsertOneAsync(serverName, collectionName, document);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "insert_one",
                operatedOn = serverName,
                collection = collectionName,
                insertedId = resultObj.GetProperty("insertedId").GetString(),
                message = "Document inserted successfully"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "insert_one",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = serverName == "default"
                    ? "Ensure primary connection is established and document JSON is valid"
                    : $"Verify server '{serverName}' is connected and document JSON is valid"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Insert multiple documents into a collection. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> InsertMany(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Array of documents to insert in JSON format (e.g., '[{\"name\": \"John\"}, {\"name\": \"Jane\"}]')")]
        string documents,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("insert_many");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.InsertManyAsync(serverName, collectionName, documents);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "insert_many",
                operatedOn = serverName,
                collection = collectionName,
                message = resultObj.GetProperty("message").GetString()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "insert_many",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure documents is a valid JSON array"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Update a single document in a collection. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> UpdateOne(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Filter to match documents in JSON format (e.g., '{\"name\": \"John\"}')")]
        string filter,
        [Description("Update operations in JSON format (e.g., '{\"$set\": {\"age\": 31}}')")]
        string update,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("update_one");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.UpdateOneAsync(serverName, collectionName, filter, update);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "update_one",
                operatedOn = serverName,
                collection = collectionName,
                matchedCount = resultObj.GetProperty("matchedCount").GetInt64(),
                modifiedCount = resultObj.GetProperty("modifiedCount").GetInt64()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "update_one",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure filter/update JSON syntax is correct"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Update multiple documents in a collection. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> UpdateMany(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Filter to match documents in JSON format (e.g., '{\"status\": \"active\"}')")]
        string filter,
        [Description("Update operations in JSON format (e.g., '{\"$set\": {\"lastUpdated\": \"2024-01-01\"}}')")]
        string update,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("update_many");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.UpdateManyAsync(serverName, collectionName, filter, update);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "update_many",
                operatedOn = serverName,
                collection = collectionName,
                matchedCount = resultObj.GetProperty("matchedCount").GetInt64(),
                modifiedCount = resultObj.GetProperty("modifiedCount").GetInt64()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "update_many",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure filter/update JSON syntax is correct"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Delete a single document from a collection. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> DeleteOne(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Filter to match the document to delete in JSON format (e.g., '{\"_id\": \"507f1f77bcf86cd799439011\"}')")]
        string filter,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("delete_one");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.DeleteOneAsync(serverName, collectionName, filter);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "delete_one",
                operatedOn = serverName,
                collection = collectionName,
                deletedCount = resultObj.GetProperty("deletedCount").GetInt64()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "delete_one",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure filter JSON syntax is correct"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Delete multiple documents from a collection. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> DeleteMany(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Filter to match documents to delete in JSON format (e.g., '{\"status\": \"inactive\"}')")]
        string filter,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("delete_many");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.DeleteManyAsync(serverName, collectionName, filter);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "delete_many",
                operatedOn = serverName,
                collection = collectionName,
                deletedCount = resultObj.GetProperty("deletedCount").GetInt64()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "delete_many",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure filter JSON syntax is correct"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Run an aggregation pipeline on a collection for advanced data processing. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> Aggregate(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Aggregation pipeline stages in JSON array format (e.g., '[{\"$match\": {\"age\": {\"$gte\": 18}}}, {\"$group\": {\"_id\": \"$department\", \"count\": {\"$sum\": 1}}}]')")]
        string pipeline,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("aggregate");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.AggregateAsync(serverName, collectionName, pipeline);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "aggregate",
                operatedOn = serverName,
                collection = collectionName,
                stages = resultObj.GetProperty("stages").GetInt32(),
                results = resultObj.GetProperty("results")
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "aggregate",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure pipeline is a valid JSON array of aggregation stages"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Count documents in a collection, optionally with a filter. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> CountDocuments(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Optional filter in JSON format (e.g., '{\"status\": \"active\"}' or '{}' for all)")]
        string filter = "{}",
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("count_documents");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.CountDocumentsAsync(serverName, collectionName, filter);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "count_documents",
                operatedOn = serverName,
                collection = collectionName,
                count = resultObj.GetProperty("count").GetInt64()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "count_documents",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure filter JSON syntax is correct"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Administration Operations

    [McpServerTool]
    [Description("Create an index on a collection to improve query performance. Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> CreateIndex(
        [Description("Name of the collection")]
        string collectionName,
        [Description("Index specification in JSON format (e.g., '{\"name\": 1}' for ascending, '{\"email\": -1}' for descending)")]
        string indexKeys,
        [Description("Optional name for the index")]
        string? indexName = null,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("create_index");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.CreateIndexAsync(serverName, collectionName, indexKeys, indexName);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "create_index",
                operatedOn = serverName,
                collection = collectionName,
                indexName = resultObj.GetProperty("indexName").GetString()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "create_index",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure index specification is valid JSON"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Drop (delete) an entire collection and all its documents. Use with caution! Requires primary connection established via mongodb:connect_primary.")]
    public async Task<string> DropCollection(
        [Description("Name of the collection to drop")]
        string collectionName,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("drop_collection");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.DropCollectionAsync(serverName, collectionName);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "drop_collection",
                operatedOn = serverName,
                collection = collectionName,
                message = resultObj.GetProperty("message").GetString()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "drop_collection",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure collection name is correct"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("List all collections in a database. Works with primary connection or specified server.")]
    public async Task<string> ListCollections(
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("list_collections");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.ListCollectionsAsync(serverName);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "list_collections",
                operatedOn = serverName,
                collections = resultObj.GetProperty("collections")
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "list_collections",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection is established and healthy"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Execute a raw MongoDB command. For advanced users only. Requires established connection.")]
    public async Task<string> ExecuteCommand(
        [Description("MongoDB command in JSON format (e.g., '{\"ping\": 1}')")]
        string command,
        [Description("Server name (default: uses primary connection)")]
        string serverName = "default")
    {
        // Pre-operation validation
        if (serverName == "default")
        {
            string? validationError = ValidateDefaultConnection("execute_command");
            if (validationError != null) return validationError;
        }

        try
        {
            string result = await _mongoDbService.ExecuteCommandAsync(serverName, command);
            
            // Parse and enhance result with operation context
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "execute_command",
                operatedOn = serverName,
                result = resultObj.GetProperty("result")
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "execute_command",
                error = ex.Message,
                operatedOn = serverName,
                suggestion = "Verify connection and ensure command is valid MongoDB JSON"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Multi-Server Operations

    [McpServerTool]
    [Description("List all active server connections with their status and capabilities.")]
    public string ListServers()
    {
        try
        {
            string result = _mongoDbService.ListActiveConnections();
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "list_servers",
                totalConnections = resultObj.GetProperty("totalConnections").GetInt32(),
                healthyConnections = resultObj.GetProperty("healthyConnections").GetInt32(),
                defaultServer = resultObj.GetProperty("defaultServer").GetString(),
                connections = resultObj.GetProperty("connections")
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "list_servers",
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Set the default server for operations that don't specify a server name.")]
    public string SetDefaultServer(
        [Description("Server name to set as default")]
        string serverName)
    {
        try
        {
            string result = _mongoDbService.SetDefaultServer(serverName);
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "set_default_server",
                message = result,
                newDefault = serverName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "set_default_server",
                error = ex.Message,
                suggestion = "Verify the server name exists in your active connections"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Get detailed connection status for a specific server.")]
    public string GetServerStatus(
        [Description("Server name to check")]
        string serverName)
    {
        try
        {
            string result = _mongoDbService.GetServerConnectionStatus(serverName);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "get_server_status",
                serverName = serverName,
                details = resultObj
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "get_server_status",
                error = ex.Message,
                serverName = serverName
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Ping a specific server to test connectivity and measure response time.")]
    public async Task<string> PingServer(
        [Description("Server name to ping")]
        string serverName)
    {
        try
        {
            string result = await _mongoDbService.PingServerAsync(serverName);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                operation = "ping_server",
                serverName = serverName,
                pingSuccessful = resultObj.GetProperty("pingSuccessful").GetBoolean(),
                isHealthy = resultObj.GetProperty("isHealthy").GetBoolean(),
                responseTimeMs = resultObj.TryGetProperty("lastPingDuration", out JsonElement duration) 
                    ? (double?)duration.GetDouble() : null
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                operation = "ping_server",
                error = ex.Message,
                serverName = serverName
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Advanced Cross-Server Operations

    [McpServerTool]
    [Description("Compare collections between two servers to identify differences in document counts and structure.")]
    public async Task<string> CompareServers(
        [Description("First server name")]
        string server1,
        [Description("Second server name")]
        string server2,
        [Description("Collection name to compare")]
        string collectionName,
        [Description("Optional filter in JSON format (default: '{}' for all documents)")]
        string filter = "{}")
    {
        return await _crossServerOperations.CompareCollectionsAsync(server1, server2, collectionName, filter);
    }

    [McpServerTool]
    [Description("Synchronize data between two servers. Use dryRun=true to preview changes without applying them.")]
    public async Task<string> SyncCollections(
        [Description("Source server name")]
        string sourceServer,
        [Description("Target server name")]
        string targetServer,
        [Description("Collection name to synchronize")]
        string collectionName,
        [Description("Optional filter in JSON format (default: '{}' for all documents)")]
        string filter = "{}",
        [Description("Dry run mode - preview changes without applying them (default: true)")]
        bool dryRun = true)
    {
        return await _crossServerOperations.SyncDataAsync(sourceServer, targetServer, collectionName, filter, dryRun);
    }

    [McpServerTool]
    [Description("Execute a query across multiple servers simultaneously and aggregate results.")]
    public async Task<string> CrossServerQuery(
        [Description("Array of server names to query (JSON format, e.g., '[\"local\", \"production\"]')")]
        string serverNamesJson,
        [Description("Collection name to query")]
        string collectionName,
        [Description("Optional filter in JSON format (default: '{}' for all documents)")]
        string filter = "{}",
        [Description("Maximum documents per server (default: 50)")]
        int limitPerServer = 50)
    {
        try
        {
            string[]? serverNames = JsonSerializer.Deserialize<string[]>(serverNamesJson);
            if (serverNames == null || serverNames.Length == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid or empty server names array",
                    operation = "cross_server_query"
                }, SerializerOptions.JsonOptionsIndented);
            }
            
            return await _crossServerOperations.CrossServerQueryAsync(serverNames, collectionName, filter, limitPerServer);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Invalid JSON format for server names. Expected array like [\"server1\", \"server2\"]",
                operation = "cross_server_query"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Transfer multiple collections from one server to another. Use dryRun=true to preview the operation.")]
    public async Task<string> BulkTransfer(
        [Description("Source server name")]
        string sourceServer,
        [Description("Target server name")]
        string targetServer,
        [Description("Array of collection names to transfer (JSON format, e.g., '[\"users\", \"orders\"]')")]
        string collectionNamesJson,
        [Description("Dry run mode - preview transfer without executing (default: true)")]
        bool dryRun = true)
    {
        try
        {
            string[]? collectionNames = JsonSerializer.Deserialize<string[]>(collectionNamesJson);
            if (collectionNames == null || collectionNames.Length == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid or empty collection names array",
                    operation = "bulk_transfer"
                }, SerializerOptions.JsonOptionsIndented);
            }
            
            return await _crossServerOperations.BulkTransferAsync(sourceServer, targetServer, collectionNames, dryRun);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Invalid JSON format for collection names. Expected array like [\"collection1\", \"collection2\"]",
                operation = "bulk_transfer"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool]
    [Description("Execute a MongoDB command on all connected servers simultaneously.")]
    public async Task<string> BatchOperations(
        [Description("MongoDB command in JSON format (e.g., '{\"ping\": 1}')")]
        string command)
    {
        return await _crossServerOperations.ExecuteOnAllServersAsync(command);
    }

    [McpServerTool]
    [Description("Get comprehensive health status dashboard for all connected servers.")]
    public async Task<string> HealthDashboard()
    {
        return await _crossServerOperations.GetHealthDashboardAsync();
    }

    #endregion

    #region User Experience Tools

    [McpServerTool]
    [Description("Understand MongoDB tool capabilities and when to use different connection types.")]
    public string GetCapabilities()
    {
        return JsonSerializer.Serialize(new
        {
            connectionTypes = new
            {
                primary = new
                {
                    tool = "mongodb:connect_primary",
                    purpose = "Main database connection for day-to-day CRUD operations",
                    supports = new[] { "insert", "query", "update", "delete", "admin_operations" },
                    requirement = "Required for all basic database operations",
                    serverName = "default"
                },
                additional = new
                {
                    tool = "mongodb:connect_additional",
                    purpose = "Secondary connections for multi-database operations",
                    supports = new[] { "cross_server_queries", "data_migration", "comparisons" },
                    requirement = "Optional, used for advanced multi-server features",
                    serverName = "user_defined"
                }
            },
            operationGroups = new
            {
                basicCrud = new
                {
                    tools = new[] { "mongodb:insert_one", "mongodb:query", "mongodb:update_one", "mongodb:delete_many" },
                    requirement = "Primary connection required",
                    description = "Core database operations for managing documents"
                },
                administration = new
                {
                    tools = new[] { "mongodb:list_collections", "mongodb:create_index", "mongodb:drop_collection" },
                    requirement = "Primary connection required",
                    description = "Database schema and structure management"
                },
                multiServer = new
                {
                    tools = new[] { "mongodb:compare_servers", "mongodb:sync_collections", "mongodb:cross_server_query" },
                    requirement = "Multiple connections required",
                    description = "Advanced operations across multiple databases"
                },
                databaseManagement = new
                {
                    tools = new[] { "mongodb:list_databases", "mongodb:switch_database", "mongodb:query_database" },
                    requirement = "Single connection required",
                    description = "Browse and switch between databases on the same server"
                }
            },
            commonWorkflows = new
            {
                singleDatabase = new
                {
                    step1 = "Run mongodb:connect_primary with your connection string",
                    step2 = "Use any CRUD operation (insert, query, update, delete)",
                    step3 = "Check status with mongodb:get_connection_status"
                },
                multiDatabase = new
                {
                    step1 = "Run mongodb:connect_primary for your main database",
                    step2 = "Run mongodb:connect_additional for each additional server",
                    step3 = "Use cross-server operations like mongodb:compare_servers"
                },
                profileConnection = new
                {
                    step1 = "Run mongodb:connect_with_profile_and_explore to connect and see all databases",
                    step2 = "Use mongodb:switch_database to change databases or mongodb:query_database to query any database",
                    step3 = "Browse with mongodb:list_collections_by_database"
                }
            },
            troubleshooting = new
            {
                errorMessage = "No primary connection established",
                solution = "Run mongodb:connect_primary first",
                checkHealth = "Use mongodb:ping_server to test connectivity",
                viewStatus = "Use mongodb:get_connection_status for comprehensive info",
                databaseLockIn = "Use mongodb:connect_with_profile_and_explore to avoid being trapped in one database"
            }
        }, SerializerOptions.JsonOptionsIndented);
    }

    [McpServerTool]
    [Description("Get quick start workflows and examples for common MongoDB tasks.")]
    public string GetQuickStart()
    {
        return JsonSerializer.Serialize(new
        {
            gettingStarted = new
            {
                title = "MongoDB Integration Quick Start",
                firstTime = "If this is your first time, start with mongodb:connect_primary to establish your main database connection"
            },
            workflows = new
            {
                basicDatabaseWork = new
                {
                    description = "Working with a single database for CRUD operations",
                    steps = new[]
                    {
                        "mongodb:connect_primary \"mongodb://localhost:27017\" \"myapp\"",
                        "mongodb:list_collections",
                        "mongodb:insert_one \"users\" '{\"name\": \"John\", \"email\": \"john@example.com\"}'",
                        "mongodb:query \"users\" '{\"name\": \"John\"}'"
                    }
                },
                profileConnectionEnhanced = new
                {
                    description = "Connect with profile and explore all databases (SOLVES LOCK-IN ISSUE)",
                    steps = new[]
                    {
                        "mongodb:connect_with_profile_and_explore \"production\"",
                        "mongodb:list_databases \"production\"",
                        "mongodb:switch_database \"target_database\" \"production\"",
                        "mongodb:list_collections \"production\""
                    }
                },
                dataComparison = new
                {
                    description = "Compare data between development and production databases",
                    steps = new[]
                    {
                        "mongodb:connect_primary \"mongodb://localhost:27017\" \"myapp_dev\"",
                        "mongodb:connect_additional \"production\" \"mongodb://prod-server:27017\" \"myapp_prod\"",
                        "mongodb:compare_servers \"default\" \"production\" \"users\""
                    }
                },
                dataMigration = new
                {
                    description = "Migrate collections from one server to another",
                    steps = new[]
                    {
                        "Connect to both servers using connect_primary and connect_additional",
                        "mongodb:sync_collections \"source_server\" \"target_server\" \"users\" \"{}\" true # dry run first",
                        "mongodb:sync_collections \"source_server\" \"target_server\" \"users\" \"{}\" false # actual sync"
                    }
                }
            },
            commonCommands = new
            {
                connectionStatus = "mongodb:get_connection_status - Shows all connections and their capabilities",
                listServers = "mongodb:list_servers - View all active server connections",
                healthCheck = "mongodb:ping_server \"server_name\" - Test server connectivity",
                capabilities = "mongodb:get_capabilities - Understand what each tool does",
                browseDatabases = "mongodb:list_databases \"server_name\" - See all databases on a server",
                switchDatabase = "mongodb:switch_database \"database_name\" \"server_name\" - Change current database"
            },
            examples = new
            {
                queryWithFilter = "mongodb:query \"products\" '{\"price\": {\"$gte\": 100}}' 10",
                updateMultiple = "mongodb:update_many \"users\" '{\"status\": \"inactive\"}' '{\"$set\": {\"archived\": true}}'",
                createIndex = "mongodb:create_index \"users\" '{\"email\": 1}' \"email_index\"",
                aggregation = "mongodb:aggregate \"orders\" '[{\"$group\": {\"_id\": \"$status\", \"total\": {\"$sum\": \"$amount\"}}}]'",
                crossDatabaseQuery = "mongodb:query_database \"analytics\" \"events\" '{\"date\": {\"$gte\": \"2024-01-01\"}}' 50",
                browseSpecificDatabase = "mongodb:list_collections_by_database \"admin\" \"production\""
            },
            nextSteps = new
            {
                profiles = "Use mongodb:list_connection_profiles to see saved connection configurations",
                advanced = "Explore cross-server operations once you have multiple connections established",
                monitoring = "Use mongodb:health_dashboard for comprehensive server monitoring",
                databaseSwitching = "Use the new database management tools to browse multiple databases on the same server"
            }
        }, SerializerOptions.JsonOptionsIndented);
    }

    #endregion

    #region Connection Profiles and Configuration

    [McpServerTool]
    [Description("List all available connection profiles and show current connection status.")]
    public string ListConnectionProfiles()
    {
        return _mongoDbService.ListConnectionProfiles();
    }

    [McpServerTool]
    [Description("Connect using a predefined connection profile by name.")]
    public async Task<string> ConnectWithProfile(
        [Description("Name of the connection profile to use")]
        string profileName)
    {
        return await _mongoDbService.ConnectWithProfileAsync(profileName);
    }

    [McpServerTool]
    [Description("Get detailed status about auto-connect configuration and available connection methods.")]
    public string GetAutoConnectStatus()
    {
        return _mongoDbService.GetAutoConnectStatus();
    }

    #endregion
}
