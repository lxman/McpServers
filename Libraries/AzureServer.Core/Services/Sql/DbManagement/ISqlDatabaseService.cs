using AzureServer.Core.Services.Sql.DbManagement.Models;

namespace AzureServer.Core.Services.Sql.DbManagement;

/// <summary>
/// Service for managing Azure SQL, PostgreSQL, and MySQL databases
/// </summary>
public interface ISqlDatabaseService
{
    // Server Management
    Task<IEnumerable<ServerDto>> ListServersAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<ServerDto?> GetServerAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> DeleteServerAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    
    // Database Management
    Task<IEnumerable<DatabaseDto>> ListDatabasesAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    Task<DatabaseDto?> GetDatabaseAsync(string databaseName, string serverName, string resourceGroupName, string? subscriptionId = null);
    Task<DatabaseDto> CreateDatabaseAsync(string databaseName, string serverName, string resourceGroupName, 
        string? subscriptionId = null, string? edition = null, string? serviceObjective = null, 
        long? maxSizeBytes = null, Dictionary<string, string>? tags = null);
    Task<bool> DeleteDatabaseAsync(string databaseName, string serverName, string resourceGroupName, string? subscriptionId = null);
    
    // Firewall Rules
    Task<IEnumerable<FirewallRuleDto>> ListFirewallRulesAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    Task<FirewallRuleDto?> GetFirewallRuleAsync(string ruleName, string serverName, string resourceGroupName, string? subscriptionId = null);
    Task<FirewallRuleDto> CreateFirewallRuleAsync(string ruleName, string serverName, string resourceGroupName, 
        string startIpAddress, string endIpAddress, string? subscriptionId = null);
    Task<bool> DeleteFirewallRuleAsync(string ruleName, string serverName, string resourceGroupName, string? subscriptionId = null);
    
    // Elastic Pools
    Task<IEnumerable<ElasticPoolDto>> ListElasticPoolsAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    Task<ElasticPoolDto?> GetElasticPoolAsync(string poolName, string serverName, string resourceGroupName, string? subscriptionId = null);
    
    // PostgreSQL Specific
    Task<IEnumerable<ServerDto>> ListPostgreSqlServersAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<IEnumerable<DatabaseDto>> ListPostgreSqlDatabasesAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> DeletePostgreSqlServerAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    
    // PostgreSQL Flexible Server Specific
    Task<IEnumerable<ServerDto>> ListPostgreSqlFlexibleServersAsync(string? subscriptionId = null, string? resourceGroupName = null);

    
    // MySQL Specific
    Task<IEnumerable<ServerDto>> ListMySqlServersAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<IEnumerable<DatabaseDto>> ListMySqlDatabasesAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> DeleteMySqlServerAsync(string serverName, string resourceGroupName, string? subscriptionId = null);
    
    // MySQL Flexible Server Specific
    Task<IEnumerable<ServerDto>> ListMySqlFlexibleServersAsync(string? subscriptionId = null, string? resourceGroupName = null);

}