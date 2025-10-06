using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.Sql.DbManagement;
using AzureMcp.Services.Sql.DbManagement.Models;
using AzureMcp.Services.Sql.QueryExecution;
using AzureMcp.Services.Sql.QueryExecution.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class SqlTools(ISqlDatabaseService databaseService, ISqlQueryService queryService)
{
    #region Database Management - Servers

    [McpServerTool]
    [Description("List Azure SQL, PostgreSQL, or MySQL servers")]
    public async Task<string> ListServersAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ServerDto> servers = await databaseService.ListServersAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, servers = servers.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListServers");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific SQL server")]
    public async Task<string> GetServerAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            ServerDto? server = await databaseService.GetServerAsync(serverName, resourceGroupName, subscriptionId);
            if (server == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Server {serverName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, server },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetServer");
        }
    }

    [McpServerTool]
    [Description("Delete an Azure SQL server")]
    public async Task<string> DeleteServerAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeleteServerAsync(serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = result, message = result ? "Server deleted successfully" : "Server not found" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteServer");
        }
    }

    #endregion

    #region Database Management - Databases

    [McpServerTool]
    [Description("List databases on an Azure SQL server")]
    public async Task<string> ListDatabasesAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<DatabaseDto> databases = await databaseService.ListDatabasesAsync(serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, databases = databases.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListDatabases");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific database")]
    public async Task<string> GetDatabaseAsync(
        [Description("Database name")] string databaseName,
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            DatabaseDto? database = await databaseService.GetDatabaseAsync(databaseName, serverName, resourceGroupName, subscriptionId);
            if (database == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Database {databaseName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, database },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetDatabase");
        }
    }

    [McpServerTool]
    [Description("Create a new Azure SQL database")]
    public async Task<string> CreateDatabaseAsync(
        [Description("Database name")] string databaseName,
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional edition (Basic, Standard, Premium)")] string? edition = null,
        [Description("Optional service objective (Basic, S0, S1, P1, etc.)")] string? serviceObjective = null,
        [Description("Optional maximum size in bytes")] long? maxSizeBytes = null,
        [Description("Optional tags as JSON object")] string? tagsJson = null)
    {
        try
        {
            Dictionary<string, string>? tags = null;
            if (!string.IsNullOrEmpty(tagsJson))
                tags = JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson);

            DatabaseDto database = await databaseService.CreateDatabaseAsync(databaseName, serverName, resourceGroupName, 
                subscriptionId, edition, serviceObjective, maxSizeBytes, tags);

            return JsonSerializer.Serialize(new { success = true, database },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateDatabase");
        }
    }

    [McpServerTool]
    [Description("Delete an Azure SQL database")]
    public async Task<string> DeleteDatabaseAsync(
        [Description("Database name")] string databaseName,
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeleteDatabaseAsync(databaseName, serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = result, message = result ? "Database deleted successfully" : "Database not found" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteDatabase");
        }
    }

    #endregion

    #region Database Management - Firewall Rules

    [McpServerTool]
    [Description("List firewall rules on an Azure SQL server")]
    public async Task<string> ListFirewallRulesAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<FirewallRuleDto> rules = await databaseService.ListFirewallRulesAsync(serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, firewallRules = rules.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListFirewallRules");
        }
    }

    [McpServerTool]
    [Description("Create a firewall rule on an Azure SQL server")]
    public async Task<string> CreateFirewallRuleAsync(
        [Description("Rule name")] string ruleName,
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Start IP address (e.g., 192.168.1.1)")] string startIpAddress,
        [Description("End IP address (e.g., 192.168.1.255)")] string endIpAddress,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            FirewallRuleDto rule = await databaseService.CreateFirewallRuleAsync(ruleName, serverName, resourceGroupName, 
                startIpAddress, endIpAddress, subscriptionId);

            return JsonSerializer.Serialize(new { success = true, firewallRule = rule },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateFirewallRule");
        }
    }

    [McpServerTool]
    [Description("Delete a firewall rule from an Azure SQL server")]
    public async Task<string> DeleteFirewallRuleAsync(
        [Description("Rule name")] string ruleName,
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeleteFirewallRuleAsync(ruleName, serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = result, message = result ? "Firewall rule deleted successfully" : "Firewall rule not found" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteFirewallRule");
        }
    }

    #endregion

    #region Database Management - Elastic Pools

    [McpServerTool]
    [Description("List elastic pools on an Azure SQL server")]
    public async Task<string> ListElasticPoolsAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ElasticPoolDto> pools = await databaseService.ListElasticPoolsAsync(serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, elasticPools = pools.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListElasticPools");
        }
    }

    #endregion

    #region Database Management - PostgreSQL

    [McpServerTool]
    [Description("List Azure PostgreSQL servers")]
    public async Task<string> ListPostgreSqlServersAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ServerDto> servers = await databaseService.ListPostgreSqlServersAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, servers = servers.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListPostgreSqlServers");
        }
    }

    [McpServerTool]
    [Description("List databases on an Azure PostgreSQL server")]
    public async Task<string> ListPostgreSqlDatabasesAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<DatabaseDto> databases = await databaseService.ListPostgreSqlDatabasesAsync(serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, databases = databases.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListPostgreSqlDatabases");
        }
    }

    [McpServerTool]
    [Description("Delete an Azure PostgreSQL server")]
    public async Task<string> DeletePostgreSqlServerAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeletePostgreSqlServerAsync(serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = result, message = result ? "PostgreSQL server deleted successfully" : "PostgreSQL server not found" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeletePostgreSqlServer");
        }
    }

    [McpServerTool]
    [Description("List Azure PostgreSQL Flexible servers")]
    public async Task<string> ListPostgreSqlFlexibleServersAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ServerDto> servers = await databaseService.ListPostgreSqlFlexibleServersAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, servers = servers.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListPostgreSqlFlexibleServers");
        }
    }



    #endregion

    #region Database Management - MySQL

    [McpServerTool]
    [Description("List Azure MySQL servers")]
    public async Task<string> ListMySqlServersAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ServerDto> servers = await databaseService.ListMySqlServersAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, servers = servers.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListMySqlServers");
        }
    }

    [McpServerTool]
    [Description("List databases on an Azure MySQL server")]
    public async Task<string> ListMySqlDatabasesAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<DatabaseDto> databases = await databaseService.ListMySqlDatabasesAsync(serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, databases = databases.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListMySqlDatabases");
        }
    }

    [McpServerTool]
    [Description("Delete an Azure MySQL server")]
    public async Task<string> DeleteMySqlServerAsync(
        [Description("Server name")] string serverName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool result = await databaseService.DeleteMySqlServerAsync(serverName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = result, message = result ? "MySQL server deleted successfully" : "MySQL server not found" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteMySqlServer");
        }
    }


    #endregion

    #region Query Execution

    [McpServerTool]
    [Description("Execute a SELECT query against a database and return results")]
    public async Task<string> ExecuteQueryAsync(
        [Description("Database connection info as JSON (see example)")] string connectionInfoJson,
        [Description("SQL query to execute")] string query,
        [Description("Maximum number of rows to return (default: 1000)")] int maxRows = 1000,
        [Description("Query timeout in seconds (default: 30)")] int timeoutSeconds = 30)
    {
        try
        {
            ConnectionInfoDto? connectionInfo = JsonSerializer.Deserialize<ConnectionInfoDto>(connectionInfoJson);
            if (connectionInfo == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid connection info" },
                    SerializerOptions.JsonOptionsIndented);
            }

            QueryResultDto result = await queryService.ExecuteQueryAsync(connectionInfo, query, maxRows, timeoutSeconds);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ExecuteQuery");
        }
    }

    [McpServerTool]
    [Description("Execute a non-query command (INSERT, UPDATE, DELETE, CREATE, ALTER, etc.)")]
    public async Task<string> ExecuteNonQueryAsync(
        [Description("Database connection info as JSON")] string connectionInfoJson,
        [Description("SQL command to execute")] string command,
        [Description("Command timeout in seconds (default: 30)")] int timeoutSeconds = 30)
    {
        try
        {
            ConnectionInfoDto? connectionInfo = JsonSerializer.Deserialize<ConnectionInfoDto>(connectionInfoJson);
            if (connectionInfo == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid connection info" },
                    SerializerOptions.JsonOptionsIndented);
            }

            QueryResultDto result = await queryService.ExecuteNonQueryAsync(connectionInfo, command, timeoutSeconds);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ExecuteNonQuery");
        }
    }

    [McpServerTool]
    [Description("Test database connectivity")]
    public async Task<string> TestConnectionAsync(
        [Description("Database connection info as JSON")] string connectionInfoJson)
    {
        try
        {
            ConnectionInfoDto? connectionInfo = JsonSerializer.Deserialize<ConnectionInfoDto>(connectionInfoJson);
            if (connectionInfo == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid connection info" },
                    SerializerOptions.JsonOptionsIndented);
            }

            bool result = await queryService.TestConnectionAsync(connectionInfo);
            return JsonSerializer.Serialize(new 
            { 
                success = result, 
                message = result ? "Connection successful" : "Connection failed" 
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "TestConnection");
        }
    }

    [McpServerTool]
    [Description("Get database schema information (tables and columns)")]
    public async Task<string> GetSchemaInfoAsync(
        [Description("Database connection info as JSON")] string connectionInfoJson,
        [Description("Optional specific table name to get column info")] string? tableName = null)
    {
        try
        {
            ConnectionInfoDto? connectionInfo = JsonSerializer.Deserialize<ConnectionInfoDto>(connectionInfoJson);
            if (connectionInfo == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid connection info" },
                    SerializerOptions.JsonOptionsIndented);
            }

            QueryResultDto result = await queryService.GetSchemaInfoAsync(connectionInfo, tableName);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetSchemaInfo");
        }
    }

    [McpServerTool]
    [Description("Execute multiple commands in a transaction")]
    public async Task<string> ExecuteTransactionAsync(
        [Description("Database connection info as JSON")] string connectionInfoJson,
        [Description("Array of SQL commands as JSON")] string commandsJson,
        [Description("Transaction timeout in seconds (default: 30)")] int timeoutSeconds = 30)
    {
        try
        {
            ConnectionInfoDto? connectionInfo = JsonSerializer.Deserialize<ConnectionInfoDto>(connectionInfoJson);
            if (connectionInfo == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid connection info" },
                    SerializerOptions.JsonOptionsIndented);
            }

            List<string>? commands = JsonSerializer.Deserialize<List<string>>(commandsJson);
            if (commands == null || commands.Count == 0)
            {
                return JsonSerializer.Serialize(new { success = false, error = "No commands provided" },
                    SerializerOptions.JsonOptionsIndented);
            }

            List<QueryResultDto> results = await queryService.ExecuteTransactionAsync(connectionInfo, commands, timeoutSeconds);
            return JsonSerializer.Serialize(new { success = true, results }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ExecuteTransaction");
        }
    }

    #endregion

    #region Helper Methods

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = ex.Message,
            operation,
            exceptionType = ex.GetType().Name
        }, SerializerOptions.JsonOptionsIndented);
    }

    #endregion
}