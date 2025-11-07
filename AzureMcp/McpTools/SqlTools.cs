using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.Sql.DbManagement;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure SQL operations
/// </summary>
[McpServerToolType]
public class SqlTools(
    ISqlDatabaseService databaseService,
    ILogger<SqlTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    #region SQL Server Operations

    [McpServerTool, DisplayName("list_sql_servers")]
    [Description("List SQL servers. See skills/azure/sql/list-servers.md only when using this tool")]
    public async Task<string> ListServers(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing SQL servers");
            var servers = await databaseService.ListServersAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                servers = servers.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing SQL servers");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_sql_server")]
    [Description("Get SQL server. See skills/azure/sql/get-server.md only when using this tool")]
    public async Task<string> GetServer(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting SQL server {ServerName}", serverName);
            var server = await databaseService.GetServerAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                server
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_sql_server")]
    [Description("Delete SQL server. See skills/azure/sql/delete-server.md only when using this tool")]
    public async Task<string> DeleteServer(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Deleting SQL server {ServerName}", serverName);
            var result = await databaseService.DeleteServerAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Server deleted successfully" : "Server not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_sql_databases")]
    [Description("List SQL databases. See skills/azure/sql/list-databases.md only when using this tool")]
    public async Task<string> ListDatabases(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing databases on server {ServerName}", serverName);
            var databases = await databaseService.ListDatabasesAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                databases = databases.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing databases on server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_sql_database")]
    [Description("Get SQL database. See skills/azure/sql/get-database.md only when using this tool")]
    public async Task<string> GetDatabase(
        string databaseName,
        string serverName,
        string resourceGroupName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting database {DatabaseName}", databaseName);
            var database = await databaseService.GetDatabaseAsync(databaseName, serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                database
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting database {DatabaseName}", databaseName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_sql_database")]
    [Description("Create SQL database. See skills/azure/sql/create-database.md only when using this tool")]
    public async Task<string> CreateDatabase(
        string databaseName,
        string serverName,
        string resourceGroupName,
        string? subscriptionId = null,
        string? edition = null,
        string? serviceObjective = null,
        long? maxSizeBytes = null,
        Dictionary<string, string>? tags = null)
    {
        try
        {
            logger.LogDebug("Creating database {DatabaseName}", databaseName);
            var database = await databaseService.CreateDatabaseAsync(
                databaseName, serverName, resourceGroupName,
                subscriptionId, edition, serviceObjective,
                maxSizeBytes, tags);

            return JsonSerializer.Serialize(new
            {
                success = true,
                database
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating database {DatabaseName}", databaseName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_sql_database")]
    [Description("Delete SQL database. See skills/azure/sql/delete-database.md only when using this tool")]
    public async Task<string> DeleteDatabase(
        string databaseName,
        string serverName,
        string resourceGroupName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Deleting database {DatabaseName}", databaseName);
            var result = await databaseService.DeleteDatabaseAsync(databaseName, serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Database deleted successfully" : "Database not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting database {DatabaseName}", databaseName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_firewall_rules")]
    [Description("List firewall rules. See skills/azure/sql/list-firewall-rules.md only when using this tool")]
    public async Task<string> ListFirewallRules(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing firewall rules for server {ServerName}", serverName);
            var rules = await databaseService.ListFirewallRulesAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                firewallRules = rules.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing firewall rules for server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("create_firewall_rule")]
    [Description("Create firewall rule. See skills/azure/sql/create-firewall-rule.md only when using this tool")]
    public async Task<string> CreateFirewallRule(
        string ruleName,
        string serverName,
        string resourceGroupName,
        string startIpAddress,
        string endIpAddress,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Creating firewall rule {RuleName}", ruleName);
            var rule = await databaseService.CreateFirewallRuleAsync(
                ruleName, serverName, resourceGroupName,
                startIpAddress, endIpAddress, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                firewallRule = rule
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating firewall rule {RuleName}", ruleName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_firewall_rule")]
    [Description("Delete firewall rule. See skills/azure/sql/delete-firewall-rule.md only when using this tool")]
    public async Task<string> DeleteFirewallRule(
        string ruleName,
        string serverName,
        string resourceGroupName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Deleting firewall rule {RuleName}", ruleName);
            var result = await databaseService.DeleteFirewallRuleAsync(ruleName, serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "Firewall rule deleted successfully" : "Firewall rule not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting firewall rule {RuleName}", ruleName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_elastic_pools")]
    [Description("List elastic pools. See skills/azure/sql/list-elastic-pools.md only when using this tool")]
    public async Task<string> ListElasticPools(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing elastic pools for server {ServerName}", serverName);
            var pools = await databaseService.ListElasticPoolsAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                elasticPools = pools.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing elastic pools for server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region PostgreSQL Operations

    [McpServerTool, DisplayName("list_postgresql_servers")]
    [Description("List PostgreSQL servers. See skills/azure/sql/list-postgresql-servers.md only when using this tool")]
    public async Task<string> ListPostgreSqlServers(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing PostgreSQL servers");
            var servers = await databaseService.ListPostgreSqlServersAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                servers = servers.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL servers");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_postgresql_databases")]
    [Description("List PostgreSQL databases. See skills/azure/sql/list-postgresql-databases.md only when using this tool")]
    public async Task<string> ListPostgreSqlDatabases(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing PostgreSQL databases on server {ServerName}", serverName);
            var databases = await databaseService.ListPostgreSqlDatabasesAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                databases = databases.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL databases on server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_postgresql_server")]
    [Description("Delete PostgreSQL server. See skills/azure/sql/delete-postgresql-server.md only when using this tool")]
    public async Task<string> DeletePostgreSqlServer(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Deleting PostgreSQL server {ServerName}", serverName);
            var result = await databaseService.DeletePostgreSqlServerAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "PostgreSQL server deleted successfully" : "PostgreSQL server not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting PostgreSQL server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_postgresql_flexible_servers")]
    [Description("List PostgreSQL Flexible servers. See skills/azure/sql/list-postgresql-flexible.md only when using this tool")]
    public async Task<string> ListPostgreSqlFlexibleServers(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing PostgreSQL Flexible servers");
            var servers = await databaseService.ListPostgreSqlFlexibleServersAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                servers = servers.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL Flexible servers");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region MySQL Operations

    [McpServerTool, DisplayName("list_mysql_servers")]
    [Description("List MySQL servers. See skills/azure/sql/list-mysql-servers.md only when using this tool")]
    public async Task<string> ListMySqlServers(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing MySQL servers");
            var servers = await databaseService.ListMySqlServersAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                servers = servers.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing MySQL servers");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_mysql_databases")]
    [Description("List MySQL databases. See skills/azure/sql/list-mysql-databases.md only when using this tool")]
    public async Task<string> ListMySqlDatabases(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing MySQL databases on server {ServerName}", serverName);
            var databases = await databaseService.ListMySqlDatabasesAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                databases = databases.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing MySQL databases on server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_mysql_server")]
    [Description("Delete MySQL server. See skills/azure/sql/delete-mysql-server.md only when using this tool")]
    public async Task<string> DeleteMySqlServer(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Deleting MySQL server {ServerName}", serverName);
            var result = await databaseService.DeleteMySqlServerAsync(serverName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = result,
                message = result ? "MySQL server deleted successfully" : "MySQL server not found"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting MySQL server {ServerName}", serverName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion
}