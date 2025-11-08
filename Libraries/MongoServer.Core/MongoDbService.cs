using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoServer.Core.Common;
using MongoServer.Core.Configuration;
using MongoServer.Core.Services;
using ConnectionInfo = MongoServer.Core.Configuration.ConnectionInfo;

namespace MongoServer.Core;

public class MongoDbService
{
    private readonly ILogger<MongoDbService> _logger;
    private readonly MongoDbConfiguration _mongoConfig;

    private const string DEFAULT_SERVER_NAME = "default";

    public ConnectionManager ConnectionManager { get; }

    public MongoDbService(IConfiguration configuration, ILogger<MongoDbService> logger)
    {
        _logger = logger;
        _mongoConfig = new MongoDbConfiguration();
        
        // Create a connection manager with its own logger
        var connectionLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ConnectionManager>();
        ConnectionManager = new ConnectionManager(connectionLogger);
        
        // DEBUG: Log configuration details
        var currentDir = Directory.GetCurrentDirectory();
        var baseDir = AppContext.BaseDirectory;
        
        // Create a debug file to bypass console output restrictions
        var debugPath = Path.Combine(baseDir, "debug-config.log");
        var debugInfo = new List<string>
        {
            $"=== MongoDB Configuration Debug - {DateTime.Now} ===",
            $"Current Directory: {currentDir}",
            $"Base Directory: {baseDir}",
            $"Configuration providers count: {configuration.GetType().GetProperty("Providers")?.GetValue(configuration)?.ToString() ?? "unknown"}",
            ""
        };

        // Check if the MongoDB section exists
        var mongoSection = configuration.GetSection("MongoDB");
        debugInfo.Add($"MongoDB section exists: {mongoSection.Exists()}");
        debugInfo.Add($"MongoDB section path: {mongoSection.Path}");
        debugInfo.Add($"MongoDB section key: {mongoSection.Key}");
        
        // Get all keys in the section
        var children = mongoSection.GetChildren().ToList();
        debugInfo.Add($"MongoDB section children count: {children.Count}");

        debugInfo.AddRange(children.Select(child => $"  Child Key: {child.Key}, Value: {child.Value}"));

        // Try binding and check results
        configuration.GetSection("MongoDB").Bind(_mongoConfig);
        
        debugInfo.Add("");
        debugInfo.Add("=== After Binding ===");
        debugInfo.Add($"AutoConnect: {_mongoConfig.AutoConnect}");
        debugInfo.Add($"DefaultServer: {_mongoConfig.DefaultServer}");
        debugInfo.Add($"ConnectionString: '{_mongoConfig.ConnectionString}'");
        debugInfo.Add($"DefaultDatabase: '{_mongoConfig.DefaultDatabase}'");
        debugInfo.Add($"ConnectionProfiles count: {_mongoConfig.ConnectionProfiles?.Count ?? 0}");
        
        if (_mongoConfig.ConnectionProfiles != null)
        {
            debugInfo.AddRange(_mongoConfig.ConnectionProfiles.Select((profile, i) => $"  Profile {i}: Name='{profile.Name}', ConnectionString='{profile.ConnectionString}', Database='{profile.DefaultDatabase}', AutoConnect={profile.AutoConnect}"));
        }
        
        // Write debug info to a file
        try
        {
            File.WriteAllLines(debugPath, debugInfo);
        }
        catch
        {
            // Ignore file write errors
        }
        
        // Try to auto-connect if configured - but don't log to the console
        _ = Task.Run(TryAutoConnectAsync);
    }

    private async Task TryAutoConnectAsync()
    {
        try
        {
            var hasConnected = false;
            var connectTasks = new List<Task<string>>();
            
            // Try environment variables first (the highest priority, with registry fallback)
            var envConnectionString = RegistryEnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_CONNECTION_STRING");
            var envDatabase = RegistryEnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_DATABASE");

            
            if (!string.IsNullOrEmpty(envConnectionString) && !string.IsNullOrEmpty(envDatabase))
            {
                _logger.LogInformation("Attempting auto-connect using environment variables");
                await ConnectToServerAsync(DEFAULT_SERVER_NAME, envConnectionString, envDatabase);
                ConnectionManager.SetDefaultServer(DEFAULT_SERVER_NAME);
                hasConnected = true;
            }
            
            // Try profiles with AutoConnect enabled
            var autoConnectProfiles = _mongoConfig.ConnectionProfiles
                .Where(p => p.AutoConnect && 
                           !string.IsNullOrEmpty(p.ConnectionString) && 
                           !string.IsNullOrEmpty(p.DefaultDatabase))
                .ToList();
            
            foreach (var profile in autoConnectProfiles)
            {
                try
                {
                    _logger.LogInformation("Attempting auto-connect to profile: {ProfileName}", profile.Name);
                    var serverName = profile.Name.Replace(" ", "_").ToLowerInvariant();
                    
                    // Don't overwrite the environment variable connection
                    if (serverName == DEFAULT_SERVER_NAME && hasConnected)
                        serverName = $"{profile.Name.Replace(" ", "_").ToLowerInvariant()}_profile";
                    
                    await ConnectToServerAsync(serverName, profile.ConnectionString, profile.DefaultDatabase);
                    
                    // Set the first successfully connected profile as default if no env connection
                    if (!hasConnected && !string.IsNullOrEmpty(_mongoConfig.DefaultServer))
                    {
                        if (profile.Name.Equals(_mongoConfig.DefaultServer, StringComparison.OrdinalIgnoreCase) ||
                            serverName.Equals(_mongoConfig.DefaultServer, StringComparison.OrdinalIgnoreCase))
                        {
                            ConnectionManager.SetDefaultServer(serverName);
                            hasConnected = true;
                        }
                    }
                    else if (!hasConnected)
                    {
                        ConnectionManager.SetDefaultServer(serverName);
                        hasConnected = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-connect to profile: {ProfileName}", profile.Name);
                }
            }
            
            // Fallback to the configuration file if no profiles worked
            if (!hasConnected && _mongoConfig.AutoConnect && 
                !string.IsNullOrEmpty(_mongoConfig.ConnectionString) && 
                !string.IsNullOrEmpty(_mongoConfig.DefaultDatabase))
            {
                _logger.LogInformation("Attempting auto-connect using configuration file");
                await ConnectToServerAsync(DEFAULT_SERVER_NAME, _mongoConfig.ConnectionString, _mongoConfig.DefaultDatabase);
                ConnectionManager.SetDefaultServer(DEFAULT_SERVER_NAME);
                hasConnected = true;
            }
            
            // Final fallback to the first available profile
            if (!hasConnected)
            {
                var activeProfile = _mongoConfig.ConnectionProfiles
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p.ConnectionString) && !string.IsNullOrEmpty(p.DefaultDatabase));
                
                if (activeProfile != null)
                {
                    _logger.LogInformation("Attempting fallback auto-connect using profile: {ProfileName}", activeProfile.Name);
                    var serverName = activeProfile.Name.Replace(" ", "_").ToLowerInvariant();
                    await ConnectToServerAsync(serverName, activeProfile.ConnectionString, activeProfile.DefaultDatabase);
                    ConnectionManager.SetDefaultServer(serverName);
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
                    ConnectionManager.GetServerNames().Count);
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
        return await ConnectionManager.AddConnectionAsync(serverName, connectionString, databaseName);
    }

    public string DisconnectFromServer(string serverName)
    {
        var result = ConnectionManager.RemoveConnection(serverName);
        return result ? $"Disconnected from server '{serverName}'" : $"Failed to disconnect from server '{serverName}'";
    }

    public string ListActiveConnections()
    {
        return ConnectionManager.GetConnectionsStatus();
    }

    public string SetDefaultServer(string serverName)
    {
        var serverNames = ConnectionManager.GetServerNames();
        if (!serverNames.Contains(serverName))
        {
            return $"Server '{serverName}' is not connected. Available servers: {string.Join(", ", serverNames)}";
        }

        ConnectionManager.SetDefaultServer(serverName);
        return $"Default server set to '{serverName}'";
    }

    public string GetServerConnectionStatus(string serverName)
    {
        var info = ConnectionManager.GetConnectionInfo(serverName);
        if (info == null)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                connected = false,
                message = "Server not found"
            }, SerializerOptions.JsonOptionsIndented);
        }

        return info.ToJson();
    }

    public async Task<string> PingServerAsync(string serverName)
    {
        var result = await ConnectionManager.PingConnectionAsync(serverName);
        var info = ConnectionManager.GetConnectionInfo(serverName);
        
        return JsonSerializer.Serialize(new
        {
            serverName,
            pingSuccessful = result,
            isHealthy = info?.IsHealthy ?? false,
            lastPingDuration = info?.LastPingDuration?.TotalMilliseconds,
            lastPing = info?.LastPing
        }, SerializerOptions.JsonOptionsIndented);
    }

    private IMongoDatabase GetDatabase(string serverName = DEFAULT_SERVER_NAME)
    {
        var database = ConnectionManager.GetDatabase(serverName);
        if (database == null)
        {
            var availableServers = ConnectionManager.GetServerNames();
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

    public async Task<string> ListDatabasesAsync(string serverName = DEFAULT_SERVER_NAME)
    {
        try
        {
            var databases = await ConnectionManager.ListDatabasesAsync(serverName);
            
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = true,
                currentDatabase = ConnectionManager.GetCurrentDatabase(serverName),
                totalDatabases = databases.Count,
                databases = databases.OrderBy(db => db).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = false,
                error = ex.Message,
                suggestion = $"Ensure server '{serverName}' is connected and accessible"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    public async Task<string> SwitchDatabaseAsync(string serverName, string databaseName)
    {
        try
        {
            var result = await ConnectionManager.SwitchDatabaseAsync(serverName, databaseName);
            
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = true,
                message = result,
                currentDatabase = databaseName,
                nextSteps = "You can now run operations like list_collections, query, etc. on this database"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = false,
                error = ex.Message,
                suggestion = "Use list_databases to see available databases on this server"
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    public string GetCurrentDatabaseInfo(string serverName = DEFAULT_SERVER_NAME)
    {
        try
        {
            var info = ConnectionManager.GetConnectionInfo(serverName);
            var currentDb = ConnectionManager.GetCurrentDatabase(serverName);
            
            if (info == null)
            {
                return JsonSerializer.Serialize(new
                {
                    serverName,
                    success = false,
                    error = "Server not connected"
                }, SerializerOptions.JsonOptionsIndented);
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    // Enhanced method that works with any database on a connected server
    public async Task<string> QueryDatabaseAsync(string serverName, string databaseName, string collectionName, string filterJson, int limit = 100, int skip = 0)
    {
        try
        {
            var database = ConnectionManager.GetDatabase(serverName, databaseName);
            if (database == null)
            {
                throw new InvalidOperationException($"Cannot access database '{databaseName}' on server '{serverName}'. Server may not be connected.");
            }

            var collection = database.GetCollection<BsonDocument>(collectionName);
            
            var filter = string.IsNullOrEmpty(filterJson) ? 
                FilterDefinition<BsonDocument>.Empty : 
                BsonDocument.Parse(filterJson);
            
            var results = await collection.Find(filter)
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
            }, SerializerOptions.JsonOptionsIndented);
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
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    public async Task<string> ListCollectionsByDatabaseAsync(string serverName, string databaseName)
    {
        try
        {
            var database = ConnectionManager.GetDatabase(serverName, databaseName);
            if (database == null)
            {
                throw new InvalidOperationException($"Cannot access database '{databaseName}' on server '{serverName}'. Server may not be connected.");
            }

            IAsyncCursor<string> collections = await database.ListCollectionNamesAsync();
            var collectionList = await collections.ToListAsync();
            
            return JsonSerializer.Serialize(new { 
                serverName,
                databaseName,
                collections = collectionList.OrderBy(c => c).ToList(),
                totalCollections = collectionList.Count
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                serverName,
                databaseName,
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Backward Compatible Methods (now with optional serverName parameter)

    public async Task<string> ConnectAsync(string connectionString, string databaseName)
    {
        return await ConnectToServerAsync(DEFAULT_SERVER_NAME, connectionString, databaseName);
    }

    public string Disconnect()
    {
        return DisconnectFromServer(DEFAULT_SERVER_NAME);
    }

    public string GetConnectionStatus()
    {
        var info = ConnectionManager.GetConnectionInfo(DEFAULT_SERVER_NAME);
        return info == null
            ? "Not connected to MongoDB"
            : $"Connected to database '{info.DatabaseName}' at '{info.ConnectionString}'";
    }

    private IMongoDatabase EnsureConnected()
    {
        return GetDatabase();
    }

    public async Task<string> ListCollectionsAsync(string serverName = DEFAULT_SERVER_NAME)
    {
        var db = GetDatabase(serverName);
        IAsyncCursor<string>? collections = await db.ListCollectionNamesAsync();
        var collectionList = await collections.ToListAsync();
        
        return JsonSerializer.Serialize(new { 
            serverName,
            collections = collectionList 
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> QueryAsync(string serverName, string collectionName, string filterJson, int limit = 100, int skip = 0)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var filter = string.IsNullOrEmpty(filterJson) ? 
            FilterDefinition<BsonDocument>.Empty : 
            BsonDocument.Parse(filterJson);
        
        var results = await collection.Find(filter)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            collection = collectionName,
            count = results.Count,
            documents = results.Select(doc => BsonTypeMapper.MapToDotNetValue(doc))
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> InsertOneAsync(string serverName, string collectionName, string documentJson)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var document = BsonDocument.Parse(documentJson);
        await collection.InsertOneAsync(document);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true, 
            message = "Document inserted successfully",
            insertedId = document.GetValue("_id", BsonNull.Value).ToString()
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> InsertManyAsync(string serverName, string collectionName, string documentsJson)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var documentsArray = BsonSerializer.Deserialize<BsonArray>(documentsJson);
        var documents = documentsArray.Select(doc => doc.AsBsonDocument).ToList();
        
        await collection.InsertManyAsync(documents);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true, 
            message = $"Inserted {documents.Count} documents successfully"
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> UpdateOneAsync(string serverName, string collectionName, string filterJson, string updateJson)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var filter = BsonDocument.Parse(filterJson);
        var update = BsonDocument.Parse(updateJson);
        
        var result = await collection.UpdateOneAsync(filter, update);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            matchedCount = result.MatchedCount,
            modifiedCount = result.ModifiedCount,
            upsertedId = result.UpsertedId?.ToString()
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> UpdateManyAsync(string serverName, string collectionName, string filterJson, string updateJson)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var filter = BsonDocument.Parse(filterJson);
        var update = BsonDocument.Parse(updateJson);
        
        var result = await collection.UpdateManyAsync(filter, update);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            matchedCount = result.MatchedCount,
            modifiedCount = result.ModifiedCount
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> DeleteOneAsync(string serverName, string collectionName, string filterJson)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var filter = BsonDocument.Parse(filterJson);
        var result = await collection.DeleteOneAsync(filter);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            deletedCount = result.DeletedCount
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> DeleteManyAsync(string serverName, string collectionName, string filterJson)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var filter = BsonDocument.Parse(filterJson);
        var result = await collection.DeleteManyAsync(filter);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            deletedCount = result.DeletedCount
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> AggregateAsync(string serverName, string collectionName, string pipelineJson)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var pipelineArray = BsonSerializer.Deserialize<BsonArray>(pipelineJson);
        var pipeline = pipelineArray.Select(stage => stage.AsBsonDocument).ToArray();
        
        var results = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            collection = collectionName,
            stages = pipeline.Length,
            results = results.Select(doc => BsonTypeMapper.MapToDotNetValue(doc))
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> CountDocumentsAsync(string serverName, string collectionName, string filterJson = "{}")
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var filter = BsonDocument.Parse(filterJson);
        var count = await collection.CountDocumentsAsync(filter);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            collection = collectionName,
            count
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> CreateIndexAsync(string serverName, string collectionName, string indexJson, string? indexName = null)
    {
        var db = GetDatabase(serverName);
        var collection = db.GetCollection<BsonDocument>(collectionName);
        
        var indexKeys = BsonDocument.Parse(indexJson);
        var indexModel = new CreateIndexModel<BsonDocument>(indexKeys, new CreateIndexOptions 
        { 
            Name = indexName 
        });
        
        var result = await collection.Indexes.CreateOneAsync(indexModel);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            indexName = result
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> DropCollectionAsync(string serverName, string collectionName)
    {
        var db = GetDatabase(serverName);
        await db.DropCollectionAsync(collectionName);
        
        return JsonSerializer.Serialize(new 
        { 
            serverName,
            success = true,
            message = $"Collection '{collectionName}' dropped successfully"
        }, SerializerOptions.JsonOptionsIndented);
    }

    public async Task<string> ExecuteCommandAsync(string serverName, string command)
    {
        var db = GetDatabase(serverName);
        var commandDoc = BsonDocument.Parse(command);
        var result = await db.RunCommandAsync<BsonDocument>(commandDoc);
        
        return JsonSerializer.Serialize(new
        {
            serverName,
            result = BsonTypeMapper.MapToDotNetValue(result)
        }, SerializerOptions.JsonOptionsIndented);
    }

    public string ListConnectionProfiles()
    {
        // DEBUG: Add detailed debugging
        var debugPath = Path.Combine(AppContext.BaseDirectory, "debug-profiles.log");
        var debugInfo = new List<string>
        {
            $"=== ListConnectionProfiles Debug - {DateTime.Now} ===",
            $"_mongoConfig is null: {false}",
            $"_mongoConfig.ConnectionProfiles is null: {_mongoConfig?.ConnectionProfiles == null}",
            $"_mongoConfig.ConnectionProfiles count: {_mongoConfig?.ConnectionProfiles?.Count ?? -1}",
            ""
        };

        if (_mongoConfig?.ConnectionProfiles != null)
        {
            debugInfo.Add("Raw profiles from _mongoConfig:");
            debugInfo.AddRange(_mongoConfig.ConnectionProfiles.Select((p, i) => $"  Profile {i}: Name='{p?.Name}', ConnectionString='{p?.ConnectionString}', DefaultDatabase='{p?.DefaultDatabase}', Description='{p?.Description}'"));
        }

        var profiles = _mongoConfig?.ConnectionProfiles?.Select(p => new
        {
            p.Name,
            p.Description,
            HasConnectionString = !string.IsNullOrEmpty(p.ConnectionString),
            p.DefaultDatabase,
            p.AutoConnect
        }) ?? Enumerable.Empty<object>();
        
        var profilesList = profiles.ToList();
        debugInfo.Add("");
        debugInfo.Add($"Processed profiles count: {profilesList.Count}");
        
        // Include current connections from connection manager
        var currentConnections = ConnectionManager.GetServerNames().Select(name =>
        {
            var info = ConnectionManager.GetConnectionInfo(name);
            return new
            {
                ServerName = name,
                info?.DatabaseName,
                IsConnected = info?.IsHealthy ?? false,
                info?.ConnectedAt,
                IsDefault = name == ConnectionManager.GetDefaultServer()
            };
        }).ToList();
        
        var result = JsonSerializer.Serialize(new 
        { 
            profiles = profilesList,
            currentConnections,
            defaultServer = ConnectionManager.GetDefaultServer(),
            configDefaultServer = _mongoConfig?.DefaultServer
        }, SerializerOptions.JsonOptionsIndented);
        
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
        var profile = _mongoConfig.ConnectionProfiles
            .FirstOrDefault(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        
        if (profile == null)
            return $"Profile '{profileName}' not found";
        
        if (string.IsNullOrEmpty(profile.ConnectionString) || string.IsNullOrEmpty(profile.DefaultDatabase))
            return $"Profile '{profileName}' is incomplete (missing connection string or database)";
        
        // Connect using the profile name as the server name
        var serverName = profile.Name.Replace(" ", "_").ToLowerInvariant();
        return await ConnectToServerAsync(serverName, profile.ConnectionString, profile.DefaultDatabase);
    }

    public string GetAutoConnectStatus()
    {
        var envConnectionString = RegistryEnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_CONNECTION_STRING");
        var envDatabase = RegistryEnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_DATABASE");

        
        return JsonSerializer.Serialize(new 
        {
            currentConnections = ConnectionManager.GetConnectionsStatus(),
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
        }, SerializerOptions.JsonOptionsIndented);
    }

    #endregion

    public void Dispose()
    {
        ConnectionManager?.Dispose();
    }
}