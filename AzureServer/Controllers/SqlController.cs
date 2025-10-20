using AzureServer.Services.Sql.DbManagement;
using AzureServer.Services.Sql.DbManagement.Models;
using AzureServer.Services.Sql.QueryExecution;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SqlController(ISqlDatabaseService databaseService, ISqlQueryService queryService, ILogger<SqlController> logger) : ControllerBase
{
    [HttpGet("servers")]
    public async Task<ActionResult> ListServers([FromQuery] string? subscriptionId = null, [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ServerDto> servers = await databaseService.ListServersAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, servers = servers.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing SQL servers");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListServers", type = ex.GetType().Name });
        }
    }

    [HttpGet("servers/{resourceGroupName}/{serverName}")]
    public async Task<ActionResult> GetServer(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            ServerDto? server = await databaseService.GetServerAsync(serverName, resourceGroupName, subscriptionId);
            if (server is null)
                return NotFound(new { success = false, error = $"Server {serverName} not found" });

            return Ok(new { success = true, server });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetServer", type = ex.GetType().Name });
        }
    }

    [HttpDelete("servers/{resourceGroupName}/{serverName}")]
    public async Task<ActionResult> DeleteServer(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeleteServerAsync(serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = result, message = result ? "Server deleted successfully" : "Server not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteServer", type = ex.GetType().Name });
        }
    }

    [HttpGet("servers/{resourceGroupName}/{serverName}/databases")]
    public async Task<ActionResult> ListDatabases(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<DatabaseDto> databases = await databaseService.ListDatabasesAsync(serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = true, databases = databases.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing databases on server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListDatabases", type = ex.GetType().Name });
        }
    }

    [HttpGet("servers/{resourceGroupName}/{serverName}/databases/{databaseName}")]
    public async Task<ActionResult> GetDatabase(string databaseName, string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            DatabaseDto? database = await databaseService.GetDatabaseAsync(databaseName, serverName, resourceGroupName, subscriptionId);
            if (database is null)
                return NotFound(new { success = false, error = $"Database {databaseName} not found" });

            return Ok(new { success = true, database });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting database {DatabaseName}", databaseName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetDatabase", type = ex.GetType().Name });
        }
    }

    [HttpPost("servers/{resourceGroupName}/{serverName}/databases")]
    public async Task<ActionResult> CreateDatabase(
        string serverName,
        string resourceGroupName,
        [FromBody] CreateDatabaseRequest request)
    {
        try
        {
            DatabaseDto database = await databaseService.CreateDatabaseAsync(
                request.DatabaseName, serverName, resourceGroupName,
                request.SubscriptionId, request.Edition, request.ServiceObjective,
                request.MaxSizeBytes, request.Tags);

            return Ok(new { success = true, database });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating database {DatabaseName}", request.DatabaseName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateDatabase", type = ex.GetType().Name });
        }
    }

    [HttpDelete("servers/{resourceGroupName}/{serverName}/databases/{databaseName}")]
    public async Task<ActionResult> DeleteDatabase(string databaseName, string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeleteDatabaseAsync(databaseName, serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = result, message = result ? "Database deleted successfully" : "Database not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting database {DatabaseName}", databaseName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteDatabase", type = ex.GetType().Name });
        }
    }

    [HttpGet("servers/{resourceGroupName}/{serverName}/firewall-rules")]
    public async Task<ActionResult> ListFirewallRules(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<FirewallRuleDto> rules = await databaseService.ListFirewallRulesAsync(serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = true, firewallRules = rules.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing firewall rules for server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListFirewallRules", type = ex.GetType().Name });
        }
    }

    [HttpPost("servers/{resourceGroupName}/{serverName}/firewall-rules")]
    public async Task<ActionResult> CreateFirewallRule(
        string serverName,
        string resourceGroupName,
        [FromBody] CreateFirewallRuleRequest request)
    {
        try
        {
            FirewallRuleDto rule = await databaseService.CreateFirewallRuleAsync(
                request.RuleName, serverName, resourceGroupName,
                request.StartIpAddress, request.EndIpAddress, request.SubscriptionId);

            return Ok(new { success = true, firewallRule = rule });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating firewall rule {RuleName}", request.RuleName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateFirewallRule", type = ex.GetType().Name });
        }
    }

    [HttpDelete("servers/{resourceGroupName}/{serverName}/firewall-rules/{ruleName}")]
    public async Task<ActionResult> DeleteFirewallRule(string ruleName, string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeleteFirewallRuleAsync(ruleName, serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = result, message = result ? "Firewall rule deleted successfully" : "Firewall rule not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting firewall rule {RuleName}", ruleName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteFirewallRule", type = ex.GetType().Name });
        }
    }

    [HttpGet("servers/{resourceGroupName}/{serverName}/elastic-pools")]
    public async Task<ActionResult> ListElasticPools(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ElasticPoolDto> pools = await databaseService.ListElasticPoolsAsync(serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = true, elasticPools = pools.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing elastic pools for server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListElasticPools", type = ex.GetType().Name });
        }
    }

    [HttpGet("postgresql/servers")]
    public async Task<ActionResult> ListPostgreSqlServers([FromQuery] string? subscriptionId = null, [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ServerDto> servers = await databaseService.ListPostgreSqlServersAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, servers = servers.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL servers");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListPostgreSqlServers", type = ex.GetType().Name });
        }
    }

    [HttpGet("postgresql/servers/{resourceGroupName}/{serverName}/databases")]
    public async Task<ActionResult> ListPostgreSqlDatabases(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<DatabaseDto> databases = await databaseService.ListPostgreSqlDatabasesAsync(serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = true, databases = databases.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL databases on server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListPostgreSqlDatabases", type = ex.GetType().Name });
        }
    }

    [HttpDelete("postgresql/servers/{resourceGroupName}/{serverName}")]
    public async Task<ActionResult> DeletePostgreSqlServer(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeletePostgreSqlServerAsync(serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = result, message = result ? "PostgreSQL server deleted successfully" : "PostgreSQL server not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting PostgreSQL server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeletePostgreSqlServer", type = ex.GetType().Name });
        }
    }

    [HttpGet("postgresql/flexible-servers")]
    public async Task<ActionResult> ListPostgreSqlFlexibleServers([FromQuery] string? subscriptionId = null, [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ServerDto> servers = await databaseService.ListPostgreSqlFlexibleServersAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, servers = servers.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL Flexible servers");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListPostgreSqlFlexibleServers", type = ex.GetType().Name });
        }
    }

    [HttpGet("mysql/servers")]
    public async Task<ActionResult> ListMySqlServers([FromQuery] string? subscriptionId = null, [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ServerDto> servers = await databaseService.ListMySqlServersAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, servers = servers.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing MySQL servers");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListMySqlServers", type = ex.GetType().Name });
        }
    }

    [HttpGet("mysql/servers/{resourceGroupName}/{serverName}/databases")]
    public async Task<ActionResult> ListMySqlDatabases(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<DatabaseDto> databases = await databaseService.ListMySqlDatabasesAsync(serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = true, databases = databases.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing MySQL databases on server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListMySqlDatabases", type = ex.GetType().Name });
        }
    }

    [HttpDelete("mysql/servers/{resourceGroupName}/{serverName}")]
    public async Task<ActionResult> DeleteMySqlServer(string serverName, string resourceGroupName, [FromQuery] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeleteMySqlServerAsync(serverName, resourceGroupName, subscriptionId);
            return Ok(new { success = result, message = result ? "MySQL server deleted successfully" : "MySQL server not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting MySQL server {ServerName}", serverName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteMySqlServer", type = ex.GetType().Name });
        }
    }
}

public record CreateDatabaseRequest(string DatabaseName, string? SubscriptionId = null, string? Edition = null, string? ServiceObjective = null, long? MaxSizeBytes = null, Dictionary<string, string>? Tags = null);
public record CreateFirewallRuleRequest(string RuleName, string StartIpAddress, string EndIpAddress, string? SubscriptionId = null);