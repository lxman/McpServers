using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoIntegration.Configuration;
using MongoIntegration.Services;

namespace MongoIntegration;

public class MongoDbService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MongoDbService> _logger;
    private readonly MongoDbConfiguration _mongoConfig;
    private readonly ConnectionManager _connectionManager;

    // Backward compatibility fields - these will delegate to connection manager
    private const string DefaultServerName = "default";

    // Public property to access the connection manager
    public ConnectionManager ConnectionManager => _connectionManager;

    public MongoDbService(IConfiguration configuration, ILogger<MongoDbService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _mongoConfig = new MongoDbConfiguration();
        
        // Create connection manager with its own logger
        ILogger<ConnectionManager> connectionLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ConnectionManager>();
        _connectionManager = new ConnectionManager(connectionLogger);
        
        // DEBUG: Log configuration details
        string currentDir = Directory.GetCurrentDirectory();
        string baseDir = AppContext.BaseDirectory;
        
        // Create a debug file to bypass console output restrictions
        string debugPath = Path.Combine(baseDir, "debug-config.log");
        var debugInfo = new List<string>
        {
            $"=== MongoDB Configuration Debug - {DateTime.Now} ===",
            $"Current Directory: {currentDir}",
            $"Base Directory: {baseDir}",
            $"Configuration providers count: {_configuration.GetType().GetProperty("Providers")?.GetValue(_configuration)?.ToString() ?? "unknown"}",
            ""
        };

        // Check if MongoDB section exists
        IConfigurationSection mongoSection = _configuration.GetSection("MongoDB");
        debugInfo.Add($"MongoDB section exists: {mongoSection.Exists()}");
        debugInfo.Add($"MongoDB section path: {mongoSection.Path}");
        debugInfo.Add($"MongoDB section key: {mongoSection.Key}");
        
        // Get all keys in the section
        List<IConfigurationSection> children = mongoSection.GetChildren().ToList();
        debugInfo.Add($"MongoDB section children count: {children.Count}");
        
        foreach (IConfigurationSection child in children)
        {
            debugInfo.Add($"  Child Key: {child.Key}, Value: {child.Value}");
        }
        
        // Try binding and check results
        _configuration.GetSection("MongoDB").Bind(_mongoConfig);
        
        debugInfo.Add("");
        debugInfo.Add("=== After Binding ===");
        debugInfo.Add($"AutoConnect: {_mongoConfig.AutoConnect}");
        debugInfo.Add($"DefaultServer: {_mongoConfig.DefaultServer}");
        debugInfo.Add($"ConnectionString: '{_mongoConfig.ConnectionString}'");
        debugInfo.Add($"DefaultDatabase: '{_mongoConfig.DefaultDatabase}'");
        debugInfo.Add($"ConnectionProfiles count: {_mongoConfig.ConnectionProfiles?.Count ?? 0}");
        
        if (_mongoConfig.ConnectionProfiles != null)
        {
            for (var i = 0; i < _mongoConfig.ConnectionProfiles.Count; i++)
            {
                ConnectionProfile profile = _mongoConfig.ConnectionProfiles[i];
                debugInfo.Add($"  Profile {i}: Name='{profile.Name}', ConnectionString='{profile.ConnectionString}', Database='{profile.DefaultDatabase}', AutoConnect={profile.AutoConnect}");
            }
        }
        
        // Write debug info to file
        try
        {
            File.WriteAllLines(debugPath, debugInfo);
        }
        catch
        {
            // Ignore file write errors
        }
        
        // Try to auto-connect if configured - but don't log to console
        _ = Task.Run(TryAutoConnectAsync);
    }

    private async Task TryAutoConnectAsync()
    {
        try
        {
            var hasConnected = false;
            var connectTasks = new List<Task<string>>();
            
            // Try environment variables first (highest priority)
            string? envConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
            string? envDatabase = Environment.GetEnvironmentVariable("MONGODB_DATABASE");
            
            if (!string.IsNullOrEmpty(envConnectionString) && !string.IsNullOrEmpty(envDatabase))
            {
                _logger.LogInformation("Attempting auto-connect using environment variables");
                await ConnectToServerAsync(DefaultServerName, envConnectionString, envDatabase);
                _connectionManager.SetDefaultServer(DefaultServerName);
                hasConnected = true;
            }
            
            // Try profiles with AutoConnect enabled
            List<ConnectionProfile> autoConnectProfiles = _mongoConfig.ConnectionProfiles
                .Where(p => p.AutoConnect && 
                           !string.IsNullOrEmpty(p.ConnectionString) && 
                           !string.IsNullOrEmpty(p.DefaultDatabase))
                .ToList();
            
            foreach (ConnectionProfile profile in autoConnectProfiles)
            {
                try
                {
                    _logger.LogInformation("Attempting auto-connect to profile: {ProfileName}", profile.Name);
                    string serverName = profile.Name.Replace(" ", "_").ToLowerInvariant();
                    
                    // Don't overwrite the environment variable connection
                    if (serverName == DefaultServerName && hasConnected)
                        serverName = $"{profile.Name.Replace(" ", "_").ToLowerInvariant()}_profile";
                    
                    await ConnectToServerAsync(serverName, profile.ConnectionString, profile.DefaultDatabase);
                    
                    // Set the first successfully connected profile as default if no env connection
                    if (!hasConnected && !string.IsNullOrEmpty(_mongoConfig.DefaultServer))
                    {
                        if (profile.Name.Equals(_mongoConfig.DefaultServer, StringComparison.OrdinalIgnoreCase) ||
                            serverName.Equals(_mongoConfig.DefaultServer, StringComparison.OrdinalIgnoreCase))
                        {
                            _connectionManager.SetDefaultServer(serverName);
                            hasConnected = true;
                        }
                    }
                    else if (!hasConnected)
                    {
                        _connectionManager.SetDefaultServer(serverName);
                        hasConnected = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-connect to profile: {ProfileName}", profile.Name);
                }
            }
            
            // Fallback to configuration file if no profiles worked
            if (!hasConnected && _mongoConfig.AutoConnect && 
                !string.IsNullOrEmpty(_mongoConfig.ConnectionString) && 
                !string.IsNullOrEmpty(_mongoConfig.DefaultDatabase))
            {
                _logger.LogInformation("Attempting auto-connect using configuration file");
                await ConnectToServerAsync(DefaultServerName, _mongoConfig.ConnectionString, _mongoConfig.DefaultDatabase);
                _connectionManager.SetDefaultServer(DefaultServerName);
                hasConnected = true;
            }
            
            // Final fallback to first available profile
            if (!hasConnected)
            {
                ConnectionProfile? activeProfile = _mongoConfig.ConnectionProfiles
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p.ConnectionString) && !string.IsNullOrEmpty(p.DefaultDatabase));
                
                if (activeProfile != null)
                {
                    _logger.LogInformation("Attempting fallback auto-connect using profile: {ProfileName}", activeProfile.Name);
                    string serverName = activeProfile.Name.Replace(" ", "_").ToLowerInvariant();
                    await ConnectToServerAsync(serverName, activeProfile.ConnectionString, activeProfile.DefaultDatabase);
                    _connectionManager.SetDefaultServer(serverName);
                    hasConnected = true;
                }
            }
            
            if (!hasConnected)
            {
                _logger.LogInformation("No auto-connect configuration found or all connections failed");
            }
            else
            {
                _logger.LogInformation("Auto-connect completed. Active connections: {Count}", 
                    _connectionManager.GetServerNames().Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-connect process failed, manual connection will be required");
        }
    }

    #region New Multi-Server Methods

    public async Task<string> ConnectToServerAsync(string serverName, string connectionString, string databaseName)
    {
        return await _connectionManager.AddConnectionAsync(serverName, connectionString, databaseName);
    }

    public string DisconnectFromServer(string serverName)
    {
        bool result = _connectionManager.RemoveConnection(serverName);
        return result ? $"Disconnected from server '{serverName}'" : $"Failed to disconnect from server '{serverName}'";
    }

    public string ListActiveConnections()
    {
        return _connectionManager.GetConnectionsStatus();
    }

    public string SetDefaultServer(string serverName)
    {
        List<string> serverNames = _connectionManager.GetServerNames();
        if (!serverNames.Contains(serverName))
        {
            return $"Server '{serverName}' is not connected. Available servers: {string.Join(", ", serverNames)}";
        }

        _connectionManager.SetDefaultServer(serverName);
        return $"Default server set to '{serverName}'";
    }

    public string GetServerConnectionStatus(string serverName)
    {
        ConnectionInfo? info = _connectionManager.GetConnectionInfo(serverName);
        if (info == null)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                connected = false,
                message = "Server not found"
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        return info.ToJson();
    }

    public async Task<string> PingServerAsync(string serverName)
    {
        bool result = await _connectionManager.PingConnectionAsync(serverName);
        ConnectionInfo? info = _connectionManager.GetConnectionInfo(serverName);
        
        return JsonSerializer.Serialize(new
        {
            serverName,
            pingSuccessful = result,
            isHealthy = info?.IsHealthy ?? false,
            lastPingDuration = info?.LastPingDuration?.TotalMilliseconds,
            lastPing = info?.LastPing
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private IMongoDatabase GetDatabase(string serverName = DefaultServerName)
    {
        IMongoDatabase? database = _connectionManager.GetDatabase(serverName);
        if (database == null)
        {
            List<string> availableServers = _connectionManager.GetServerNames();
            if (availableServers.Count == 0)
            {
                throw new InvalidOperationException("Not connected to any MongoDB servers. Use ConnectToServer command first.");
            }
            
            throw new InvalidOperationException($"Server '{serverName}' is not connected. Available servers: {string.Join(", ", availableServers)}");
        }
        return database;
    }

    #endregion

    #region NEW: Database Management Methods

    public async Task<string> ListDatabasesAsync(string serverName = DefaultServerName)
    {
        try
        {
            List<string> databases = await _connectionManager.ListDatabasesAsync(serverName);
            
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = true,
                currentDatabase = _connectionManager.GetCurrentDatabase(serverName),
                totalDatabases = databases.Count,
                databases = databases.OrderBy(db => db).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = false,
                error = ex.Message,
                suggestion = $"Ensure server '{serverName}' is connected and accessible"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<string> SwitchDatabaseAsync(string serverName, string databaseName)
    {
        try
        {
            string result = await _connectionManager.SwitchDatabaseAsync(serverName, databaseName);
            
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = true,
                message = result,
                currentDatabase = databaseName,
                nextSteps = "You can now run operations like list_collections, query, etc. on this database"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = false,
                error = ex.Message,
                suggestion = "Use list_databases to see available databases on this server"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public string GetCurrentDatabaseInfo(string serverName = DefaultServerName)
    {
        try
        {
            ConnectionInfo? info = _connectionManager.GetConnectionInfo(serverName);
            string? currentDb = _connectionManager.GetCurrentDatabase(serverName);
            
            if (info == null)
            {
                return JsonSerializer.Serialize(new
                {
                    serverName,
                    success = false,
                    error = "Server not connected"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            return JsonSerializer.Serialize(new
            {
                serverName,
                success = true,
                currentDatabase = currentDb,
                originalDatabase = info.DatabaseName,
                connectionInfo = new
                {
                    info.ConnectedAt,
                    info.IsHealthy,
                    info.LastPing,
                    LastPingMs = info.LastPingDuration?.TotalMilliseconds
                },
                capabilities = new
                {
                    canSwitchDatabases = true,
                    canListDatabases = true,
                    supportsMultiDatabase = true
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    // Enhanced method that works with any database on a connected server
    public async Task<string> QueryDatabaseAsync(string serverName, string databaseName, string collectionName, string filterJson, int limit = 100, int skip = 0)
    {
        try
        {
            IMongoDatabase? database = _connectionManager.GetDatabase(serverName, databaseName);
            if (database == null)
            {
                throw new InvalidOperationException($"Cannot access database '{databaseName}' on server '{serverName}'. Server may not be connected.");
            }

            IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(collectionName);
            
            FilterDefinition<BsonDocument> filter = string.IsNullOrEmpty(filterJson) ? 
                FilterDefinition<BsonDocument>.Empty : 
                BsonDocument.Parse(filterJson);
            
            List<BsonDocument> results = await collection.Find(filter)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
            
            return JsonSerializer.Serialize(new 
            { 
                serverName,
                databaseName,
                collection = collectionName,
                count = results.Count,
                documents = results.Select(doc => BsonTypeMapper.MapToDotNetValue(doc))
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                databaseName,
                success = false,
                error = ex.Message,
                suggestion = "Verify server connection and database/collection names"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<string> ListCollectionsByDatabaseAsync(string serverName, string databaseName)
    {
        try
        {
            IMongoDatabase? database = _connectionManager.GetDatabase(serverName, databaseName);
            if (database == null)
            {
                throw new InvalidOperationException($"Cannot access database '{databaseName}' on server '{serverName}'. Server may not be connected.");
            }

            IAsyncCursor<string> collections = await database.ListCollectionNamesAsync();
            List<string> collectionList = await collections.ToListAsync();
            
            return JsonSerializer.Serialize(new { 
                serverName,
                databaseName,
                collections = collectionList.OrderBy(c => c).ToList(),
                totalCollections = collectionList.Count
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                databaseName,
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region Backward Compatible Methods (now with optional serverName parameter)

    public async Task<string> ConnectAsync(string connectionString, string databaseName)
    {
        return await ConnectToServerAsync(DefaultServerName, connectionString, databaseName);
    }

    public string Disconnect()
    {
        return DisconnectFromServer(DefaultServerName);
    }

    public string GetConnectionStatus()
    {
        ConnectionInfo? info = _connectionManager.GetConnectionInfo(DefaultServerName);
        if (info == null)
            return "Not connected to MongoDB";
        
        return $"Connected to database '{info.DatabaseName}' at '{info.ConnectionString}'";
    }

    private IMongoDatabase EnsureConnected()
    {
        return GetDatabase(DefaultServerName);
    }

    public async Task<string> ListCollectionsAsync(string serverName = DefaultServerName)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IAsyncCursor<string>? collections = await db.ListCollectionNamesAsync();
        List<string>? collectionList = await collections.ToListAsync();
        
        return JsonSerializer.Serialize(new { 
            serverName,
            collections = collectionList 
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> QueryAsync(string serverName, string collectionName, string filterJson, int limit = 100, int skip = 0)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        FilterDefinition<BsonDocument>? filter = string.IsNullOrEmpty(filterJson) ? 
            FilterDefinition<BsonDocument>.Empty : 
            BsonDocument.Parse(filterJson);
        
        List<BsonDocument>? results = await collection.Find(filter)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            collection = collectionName,
            count = results.Count,
            documents = results.Select(doc => BsonTypeMapper.MapToDotNetValue(doc))
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> QueryAsync(string collectionName, string filterJson, int limit = 100, int skip = 0)
    {
        return await QueryAsync(DefaultServerName, collectionName, filterJson, limit, skip);
    }

    public async Task<string> InsertOneAsync(string serverName, string collectionName, string documentJson)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        BsonDocument? document = BsonDocument.Parse(documentJson);
        await collection.InsertOneAsync(document);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true, 
            message = "Document inserted successfully",
            insertedId = document.GetValue("_id", BsonNull.Value).ToString()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> InsertOneAsync(string collectionName, string documentJson)
    {
        return await InsertOneAsync(DefaultServerName, collectionName, documentJson);
    }

    public async Task<string> InsertManyAsync(string serverName, string collectionName, string documentsJson)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        var documentsArray = BsonSerializer.Deserialize<BsonArray>(documentsJson);
        List<BsonDocument> documents = documentsArray.Select(doc => doc.AsBsonDocument).ToList();
        
        await collection.InsertManyAsync(documents);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true, 
            message = $"Inserted {documents.Count} documents successfully"
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> InsertManyAsync(string collectionName, string documentsJson)
    {
        return await InsertManyAsync(DefaultServerName, collectionName, documentsJson);
    }

    public async Task<string> UpdateOneAsync(string serverName, string collectionName, string filterJson, string updateJson)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        BsonDocument? filter = BsonDocument.Parse(filterJson);
        BsonDocument? update = BsonDocument.Parse(updateJson);
        
        UpdateResult? result = await collection.UpdateOneAsync(filter, update);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            matchedCount = result.MatchedCount,
            modifiedCount = result.ModifiedCount,
            upsertedId = result.UpsertedId?.ToString()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> UpdateOneAsync(string collectionName, string filterJson, string updateJson)
    {
        return await UpdateOneAsync(DefaultServerName, collectionName, filterJson, updateJson);
    }

    public async Task<string> UpdateManyAsync(string serverName, string collectionName, string filterJson, string updateJson)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        BsonDocument? filter = BsonDocument.Parse(filterJson);
        BsonDocument? update = BsonDocument.Parse(updateJson);
        
        UpdateResult? result = await collection.UpdateManyAsync(filter, update);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            matchedCount = result.MatchedCount,
            modifiedCount = result.ModifiedCount
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> UpdateManyAsync(string collectionName, string filterJson, string updateJson)
    {
        return await UpdateManyAsync(DefaultServerName, collectionName, filterJson, updateJson);
    }

    public async Task<string> DeleteOneAsync(string serverName, string collectionName, string filterJson)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        BsonDocument? filter = BsonDocument.Parse(filterJson);
        DeleteResult? result = await collection.DeleteOneAsync(filter);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            deletedCount = result.DeletedCount
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> DeleteOneAsync(string collectionName, string filterJson)
    {
        return await DeleteOneAsync(DefaultServerName, collectionName, filterJson);
    }

    public async Task<string> DeleteManyAsync(string serverName, string collectionName, string filterJson)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        BsonDocument? filter = BsonDocument.Parse(filterJson);
        DeleteResult? result = await collection.DeleteManyAsync(filter);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            deletedCount = result.DeletedCount
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> DeleteManyAsync(string collectionName, string filterJson)
    {
        return await DeleteManyAsync(DefaultServerName, collectionName, filterJson);
    }

    public async Task<string> AggregateAsync(string serverName, string collectionName, string pipelineJson)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        var pipelineArray = BsonSerializer.Deserialize<BsonArray>(pipelineJson);
        BsonDocument[] pipeline = pipelineArray.Select(stage => stage.AsBsonDocument).ToArray();
        
        List<BsonDocument>? results = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            collection = collectionName,
            stages = pipeline.Length,
            results = results.Select(doc => BsonTypeMapper.MapToDotNetValue(doc))
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> AggregateAsync(string collectionName, string pipelineJson)
    {
        return await AggregateAsync(DefaultServerName, collectionName, pipelineJson);
    }

    public async Task<string> CountDocumentsAsync(string serverName, string collectionName, string filterJson = "{}")
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        BsonDocument? filter = BsonDocument.Parse(filterJson);
        long count = await collection.CountDocumentsAsync(filter);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            collection = collectionName,
            count = count
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> CountDocumentsAsync(string collectionName, string filterJson = "{}")
    {
        return await CountDocumentsAsync(DefaultServerName, collectionName, filterJson);
    }

    public async Task<string> CreateIndexAsync(string serverName, string collectionName, string indexJson, string? indexName = null)
    {
        IMongoDatabase db = GetDatabase(serverName);
        IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
        
        BsonDocument? indexKeys = BsonDocument.Parse(indexJson);
        var indexModel = new CreateIndexModel<BsonDocument>(indexKeys, new CreateIndexOptions 
        { 
            Name = indexName 
        });
        
        string? result = await collection.Indexes.CreateOneAsync(indexModel);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            indexName = result
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> CreateIndexAsync(string collectionName, string indexJson, string? indexName = null)
    {
        return await CreateIndexAsync(DefaultServerName, collectionName, indexJson, indexName);
    }

    public async Task<string> DropCollectionAsync(string serverName, string collectionName)
    {
        IMongoDatabase db = GetDatabase(serverName);
        await db.DropCollectionAsync(collectionName);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            message = $"Collection '{collectionName}' dropped successfully"
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> DropCollectionAsync(string collectionName)
    {
        return await DropCollectionAsync(DefaultServerName, collectionName);
    }

    public async Task<string> ExecuteCommandAsync(string serverName, string command)
    {
        IMongoDatabase db = GetDatabase(serverName);
        BsonDocument? commandDoc = BsonDocument.Parse(command);
        var result = await db.RunCommandAsync<BsonDocument>(commandDoc);
        
        return JsonSerializer.Serialize(new
        {
            serverName,
            result = BsonTypeMapper.MapToDotNetValue(result)
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // Backward compatible overload
    public async Task<string> ExecuteCommandAsync(string command)
    {
        return await ExecuteCommandAsync(DefaultServerName, command);
    }

    // NOTE: GetServerInfoAsync method removed due to BSON serialization bug
    // Use execute_command with {"buildInfo": 1} or {"hello": 1} instead

    public string ListConnectionProfiles()
    {
        // DEBUG: Add detailed debugging
        string debugPath = Path.Combine(AppContext.BaseDirectory, "debug-profiles.log");
        var debugInfo = new List<string>
        {
            $"=== ListConnectionProfiles Debug - {DateTime.Now} ===",
            $"_mongoConfig is null: {_mongoConfig == null}",
            $"_mongoConfig.ConnectionProfiles is null: {_mongoConfig?.ConnectionProfiles == null}",
            $"_mongoConfig.ConnectionProfiles count: {_mongoConfig?.ConnectionProfiles?.Count ?? -1}",
            ""
        };

        if (_mongoConfig?.ConnectionProfiles != null)
        {
            debugInfo.Add("Raw profiles from _mongoConfig:");
            for (var i = 0; i < _mongoConfig.ConnectionProfiles.Count; i++)
            {
                ConnectionProfile? p = _mongoConfig.ConnectionProfiles[i];
                debugInfo.Add($"  Profile {i}: Name='{p?.Name}', ConnectionString='{p?.ConnectionString}', DefaultDatabase='{p?.DefaultDatabase}', Description='{p?.Description}'");
            }
        }

        IEnumerable<object> profiles = _mongoConfig?.ConnectionProfiles?.Select(p => new
        {
            p.Name,
            p.Description,
            HasConnectionString = !string.IsNullOrEmpty(p.ConnectionString),
            p.DefaultDatabase,
            p.AutoConnect
        }) ?? Enumerable.Empty<object>();
        
        List<object> profilesList = profiles.ToList();
        debugInfo.Add($"");
        debugInfo.Add($"Processed profiles count: {profilesList.Count}");
        
        // Include current connections from connection manager
        var currentConnections = _connectionManager.GetServerNames().Select(name =>
        {
            ConnectionInfo? info = _connectionManager.GetConnectionInfo(name);
            return new
            {
                ServerName = name,
                DatabaseName = info?.DatabaseName,
                IsConnected = info?.IsHealthy ?? false,
                ConnectedAt = info?.ConnectedAt,
                IsDefault = name == _connectionManager.GetDefaultServer()
            };
        }).ToList();
        
        string result = JsonSerializer.Serialize(new 
        { 
            profiles = profilesList,
            currentConnections = currentConnections,
            defaultServer = _connectionManager.GetDefaultServer(),
            configDefaultServer = _mongoConfig?.DefaultServer
        }, new JsonSerializerOptions { WriteIndented = true });
        
        debugInfo.Add($"Final JSON result: {result}");
        
        try
        {
            File.WriteAllLines(debugPath, debugInfo);
        }
        catch
        {
            // Ignore file write errors
        }
        
        return result;
    }

    public async Task<string> ConnectWithProfileAsync(string profileName)
    {
        ConnectionProfile? profile = _mongoConfig.ConnectionProfiles
            .FirstOrDefault(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        
        if (profile == null)
            return $"Profile '{profileName}' not found";
        
        if (string.IsNullOrEmpty(profile.ConnectionString) || string.IsNullOrEmpty(profile.DefaultDatabase))
            return $"Profile '{profileName}' is incomplete (missing connection string or database)";
        
        // Connect using the profile name as the server name
        string serverName = profile.Name.Replace(" ", "_").ToLowerInvariant();
        return await ConnectToServerAsync(serverName, profile.ConnectionString, profile.DefaultDatabase);
    }

    public string GetAutoConnectStatus()
    {
        string? envConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
        string? envDatabase = Environment.GetEnvironmentVariable("MONGODB_DATABASE");
        
        return JsonSerializer.Serialize(new 
        {
            currentConnections = _connectionManager.GetConnectionsStatus(),
            autoConnectEnabled = _mongoConfig.AutoConnect,
            defaultServer = _mongoConfig.DefaultServer,
            environmentVariables = new
            {
                hasConnectionString = !string.IsNullOrEmpty(envConnectionString),
                hasDatabase = !string.IsNullOrEmpty(envDatabase)
            },
            configFile = new
            {
                hasConnectionString = !string.IsNullOrEmpty(_mongoConfig.ConnectionString),
                hasDatabase = !string.IsNullOrEmpty(_mongoConfig.DefaultDatabase)
            },
            availableProfiles = _mongoConfig.ConnectionProfiles.Count,
            autoConnectProfiles = _mongoConfig.ConnectionProfiles.Count(p => p.AutoConnect),
            features = _mongoConfig.Features
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    #endregion

    public void Dispose()
    {
        _connectionManager?.Dispose();
    }
}
