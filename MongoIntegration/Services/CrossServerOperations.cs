using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MongoIntegration.Services;

public class CrossServerOperations
{
    private readonly ConnectionManager _connectionManager;
    private readonly ILogger<CrossServerOperations> _logger;

    public CrossServerOperations(ConnectionManager connectionManager, ILogger<CrossServerOperations> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task<string> CompareCollectionsAsync(string server1, string server2, string collectionName, string filterJson = "{}")
    {
        try
        {
            var db1 = _connectionManager.GetDatabase(server1);
            var db2 = _connectionManager.GetDatabase(server2);
            
            if (db1 == null || db2 == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "One or both servers are not connected",
                    server1Connected = db1 != null,
                    server2Connected = db2 != null
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var collection1 = db1.GetCollection<BsonDocument>(collectionName);
            var collection2 = db2.GetCollection<BsonDocument>(collectionName);
            
            var filter = BsonDocument.Parse(filterJson);
            
            // Get document counts
            var count1 = await collection1.CountDocumentsAsync(filter);
            var count2 = await collection2.CountDocumentsAsync(filter);
            
            // Sample documents for comparison
            var sample1 = await collection1.Find(filter).Limit(5).ToListAsync();
            var sample2 = await collection2.Find(filter).Limit(5).ToListAsync();
            
            return JsonSerializer.Serialize(new
            {
                comparison = new
                {
                    server1 = new { name = server1, documentCount = count1, sampleCount = sample1.Count },
                    server2 = new { name = server2, documentCount = count2, sampleCount = sample2.Count },
                    countDifference = count1 - count2,
                    percentageDifference = count2 > 0 ? Math.Round(((double)(count1 - count2) / count2) * 100, 2) : (count1 > 0 ? 100.0 : 0.0)
                },
                collectionName,
                filter = filterJson,
                samples = new
                {
                    server1 = sample1.Select(doc => BsonTypeMapper.MapToDotNetValue(doc)),
                    server2 = sample2.Select(doc => BsonTypeMapper.MapToDotNetValue(doc))
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing collections between {Server1} and {Server2}", server1, server2);
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "compare_collections",
                server1,
                server2,
                collectionName
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<string> SyncDataAsync(string sourceServer, string targetServer, string collectionName, string filterJson = "{}", bool dryRun = true)
    {
        try
        {
            var sourceDb = _connectionManager.GetDatabase(sourceServer);
            var targetDb = _connectionManager.GetDatabase(targetServer);
            
            if (sourceDb == null || targetDb == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "One or both servers are not connected",
                    sourceConnected = sourceDb != null,
                    targetConnected = targetDb != null
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var sourceCollection = sourceDb.GetCollection<BsonDocument>(collectionName);
            var targetCollection = targetDb.GetCollection<BsonDocument>(collectionName);
            
            var filter = BsonDocument.Parse(filterJson);
            
            // Get documents from source
            var sourceDocuments = await sourceCollection.Find(filter).ToListAsync();
            
            var insertOperations = 0;
            var updateOperations = 0;
            var operations = new List<Dictionary<string, object>>();
            
            foreach (var doc in sourceDocuments)
            {
                var id = doc.GetValue("_id");
                var existsInTarget = await targetCollection.CountDocumentsAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", id)) > 0;
                
                if (existsInTarget)
                {
                    operations.Add(new Dictionary<string, object> 
                    { 
                        ["operation"] = "update", 
                        ["_id"] = id.ToString() 
                    });
                    updateOperations++;
                    if (!dryRun)
                    {
                        await targetCollection.ReplaceOneAsync(
                            Builders<BsonDocument>.Filter.Eq("_id", id), doc);
                    }
                }
                else
                {
                    operations.Add(new Dictionary<string, object> 
                    { 
                        ["operation"] = "insert", 
                        ["_id"] = id.ToString() 
                    });
                    insertOperations++;
                    if (!dryRun)
                    {
                        await targetCollection.InsertOneAsync(doc);
                    }
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                sourceServer,
                targetServer,
                collectionName,
                dryRun,
                documentsProcessed = sourceDocuments.Count,
                operations = operations.Take(10), // Show first 10 operations
                totalOperations = operations.Count,
                operationsSummary = new Dictionary<string, int>
                {
                    ["insert"] = insertOperations,
                    ["update"] = updateOperations
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing data from {SourceServer} to {TargetServer}", sourceServer, targetServer);
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "sync_data",
                sourceServer,
                targetServer,
                collectionName
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<string> CrossServerQueryAsync(string[] serverNames, string collectionName, string filterJson = "{}", int limitPerServer = 50)
    {
        try
        {
            var results = new List<CrossServerQueryResult>();
            var filter = BsonDocument.Parse(filterJson);
            
            foreach (var serverName in serverNames)
            {
                try
                {
                    var db = _connectionManager.GetDatabase(serverName);
                    if (db == null)
                    {
                        results.Add(new CrossServerQueryResult
                        {
                            ServerName = serverName,
                            Error = "Server not connected",
                            Documents = [],
                            Count = 0,
                            Success = false
                        });
                        continue;
                    }

                    var collection = db.GetCollection<BsonDocument>(collectionName);
                    var documents = await collection.Find(filter).Limit(limitPerServer).ToListAsync();
                    
                    results.Add(new CrossServerQueryResult
                    {
                        ServerName = serverName,
                        Documents = documents.Select(doc => BsonTypeMapper.MapToDotNetValue(doc)),
                        Count = documents.Count,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new CrossServerQueryResult
                    {
                        ServerName = serverName,
                        Error = ex.Message,
                        Documents = [],
                        Count = 0,
                        Success = false
                    });
                }
            }
            
            var successfulQueries = results.Count(r => r.Success);
            var totalDocuments = results.Where(r => r.Success).Sum(r => r.Count);
            
            return JsonSerializer.Serialize(new
            {
                crossServerQuery = new
                {
                    servers = serverNames,
                    collectionName,
                    filter = filterJson,
                    limitPerServer,
                    totalServersQueried = serverNames.Length,
                    successfulQueries,
                    totalDocuments
                },
                results = results.Select(r => new
                {
                    serverName = r.ServerName,
                    documents = r.Documents,
                    count = r.Count,
                    success = r.Success,
                    error = r.Error
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing cross-server query");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "cross_server_query",
                servers = serverNames,
                collectionName
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<string> BulkTransferAsync(string sourceServer, string targetServer, string[] collectionNames, bool dryRun = true)
    {
        try
        {
            var sourceDb = _connectionManager.GetDatabase(sourceServer);
            var targetDb = _connectionManager.GetDatabase(targetServer);
            
            if (sourceDb == null || targetDb == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "One or both servers are not connected",
                    sourceConnected = sourceDb != null,
                    targetConnected = targetDb != null
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var transferResults = new List<BulkTransferResult>();
            
            foreach (var collectionName in collectionNames)
            {
                try
                {
                    var sourceCollection = sourceDb.GetCollection<BsonDocument>(collectionName);
                    var targetCollection = targetDb.GetCollection<BsonDocument>(collectionName);
                    
                    var sourceCount = await sourceCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
                    var targetCount = await targetCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
                    
                    if (!dryRun && sourceCount > 0)
                    {
                        var documents = await sourceCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
                        if (documents.Count > 0)
                        {
                            await targetCollection.InsertManyAsync(documents, new InsertManyOptions { IsOrdered = false });
                        }
                    }
                    
                    transferResults.Add(new BulkTransferResult
                    {
                        CollectionName = collectionName,
                        SourceDocuments = sourceCount,
                        TargetDocuments = targetCount,
                        Status = sourceCount > 0 ? (dryRun ? "ready_to_transfer" : "transferred") : "empty_collection",
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    transferResults.Add(new BulkTransferResult
                    {
                        CollectionName = collectionName,
                        Error = ex.Message,
                        Status = "failed",
                        Success = false
                    });
                }
            }
            
            var successfulTransfers = transferResults.Count(r => r.Success);
            var totalDocumentsToTransfer = transferResults
                .Where(r => r is { Success: true, SourceDocuments: not null })
                .Sum(r => r.SourceDocuments.Value);
            
            return JsonSerializer.Serialize(new
            {
                bulkTransfer = new
                {
                    sourceServer,
                    targetServer,
                    dryRun,
                    collectionsProcessed = collectionNames.Length,
                    successfulTransfers,
                    totalDocumentsToTransfer
                },
                results = transferResults.Select(r => new
                {
                    collectionName = r.CollectionName,
                    sourceDocuments = r.SourceDocuments,
                    targetDocuments = r.TargetDocuments,
                    status = r.Status,
                    success = r.Success,
                    error = r.Error
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk transfer from {SourceServer} to {TargetServer}", sourceServer, targetServer);
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "bulk_transfer",
                sourceServer,
                targetServer,
                collections = collectionNames
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<string> ExecuteOnAllServersAsync(string command)
    {
        try
        {
            var serverNames = _connectionManager.GetServerNames();
            var results = new List<object>();
            var commandDoc = BsonDocument.Parse(command);
            
            foreach (var serverName in serverNames)
            {
                try
                {
                    var db = _connectionManager.GetDatabase(serverName);
                    if (db == null)
                    {
                        results.Add(new
                        {
                            serverName,
                            error = "Server not connected",
                            result = (object?)null
                        });
                        continue;
                    }

                    var result = await db.RunCommandAsync<BsonDocument>(commandDoc);
                    results.Add(new
                    {
                        serverName,
                        error = (string?)null,
                        result = BsonTypeMapper.MapToDotNetValue(result)
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        serverName,
                        error = ex.Message,
                        result = (object?)null
                    });
                }
            }
            
            var successfulOperations = results.Count(r => 
            {
                var type = r.GetType();
                var errorProperty = type.GetProperty("error");
                var errorValue = errorProperty?.GetValue(r) as string;
                return string.IsNullOrEmpty(errorValue);
            });
            
            return JsonSerializer.Serialize(new
            {
                batchOperation = new
                {
                    command,
                    totalServers = serverNames.Count,
                    successfulOperations,
                    failedOperations = serverNames.Count - successfulOperations
                },
                results
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command on all servers");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "execute_on_all_servers",
                command
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<string> GetHealthDashboardAsync()
    {
        try
        {
            var serverNames = _connectionManager.GetServerNames();
            var healthResults = new List<object>();
            
            foreach (var serverName in serverNames)
            {
                try
                {
                    var isHealthy = await _connectionManager.PingConnectionAsync(serverName);
                    var info = _connectionManager.GetConnectionInfo(serverName);
                    
                    healthResults.Add(new
                    {
                        serverName,
                        isHealthy,
                        info?.DatabaseName,
                        info?.ConnectedAt,
                        info?.LastPing,
                        lastPingDurationMs = info?.LastPingDuration?.TotalMilliseconds,
                        status = isHealthy ? "healthy" : "unhealthy"
                    });
                }
                catch (Exception ex)
                {
                    healthResults.Add(new
                    {
                        serverName,
                        isHealthy = false,
                        error = ex.Message,
                        status = "error"
                    });
                }
            }
            
            var healthyServers = healthResults.Count(r => 
            {
                var type = r.GetType();
                var healthyProperty = type.GetProperty("isHealthy");
                var healthyValue = healthyProperty?.GetValue(r);
                return healthyValue is bool and true;
            });
            
            var totalServers = healthResults.Count;
            
            return JsonSerializer.Serialize(new
            {
                healthDashboard = new
                {
                    timestamp = DateTime.UtcNow,
                    totalServers,
                    healthyServers,
                    unhealthyServers = totalServers - healthyServers,
                    overallHealth = totalServers > 0 ? Math.Round((double)healthyServers / totalServers * 100, 1) : 0.0,
                    defaultServer = _connectionManager.GetDefaultServer()
                },
                servers = healthResults
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating health dashboard");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "health_dashboard"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

// Helper classes for type safety
public class CrossServerQueryResult
{
    public string ServerName { get; set; } = string.Empty;
    public string? Error { get; set; }
    public IEnumerable<object> Documents { get; set; } = [];
    public int Count { get; set; }
    public bool Success { get; set; }
}

public class BulkTransferResult
{
    public string CollectionName { get; set; } = string.Empty;
    public long? SourceDocuments { get; set; }
    public long? TargetDocuments { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public bool Success { get; set; }
}
