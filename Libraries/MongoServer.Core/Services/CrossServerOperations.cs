using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoServer.Core.Common;
using ConnectionInfo = MongoServer.Core.Configuration.ConnectionInfo;

namespace MongoServer.Core.Services;

public class CrossServerOperations(ConnectionManager connectionManager, ILogger<CrossServerOperations> logger)
{
    public async Task<string> CompareCollectionsAsync(string server1, string server2, string collectionName, string filterJson = "{}")
    {
        try
        {
            IMongoDatabase? db1 = connectionManager.GetDatabase(server1);
            IMongoDatabase? db2 = connectionManager.GetDatabase(server2);
            
            if (db1 == null || db2 == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "One or both servers are not connected",
                    server1Connected = db1 != null,
                    server2Connected = db2 != null
                }, SerializerOptions.JsonOptionsIndented);
            }

            IMongoCollection<BsonDocument>? collection1 = db1.GetCollection<BsonDocument>(collectionName);
            IMongoCollection<BsonDocument>? collection2 = db2.GetCollection<BsonDocument>(collectionName);
            
            BsonDocument? filter = BsonDocument.Parse(filterJson);
            
            // Get document counts
            long count1 = await collection1.CountDocumentsAsync(filter);
            long count2 = await collection2.CountDocumentsAsync(filter);
            
            // Sample documents for comparison
            List<BsonDocument>? sample1 = await collection1.Find(filter).Limit(5).ToListAsync();
            List<BsonDocument>? sample2 = await collection2.Find(filter).Limit(5).ToListAsync();
            
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
                    server1 = sample1.Select(BsonTypeMapper.MapToDotNetValue),
                    server2 = sample2.Select(BsonTypeMapper.MapToDotNetValue)
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error comparing collections between {Server1} and {Server2}", server1, server2);
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "compare_collections",
                server1,
                server2,
                collectionName
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    public async Task<string> SyncDataAsync(string sourceServer, string targetServer, string collectionName, string filterJson = "{}", bool dryRun = true)
    {
        try
        {
            IMongoDatabase? sourceDb = connectionManager.GetDatabase(sourceServer);
            IMongoDatabase? targetDb = connectionManager.GetDatabase(targetServer);
            
            if (sourceDb == null || targetDb == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "One or both servers are not connected",
                    sourceConnected = sourceDb != null,
                    targetConnected = targetDb != null
                }, SerializerOptions.JsonOptionsIndented);
            }

            IMongoCollection<BsonDocument>? sourceCollection = sourceDb.GetCollection<BsonDocument>(collectionName);
            IMongoCollection<BsonDocument>? targetCollection = targetDb.GetCollection<BsonDocument>(collectionName);
            
            BsonDocument? filter = BsonDocument.Parse(filterJson);
            
            // Get documents from source
            List<BsonDocument>? sourceDocuments = await sourceCollection.Find(filter).ToListAsync();
            
            var insertOperations = 0;
            var updateOperations = 0;
            var operations = new List<Dictionary<string, object>>();
            
            foreach (BsonDocument doc in sourceDocuments)
            {
                BsonValue? id = doc.GetValue("_id");
                bool existsInTarget = await targetCollection.CountDocumentsAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", id)) > 0;
                
                if (existsInTarget)
                {
                    operations.Add(new Dictionary<string, object> 
                    { 
                        ["operation"] = "update", 
                        ["_id"] = id.ToString() ?? string.Empty 
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
                        ["_id"] = id.ToString() ?? string.Empty 
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
                operations = operations.Take(10), // Show the first 10 operations
                totalOperations = operations.Count,
                operationsSummary = new Dictionary<string, int>
                {
                    ["insert"] = insertOperations,
                    ["update"] = updateOperations
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing data from {SourceServer} to {TargetServer}", sourceServer, targetServer);
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "sync_data",
                sourceServer,
                targetServer,
                collectionName
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    public async Task<string> CrossServerQueryAsync(string[] serverNames, string collectionName, string filterJson = "{}", int limitPerServer = 50)
    {
        try
        {
            var results = new List<CrossServerQueryResult>();
            BsonDocument? filter = BsonDocument.Parse(filterJson);
            
            foreach (string serverName in serverNames)
            {
                try
                {
                    IMongoDatabase? db = connectionManager.GetDatabase(serverName);
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

                    IMongoCollection<BsonDocument>? collection = db.GetCollection<BsonDocument>(collectionName);
                    List<BsonDocument>? documents = await collection.Find(filter).Limit(limitPerServer).ToListAsync();
                    
                    results.Add(new CrossServerQueryResult
                    {
                        ServerName = serverName,
                        Documents = documents.Select(BsonTypeMapper.MapToDotNetValue),
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
            
            int successfulQueries = results.Count(r => r.Success);
            int totalDocuments = results.Where(r => r.Success).Sum(r => r.Count);
            
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing cross-server query");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "cross_server_query",
                servers = serverNames,
                collectionName
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    public async Task<string> BulkTransferAsync(string sourceServer, string targetServer, string[] collectionNames, bool dryRun = true)
    {
        try
        {
            IMongoDatabase? sourceDb = connectionManager.GetDatabase(sourceServer);
            IMongoDatabase? targetDb = connectionManager.GetDatabase(targetServer);
            
            if (sourceDb == null || targetDb == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "One or both servers are not connected",
                    sourceConnected = sourceDb != null,
                    targetConnected = targetDb != null
                }, SerializerOptions.JsonOptionsIndented);
            }

            var transferResults = new List<BulkTransferResult>();
            
            foreach (string collectionName in collectionNames)
            {
                try
                {
                    IMongoCollection<BsonDocument>? sourceCollection = sourceDb.GetCollection<BsonDocument>(collectionName);
                    IMongoCollection<BsonDocument>? targetCollection = targetDb.GetCollection<BsonDocument>(collectionName);
                    
                    long sourceCount = await sourceCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
                    long targetCount = await targetCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
                    
                    if (!dryRun && sourceCount > 0)
                    {
                        List<BsonDocument>? documents = await sourceCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
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
            
            int successfulTransfers = transferResults.Count(r => r.Success);
            long totalDocumentsToTransfer = transferResults
                .Where(r => r is { Success: true, SourceDocuments: not null })
                .Sum(r => r.SourceDocuments ?? 0);
            
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during bulk transfer from {SourceServer} to {TargetServer}", sourceServer, targetServer);
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "bulk_transfer",
                sourceServer,
                targetServer,
                collections = collectionNames
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    public async Task<string> ExecuteOnAllServersAsync(string command)
    {
        try
        {
            List<string> serverNames = connectionManager.GetServerNames();
            var results = new List<object>();
            BsonDocument? commandDoc = BsonDocument.Parse(command);
            
            foreach (string serverName in serverNames)
            {
                try
                {
                    IMongoDatabase? db = connectionManager.GetDatabase(serverName);
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
            
            int successfulOperations = results.Count(r => 
            {
                Type type = r.GetType();
                PropertyInfo? errorProperty = type.GetProperty("error");
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command on all servers");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "execute_on_all_servers",
                command
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    public async Task<string> GetHealthDashboardAsync()
    {
        try
        {
            List<string> serverNames = connectionManager.GetServerNames();
            var healthResults = new List<object>();
            
            foreach (string serverName in serverNames)
            {
                try
                {
                    bool isHealthy = await connectionManager.PingConnectionAsync(serverName);
                    ConnectionInfo? info = connectionManager.GetConnectionInfo(serverName);
                    
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
            
            int healthyServers = healthResults.Count(r => 
            {
                Type type = r.GetType();
                PropertyInfo? healthyProperty = type.GetProperty("isHealthy");
                object? healthyValue = healthyProperty?.GetValue(r);
                return healthyValue is bool and true;
            });
            
            int totalServers = healthResults.Count;
            
            return JsonSerializer.Serialize(new
            {
                healthDashboard = new
                {
                    timestamp = DateTime.UtcNow,
                    totalServers,
                    healthyServers,
                    unhealthyServers = totalServers - healthyServers,
                    overallHealth = totalServers > 0 ? Math.Round((double)healthyServers / totalServers * 100, 1) : 0.0,
                    defaultServer = connectionManager.GetDefaultServer()
                },
                servers = healthResults
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating health dashboard");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                operation = "health_dashboard"
            }, SerializerOptions.JsonOptionsIndented);
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
