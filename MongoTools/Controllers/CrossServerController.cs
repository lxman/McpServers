using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MongoTools.Services;

namespace MongoTools.Controllers;

/// <summary>
/// Manages cross-server MongoDB operations and synchronization
/// </summary>
[ApiController]
[Route("api/cross-server")]
[Tags("CrossServer")]
public class CrossServerController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly CrossServerOperations _crossServerOperations;
    private readonly ILogger<CrossServerController> _logger;

    public CrossServerController(MongoDbService mongoDbService, ILogger<CrossServerController> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
        
        // Create cross-server operations with logger
        ILogger<CrossServerOperations> crossServerLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<CrossServerOperations>();
        _crossServerOperations = new CrossServerOperations(_mongoDbService.ConnectionManager, crossServerLogger);
    }

    /// <summary>
    /// Ping a specific server to test connectivity and measure response time
    /// </summary>
    [HttpPost("ping")]
    public async Task<IActionResult> PingServer([FromBody] PingServerRequest request)
    {
        try
        {
            string result = await _mongoDbService.PingServerAsync(request.ServerName);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            
            return Ok(new
            {
                success = true,
                operation = "ping_server",
                serverName = request.ServerName,
                pingSuccessful = resultObj.GetProperty("pingSuccessful").GetBoolean(),
                isHealthy = resultObj.GetProperty("isHealthy").GetBoolean(),
                responseTimeMs = resultObj.TryGetProperty("lastPingDuration", out JsonElement duration) 
                    ? (double?)duration.GetDouble() : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping server: {ServerName}", request.ServerName);
            return StatusCode(500, new
            {
                success = false,
                operation = "ping_server",
                error = ex.Message,
                serverName = request.ServerName
            });
        }
    }

    /// <summary>
    /// Compare collections between two servers to identify differences
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> CompareServers([FromBody] CompareServersRequest request)
    {
        try
        {
            string result = await _crossServerOperations.CompareCollectionsAsync(
                request.Server1,
                request.Server2,
                request.CollectionName,
                request.Filter ?? "{}");
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare servers {Server1} and {Server2}", 
                request.Server1, request.Server2);
            return StatusCode(500, new
            {
                success = false,
                operation = "compare_servers",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Synchronize data between two servers. Use dryRun=true to preview changes
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncCollections([FromBody] SyncCollectionsRequest request)
    {
        try
        {
            string result = await _crossServerOperations.SyncDataAsync(
                request.SourceServer,
                request.TargetServer,
                request.CollectionName,
                request.Filter ?? "{}",
                request.DryRun ?? true);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync collections from {Source} to {Target}",
                request.SourceServer, request.TargetServer);
            return StatusCode(500, new
            {
                success = false,
                operation = "sync_collections",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Execute a query across multiple servers simultaneously and aggregate results
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> CrossServerQuery([FromBody] CrossServerQueryRequest request)
    {
        try
        {
            if (request.ServerNames.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Invalid or empty server names array",
                    operation = "cross_server_query"
                });
            }
            
            string result = await _crossServerOperations.CrossServerQueryAsync(
                request.ServerNames,
                request.CollectionName,
                request.Filter ?? "{}",
                request.LimitPerServer ?? 50);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute cross-server query");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                operation = "cross_server_query"
            });
        }
    }

    /// <summary>
    /// Transfer multiple collections from one server to another. Use dryRun=true to preview
    /// </summary>
    [HttpPost("bulk-transfer")]
    public async Task<IActionResult> BulkTransfer([FromBody] BulkTransferRequest request)
    {
        try
        {
            if (request.CollectionNames.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Invalid or empty collection names array",
                    operation = "bulk_transfer"
                });
            }
            
            string result = await _crossServerOperations.BulkTransferAsync(
                request.SourceServer,
                request.TargetServer,
                request.CollectionNames,
                request.DryRun ?? true);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk transfer from {Source} to {Target}",
                request.SourceServer, request.TargetServer);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                operation = "bulk_transfer"
            });
        }
    }

    /// <summary>
    /// Execute a MongoDB command on all connected servers simultaneously
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> BatchOperations([FromBody] BatchOperationsRequest request)
    {
        try
        {
            string result = await _crossServerOperations.ExecuteOnAllServersAsync(request.Command);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute batch operations");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                operation = "batch_operations"
            });
        }
    }

    /// <summary>
    /// Get the comprehensive health status dashboard for all connected servers
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> HealthDashboard()
    {
        try
        {
            string result = await _crossServerOperations.GetHealthDashboardAsync();
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health dashboard");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                operation = "health_dashboard"
            });
        }
    }
}

// Request DTOs
public record PingServerRequest(
    string ServerName
);

public record CompareServersRequest(
    string Server1,
    string Server2,
    string CollectionName,
    string? Filter = "{}"
);

public record SyncCollectionsRequest(
    string SourceServer,
    string TargetServer,
    string CollectionName,
    string? Filter = "{}",
    bool? DryRun = true
);

public record CrossServerQueryRequest(
    string[] ServerNames,
    string CollectionName,
    string? Filter = "{}",
    int? LimitPerServer = 50
);

public record BulkTransferRequest(
    string SourceServer,
    string TargetServer,
    string[] CollectionNames,
    bool? DryRun = true
);

public record BatchOperationsRequest(
    string Command
);