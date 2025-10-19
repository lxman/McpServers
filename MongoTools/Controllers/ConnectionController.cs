using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MongoTools.Controllers;

/// <summary>
/// Manages MongoDB connection lifecycle and server management
/// </summary>
[ApiController]
[Route("api/connection")]
[Tags("Connection")]
public class ConnectionController(MongoDbService mongoDbService, ILogger<ConnectionController> logger)
    : ControllerBase
{
    /// <summary>
    /// Connect to MongoDB as your primary database for CRUD operations
    /// </summary>
    [HttpPost("primary")]
    public async Task<IActionResult> ConnectPrimary([FromBody] ConnectPrimaryRequest request)
    {
        try
        {
            string result = await mongoDbService.ConnectAsync(request.ConnectionString, request.DatabaseName);
            return Ok(new
            {
                success = true,
                message = result,
                connectionType = "primary",
                supportsCrud = true,
                operatedOn = "default"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to primary MongoDB server");
            return BadRequest(new
            {
                success = false,
                error = ex.Message,
                connectionType = "primary",
                suggestion = "Verify your connection string format and network connectivity"
            });
        }
    }

    /// <summary>
    /// Connect to an additional MongoDB server for multi-database operations
    /// </summary>
    [HttpPost("additional")]
    public async Task<IActionResult> ConnectAdditional([FromBody] ConnectAdditionalRequest request)
    {
        try
        {
            string result = await mongoDbService.ConnectToServerAsync(
                request.ServerName, 
                request.ConnectionString, 
                request.DatabaseName);
            
            return Ok(new
            {
                success = true,
                message = result,
                connectionType = "additional",
                serverName = request.ServerName,
                supportsCrud = true,
                operatedOn = request.ServerName
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to additional MongoDB server: {ServerName}", request.ServerName);
            return BadRequest(new
            {
                success = false,
                error = ex.Message,
                connectionType = "additional",
                serverName = request.ServerName,
                suggestion = "Verify connection string and ensure server name is unique"
            });
        }
    }

    /// <summary>
    /// Disconnect from the primary MongoDB connection
    /// </summary>
    [HttpPost("primary/disconnect")]
    public IActionResult DisconnectPrimary()
    {
        try
        {
            string result = mongoDbService.Disconnect();
            return Ok(new
            {
                success = true,
                message = result,
                operatedOn = "default"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disconnect from primary MongoDB server");
            return BadRequest(new
            {
                success = false,
                error = ex.Message,
                operatedOn = "default"
            });
        }
    }

    /// <summary>
    /// Disconnect from a specific additional MongoDB server
    /// </summary>
    [HttpPost("{serverName}/disconnect")]
    public IActionResult DisconnectFromServer(string serverName)
    {
        try
        {
            string result = mongoDbService.DisconnectFromServer(serverName);
            return Ok(new
            {
                success = true,
                message = result,
                operatedOn = serverName
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disconnect from MongoDB server: {ServerName}", serverName);
            return BadRequest(new
            {
                success = false,
                error = ex.Message,
                operatedOn = serverName
            });
        }
    }

    /// <summary>
    /// Show comprehensive connection status including primary and all additional servers
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetConnectionStatus()
    {
        try
        {
            string primaryStatus = mongoDbService.GetConnectionStatus();
            string allConnections = mongoDbService.ListActiveConnections();
            
            var connectionsObj = JsonSerializer.Deserialize<JsonElement>(allConnections);
            
            return Ok(new
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
                    totalConnections = mongoDbService.ConnectionManager.GetServerNames().Count,
                    healthyConnections = mongoDbService.ConnectionManager.GetServerNames()
                        .Count(name => mongoDbService.ConnectionManager.IsConnected(name)),
                    supportsBasicCrud = primaryStatus != "Not connected to MongoDB",
                    supportsMultiServer = mongoDbService.ConnectionManager.GetServerNames().Count > 1
                },
                nextSteps = primaryStatus == "Not connected to MongoDB" 
                    ? "Run POST /api/connection/primary to establish your main database connection"
                    : "Connection ready for CRUD operations. Use POST /api/connection/additional for multi-server features."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get connection status");
            return StatusCode(500, new
            {
                error = ex.Message,
                suggestion = "Check connection manager status"
            });
        }
    }

    /// <summary>
    /// List all active server connections with their status and capabilities
    /// </summary>
    [HttpGet("servers")]
    public IActionResult ListServers()
    {
        try
        {
            string result = mongoDbService.ListActiveConnections();
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            
            return Ok(new
            {
                success = true,
                operation = "list_servers",
                totalConnections = resultObj.GetProperty("totalConnections").GetInt32(),
                healthyConnections = resultObj.GetProperty("healthyConnections").GetInt32(),
                defaultServer = resultObj.GetProperty("defaultServer").GetString(),
                connections = resultObj.GetProperty("connections")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list servers");
            return StatusCode(500, new
            {
                success = false,
                operation = "list_servers",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Set the default server for operations that don't specify a server name
    /// </summary>
    [HttpPost("servers/default")]
    public IActionResult SetDefaultServer([FromBody] SetDefaultServerRequest request)
    {
        try
        {
            string result = mongoDbService.SetDefaultServer(request.ServerName);
            return Ok(new
            {
                success = true,
                operation = "set_default_server",
                message = result,
                newDefault = request.ServerName
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set default server: {ServerName}", request.ServerName);
            return BadRequest(new
            {
                success = false,
                operation = "set_default_server",
                error = ex.Message,
                suggestion = "Verify the server name exists in your active connections"
            });
        }
    }

    /// <summary>
    /// Get detailed connection status for a specific server
    /// </summary>
    [HttpGet("servers/{serverName}/status")]
    public IActionResult GetServerStatus(string serverName)
    {
        try
        {
            string result = mongoDbService.GetServerConnectionStatus(serverName);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            
            return Ok(new
            {
                success = true,
                operation = "get_server_status",
                serverName,
                details = resultObj
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get server status: {ServerName}", serverName);
            return StatusCode(500, new
            {
                success = false,
                operation = "get_server_status",
                error = ex.Message,
                serverName
            });
        }
    }
}

// Request DTOs
public record ConnectPrimaryRequest(
    string ConnectionString,
    string DatabaseName
);

public record ConnectAdditionalRequest(
    string ServerName,
    string ConnectionString,
    string DatabaseName
);

public record SetDefaultServerRequest(
    string ServerName
);