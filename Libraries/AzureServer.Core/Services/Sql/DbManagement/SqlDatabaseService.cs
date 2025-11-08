using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.MySql;
using Azure.ResourceManager.MySql.FlexibleServers;
using Azure.ResourceManager.PostgreSql;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Sql.DbManagement.Models;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.Sql.DbManagement;

public class SqlDatabaseService(
    ArmClientFactory armClientFactory,
    ILogger<SqlDatabaseService> logger) : ISqlDatabaseService
{
    #region Server Management

    public async Task<IEnumerable<ServerDto>> ListServersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var servers = new List<ServerDto>();

            if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                if (!string.IsNullOrEmpty(resourceGroupName))
                {
                    // List servers in specific resource group
                    ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
                    SqlServerCollection? sqlServers = resourceGroup.GetSqlServers();

                    await foreach (SqlServerResource? server in sqlServers.GetAllAsync())
                    {
                        servers.Add(MapSqlServer(server));
                    }
                }
                else
                {
                    // List all servers in subscription
                    await foreach (SqlServerResource? server in subscription.GetSqlServersAsync())
                    {
                        servers.Add(MapSqlServer(server));
                    }
                }
            }
            else
            {
                // List servers across all subscriptions
                await foreach (SubscriptionResource? subscription in client.GetSubscriptions().GetAllAsync())
                {
                    await foreach (SqlServerResource? server in subscription.GetSqlServersAsync())
                    {
                        servers.Add(MapSqlServer(server));
                    }
                }
            }

            logger.LogInformation("Retrieved {Count} SQL servers", servers.Count);
            return servers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing SQL servers");
            throw;
        }
    }

    public async Task<ServerDto?> GetServerAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerCollection? servers = resourceGroup.GetSqlServers();

            SqlServerResource server = await servers.GetAsync(serverName);

            logger.LogInformation("Retrieved SQL server {ServerName}", serverName);
            return MapSqlServer(server);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("SQL server {ServerName} not found", serverName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting SQL server {ServerName}", serverName);
            throw;
        }
    }

    public async Task<bool> DeleteServerAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            await server.DeleteAsync(WaitUntil.Completed);

            logger.LogInformation("Deleted SQL server {ServerName}", serverName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("SQL server {ServerName} not found", serverName);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting SQL server {ServerName}", serverName);
            throw;
        }
    }

    #endregion

    #region Database Management

    public async Task<IEnumerable<DatabaseDto>> ListDatabasesAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            var databases = new List<DatabaseDto>();
            await foreach (SqlDatabaseResource? database in server.GetSqlDatabases().GetAllAsync())
            {
                // Skip system databases
                if (database.Data.Name != "master")
                {
                    databases.Add(MapSqlDatabase(database, serverName, resourceGroupName));
                }
            }

            logger.LogInformation("Retrieved {Count} databases from server {ServerName}", databases.Count, serverName);
            return databases;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing databases on server {ServerName}", serverName);
            throw;
        }
    }

    public async Task<DatabaseDto?> GetDatabaseAsync(string databaseName, string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            SqlDatabaseResource database = await server.GetSqlDatabases().GetAsync(databaseName);

            logger.LogInformation("Retrieved database {DatabaseName} from server {ServerName}", databaseName, serverName);
            return MapSqlDatabase(database, serverName, resourceGroupName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Database {DatabaseName} not found on server {ServerName}", databaseName, serverName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting database {DatabaseName} from server {ServerName}", databaseName, serverName);
            throw;
        }
    }

    public async Task<DatabaseDto> CreateDatabaseAsync(string databaseName, string serverName, string resourceGroupName,
        string? subscriptionId = null, string? edition = null, string? serviceObjective = null,
        long? maxSizeBytes = null, Dictionary<string, string>? tags = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            var databaseData = new SqlDatabaseData(server.Data.Location)
            {
                Sku = new SqlSku(serviceObjective ?? "Basic"),
                MaxSizeBytes = maxSizeBytes
            };

            if (tags is not null)
            {
                foreach (KeyValuePair<string, string> tag in tags)
                {
                    databaseData.Tags.Add(tag.Key, tag.Value);
                }
            }

            ArmOperation<SqlDatabaseResource> operation = await server.GetSqlDatabases().CreateOrUpdateAsync(
                WaitUntil.Completed, databaseName, databaseData);

            SqlDatabaseResource? database = operation.Value;

            logger.LogInformation("Created database {DatabaseName} on server {ServerName}", databaseName, serverName);
            return MapSqlDatabase(database, serverName, resourceGroupName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating database {DatabaseName} on server {ServerName}", databaseName, serverName);
            throw;
        }
    }

    public async Task<bool> DeleteDatabaseAsync(string databaseName, string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);
            SqlDatabaseResource database = await server.GetSqlDatabases().GetAsync(databaseName);

            await database.DeleteAsync(WaitUntil.Completed);

            logger.LogInformation("Deleted database {DatabaseName} from server {ServerName}", databaseName, serverName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Database {DatabaseName} not found on server {ServerName}", databaseName, serverName);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting database {DatabaseName} from server {ServerName}", databaseName, serverName);
            throw;
        }
    }

    #endregion

    #region Firewall Rules

    public async Task<IEnumerable<FirewallRuleDto>> ListFirewallRulesAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            var rules = new List<FirewallRuleDto>();
            await foreach (SqlFirewallRuleResource? rule in server.GetSqlFirewallRules().GetAllAsync())
            {
                rules.Add(MapFirewallRule(rule, serverName, resourceGroupName));
            }

            logger.LogInformation("Retrieved {Count} firewall rules from server {ServerName}", rules.Count, serverName);
            return rules;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing firewall rules on server {ServerName}", serverName);
            throw;
        }
    }

    public async Task<FirewallRuleDto?> GetFirewallRuleAsync(string ruleName, string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            SqlFirewallRuleResource rule = await server.GetSqlFirewallRules().GetAsync(ruleName);

            logger.LogInformation("Retrieved firewall rule {RuleName} from server {ServerName}", ruleName, serverName);
            return MapFirewallRule(rule, serverName, resourceGroupName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Firewall rule {RuleName} not found on server {ServerName}", ruleName, serverName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting firewall rule {RuleName} from server {ServerName}", ruleName, serverName);
            throw;
        }
    }

    public async Task<FirewallRuleDto> CreateFirewallRuleAsync(string ruleName, string serverName, string resourceGroupName,
        string startIpAddress, string endIpAddress, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            var ruleData = new SqlFirewallRuleData
            {
                StartIPAddress = startIpAddress,
                EndIPAddress = endIpAddress
            };

            ArmOperation<SqlFirewallRuleResource> operation = await server.GetSqlFirewallRules().CreateOrUpdateAsync(
                WaitUntil.Completed, ruleName, ruleData);

            SqlFirewallRuleResource? rule = operation.Value;

            logger.LogInformation("Created firewall rule {RuleName} on server {ServerName}", ruleName, serverName);
            return MapFirewallRule(rule, serverName, resourceGroupName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating firewall rule {RuleName} on server {ServerName}", ruleName, serverName);
            throw;
        }
    }

    public async Task<bool> DeleteFirewallRuleAsync(string ruleName, string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);
            SqlFirewallRuleResource rule = await server.GetSqlFirewallRules().GetAsync(ruleName);

            await rule.DeleteAsync(WaitUntil.Completed);

            logger.LogInformation("Deleted firewall rule {RuleName} from server {ServerName}", ruleName, serverName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Firewall rule {RuleName} not found on server {ServerName}", ruleName, serverName);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting firewall rule {RuleName} from server {ServerName}", ruleName, serverName);
            throw;
        }
    }

    #endregion

    #region Elastic Pools

    public async Task<IEnumerable<ElasticPoolDto>> ListElasticPoolsAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            var pools = new List<ElasticPoolDto>();
            await foreach (ElasticPoolResource? pool in server.GetElasticPools().GetAllAsync())
            {
                pools.Add(MapElasticPool(pool, serverName, resourceGroupName));
            }

            logger.LogInformation("Retrieved {Count} elastic pools from server {ServerName}", pools.Count, serverName);
            return pools;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing elastic pools on server {ServerName}", serverName);
            throw;
        }
    }

    public async Task<ElasticPoolDto?> GetElasticPoolAsync(string poolName, string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            SqlServerResource server = await resourceGroup.GetSqlServers().GetAsync(serverName);

            ElasticPoolResource pool = await server.GetElasticPools().GetAsync(poolName);

            logger.LogInformation("Retrieved elastic pool {PoolName} from server {ServerName}", poolName, serverName);
            return MapElasticPool(pool, serverName, resourceGroupName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Elastic pool {PoolName} not found on server {ServerName}", poolName, serverName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting elastic pool {PoolName} from server {ServerName}", poolName, serverName);
            throw;
        }
    }

    #endregion

    #region PostgreSQL

    public async Task<IEnumerable<ServerDto>> ListPostgreSqlServersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var servers = new List<ServerDto>();

            if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                if (!string.IsNullOrEmpty(resourceGroupName))
                {
                    ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
                    await foreach (PostgreSqlServerResource? server in resourceGroup.GetPostgreSqlServers().GetAllAsync())
                    {
                        servers.Add(MapPostgreSqlServer(server));
                    }
                }
                else
                {
                    await foreach (PostgreSqlServerResource? server in subscription.GetPostgreSqlServersAsync())
                    {
                        servers.Add(MapPostgreSqlServer(server));
                    }
                }
            }
            else
            {
                await foreach (SubscriptionResource? subscription in client.GetSubscriptions().GetAllAsync())
                {
                    await foreach (PostgreSqlServerResource? server in subscription.GetPostgreSqlServersAsync())
                    {
                        servers.Add(MapPostgreSqlServer(server));
                    }
                }
            }

            logger.LogInformation("Retrieved {Count} PostgreSQL servers", servers.Count);
            return servers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL servers");
            throw;
        }
    }

    public async Task<IEnumerable<DatabaseDto>> ListPostgreSqlDatabasesAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            PostgreSqlServerResource server = await resourceGroup.GetPostgreSqlServers().GetAsync(serverName);

            var databases = new List<DatabaseDto>();
            await foreach (PostgreSqlDatabaseResource? database in server.GetPostgreSqlDatabases().GetAllAsync())
            {
                databases.Add(MapPostgreSqlDatabase(database, serverName, resourceGroupName));
            }

            logger.LogInformation("Retrieved {Count} PostgreSQL databases from server {ServerName}", databases.Count, serverName);
            return databases;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL databases on server {ServerName}", serverName);
            throw;
        }
    }

    public async Task<bool> DeletePostgreSqlServerAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            // Try Flexible Server first (newer deployment model)
            try
            {
                PostgreSqlFlexibleServerResource flexibleServer = await resourceGroup.GetPostgreSqlFlexibleServers().GetAsync(serverName);
                await flexibleServer.DeleteAsync(WaitUntil.Completed);
                logger.LogInformation("Deleted PostgreSQL Flexible Server {ServerName}", serverName);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not a Flexible Server, try Single Server
                logger.LogDebug("PostgreSQL Flexible Server {ServerName} not found, trying Single Server", serverName);
            }

            // Try Single Server (older deployment model)
            try
            {
                PostgreSqlServerResource server = await resourceGroup.GetPostgreSqlServers().GetAsync(serverName);
                await server.DeleteAsync(WaitUntil.Completed);
                logger.LogInformation("Deleted PostgreSQL Single Server {ServerName}", serverName);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                logger.LogWarning("PostgreSQL server {ServerName} not found (neither Flexible nor Single Server)", serverName);
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting PostgreSQL server {ServerName}", serverName);
            throw;
        }
    }

    public async Task<IEnumerable<ServerDto>> ListPostgreSqlFlexibleServersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var servers = new List<ServerDto>();

            if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                if (!string.IsNullOrEmpty(resourceGroupName))
                {
                    ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
                    await foreach (PostgreSqlFlexibleServerResource? server in resourceGroup.GetPostgreSqlFlexibleServers().GetAllAsync())
                    {
                        servers.Add(MapPostgreSqlFlexibleServer(server));
                    }
                }
                else
                {
                    await foreach (ResourceGroupResource? resourceGroup in subscription.GetResourceGroups().GetAllAsync())
                    {
                        await foreach (PostgreSqlFlexibleServerResource? server in resourceGroup.GetPostgreSqlFlexibleServers().GetAllAsync())
                        {
                            servers.Add(MapPostgreSqlFlexibleServer(server));
                        }
                    }
                }
            }
            else
            {
                await foreach (SubscriptionResource? subscription in client.GetSubscriptions().GetAllAsync())
                {
                    await foreach (ResourceGroupResource? resourceGroup in subscription.GetResourceGroups().GetAllAsync())
                    {
                        await foreach (PostgreSqlFlexibleServerResource? server in resourceGroup.GetPostgreSqlFlexibleServers().GetAllAsync())
                        {
                            servers.Add(MapPostgreSqlFlexibleServer(server));
                        }
                    }
                }
            }

            logger.LogInformation("Retrieved {Count} PostgreSQL Flexible servers", servers.Count);
            return servers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing PostgreSQL Flexible servers");
            throw;
        }
    }

    #endregion

    #region MySQL

    public async Task<IEnumerable<ServerDto>> ListMySqlServersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var servers = new List<ServerDto>();

            if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                if (!string.IsNullOrEmpty(resourceGroupName))
                {
                    ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
                    await foreach (MySqlServerResource? server in resourceGroup.GetMySqlServers().GetAllAsync())
                    {
                        servers.Add(MapMySqlServer(server));
                    }
                }
                else
                {
                    await foreach (MySqlServerResource? server in subscription.GetMySqlServersAsync())
                    {
                        servers.Add(MapMySqlServer(server));
                    }
                }
            }
            else
            {
                await foreach (SubscriptionResource? subscription in client.GetSubscriptions().GetAllAsync())
                {
                    await foreach (MySqlServerResource? server in subscription.GetMySqlServersAsync())
                    {
                        servers.Add(MapMySqlServer(server));
                    }
                }
            }

            logger.LogInformation("Retrieved {Count} MySQL servers", servers.Count);
            return servers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing MySQL servers");
            throw;
        }
    }

    public async Task<IEnumerable<DatabaseDto>> ListMySqlDatabasesAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
            MySqlServerResource server = await resourceGroup.GetMySqlServers().GetAsync(serverName);

            var databases = new List<DatabaseDto>();
            await foreach (MySqlDatabaseResource? database in server.GetMySqlDatabases().GetAllAsync())
            {
                databases.Add(MapMySqlDatabase(database, serverName, resourceGroupName));
            }

            logger.LogInformation("Retrieved {Count} MySQL databases from server {ServerName}", databases.Count, serverName);
            return databases;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing MySQL databases on server {ServerName}", serverName);
            throw;
        }
    }

    public async Task<bool> DeleteMySqlServerAsync(string serverName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = (await client.GetDefaultSubscriptionAsync()).Data.SubscriptionId;
            }

            SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            // Try Flexible Server first (newer deployment model)
            try
            {
                MySqlFlexibleServerResource flexibleServer = await resourceGroup.GetMySqlFlexibleServers().GetAsync(serverName);
                await flexibleServer.DeleteAsync(WaitUntil.Completed);
                logger.LogInformation("Deleted MySQL Flexible Server {ServerName}", serverName);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Not a Flexible Server, try Single Server
                logger.LogDebug("MySQL Flexible Server {ServerName} not found, trying Single Server", serverName);
            }

            // Try Single Server (older deployment model)
            try
            {
                MySqlServerResource server = await resourceGroup.GetMySqlServers().GetAsync(serverName);
                await server.DeleteAsync(WaitUntil.Completed);
                logger.LogInformation("Deleted MySQL Single Server {ServerName}", serverName);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                logger.LogWarning("MySQL server {ServerName} not found (neither Flexible nor Single Server)", serverName);
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting MySQL server {ServerName}", serverName);
            throw;
        }
    }

    public async Task<IEnumerable<ServerDto>> ListMySqlFlexibleServersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient client = await armClientFactory.GetArmClientAsync();
            var servers = new List<ServerDto>();

            if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = client.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                if (!string.IsNullOrEmpty(resourceGroupName))
                {
                    ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);
                    await foreach (MySqlFlexibleServerResource? server in resourceGroup.GetMySqlFlexibleServers().GetAllAsync())
                    {
                        servers.Add(MapMySqlFlexibleServer(server));
                    }
                }
                else
                {
                    await foreach (ResourceGroupResource? resourceGroup in subscription.GetResourceGroups().GetAllAsync())
                    {
                        await foreach (MySqlFlexibleServerResource? server in resourceGroup.GetMySqlFlexibleServers().GetAllAsync())
                        {
                            servers.Add(MapMySqlFlexibleServer(server));
                        }
                    }
                }
            }
            else
            {
                await foreach (SubscriptionResource? subscription in client.GetSubscriptions().GetAllAsync())
                {
                    await foreach (ResourceGroupResource? resourceGroup in subscription.GetResourceGroups().GetAllAsync())
                    {
                        await foreach (MySqlFlexibleServerResource? server in resourceGroup.GetMySqlFlexibleServers().GetAllAsync())
                        {
                            servers.Add(MapMySqlFlexibleServer(server));
                        }
                    }
                }
            }

            logger.LogInformation("Retrieved {Count} MySQL Flexible servers", servers.Count);
            return servers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing MySQL Flexible servers");
            throw;
        }
    }

    #endregion

    #region Mapping Methods

    private static ServerDto MapSqlServer(SqlServerResource server)
    {
        return new ServerDto
        {
            Name = server.Data.Name,
            ResourceGroupName = server.Id.ResourceGroupName ?? string.Empty,
            Location = server.Data.Location.Name,
            Version = server.Data.Version ?? string.Empty,
            State = server.Data.State ?? "Unknown",
            FullyQualifiedDomainName = server.Data.FullyQualifiedDomainName,
            AdministratorLogin = server.Data.AdministratorLogin,
            PublicNetworkAccess = server.Data.PublicNetworkAccess == ServerNetworkAccessFlag.Enabled,
            MinimalTlsVersion = server.Data.MinimalTlsVersion,
            Tags = server.Data.Tags?.ToDictionary(t => t.Key, t => t.Value),
            ServerType = "AzureSQL"
        };
    }

    private static DatabaseDto MapSqlDatabase(SqlDatabaseResource database, string serverName, string resourceGroupName)
    {
        return new DatabaseDto
        {
            Name = database.Data.Name,
            ServerName = serverName,
            ResourceGroupName = resourceGroupName,
            Location = database.Data.Location.Name,
            Status = database.Data.Status?.ToString() ?? "Unknown",
            CollationName = database.Data.Collation,
            CreationDate = database.Data.CreatedOn?.DateTime,
            DatabaseId = database.Data.DatabaseId?.ToString(),
            Edition = database.Data.Sku?.Tier,
            ServiceObjectiveName = database.Data.Sku?.Name,
            MaxSizeBytes = database.Data.MaxSizeBytes,
            Sku = database.Data.Sku?.Name,
            Tags = database.Data.Tags?.ToDictionary(t => t.Key, t => t.Value),
            ElasticPoolName = database.Data.ElasticPoolId?.Name,
            DatabaseType = "AzureSQL"
        };
    }

    private static FirewallRuleDto MapFirewallRule(SqlFirewallRuleResource rule, string serverName, string resourceGroupName)
    {
        return new FirewallRuleDto
        {
            Name = rule.Data.Name,
            ServerName = serverName,
            ResourceGroupName = resourceGroupName,
            StartIpAddress = rule.Data.StartIPAddress ?? string.Empty,
            EndIpAddress = rule.Data.EndIPAddress ?? string.Empty
        };
    }

    private static ElasticPoolDto MapElasticPool(ElasticPoolResource pool, string serverName, string resourceGroupName)
    {
        return new ElasticPoolDto
        {
            Name = pool.Data.Name,
            ServerName = serverName,
            ResourceGroupName = resourceGroupName,
            Location = pool.Data.Location.Name,
            State = pool.Data.State?.ToString() ?? "Unknown",
            CreationDate = pool.Data.CreatedOn?.DateTime,
            Edition = pool.Data.Sku?.Tier,
            Dtu = pool.Data.Sku?.Capacity,
            DatabaseDtuMax = pool.Data.PerDatabaseSettings?.MaxCapacity is not null
                ? (int?)pool.Data.PerDatabaseSettings.MaxCapacity.Value : null,
            DatabaseDtuMin = pool.Data.PerDatabaseSettings?.MinCapacity is not null
                ? (int?)pool.Data.PerDatabaseSettings.MinCapacity.Value : null,
            StorageMB = pool.Data.MaxSizeBytes / (1024 * 1024),
            Tags = pool.Data.Tags?.ToDictionary(t => t.Key, t => t.Value)
        };
    }

    private static ServerDto MapPostgreSqlServer(PostgreSqlServerResource server)
    {
        return new ServerDto
        {
            Name = server.Data.Name,
            ResourceGroupName = server.Id.ResourceGroupName ?? string.Empty,
            Location = server.Data.Location.Name,
            Version = server.Data.Version?.ToString() ?? string.Empty,
            State = server.Data.UserVisibleState?.ToString() ?? "Unknown",
            FullyQualifiedDomainName = server.Data.FullyQualifiedDomainName,
            AdministratorLogin = server.Data.AdministratorLogin,
            PublicNetworkAccess = server.Data.PublicNetworkAccess?.ToString()?.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ?? false,
            MinimalTlsVersion = server.Data.MinimalTlsVersion?.ToString(),
            Tags = server.Data.Tags?.ToDictionary(t => t.Key, t => t.Value),
            ServerType = "PostgreSQL"
        };
    }

    private static DatabaseDto MapPostgreSqlDatabase(PostgreSqlDatabaseResource database, string serverName, string resourceGroupName)
    {
        return new DatabaseDto
        {
            Name = database.Data.Name,
            ServerName = serverName,
            ResourceGroupName = resourceGroupName,
            CollationName = database.Data.Collation,
            DatabaseType = "PostgreSQL"
        };
    }

    private static ServerDto MapPostgreSqlFlexibleServer(PostgreSqlFlexibleServerResource server)
    {
        return new ServerDto
        {
            Name = server.Data.Name,
            ResourceGroupName = server.Id.ResourceGroupName ?? string.Empty,
            Location = server.Data.Location.Name,
            Version = server.Data.Version?.ToString() ?? string.Empty,
            State = server.Data.State?.ToString() ?? "Unknown",
            FullyQualifiedDomainName = server.Data.FullyQualifiedDomainName,
            AdministratorLogin = server.Data.AdministratorLogin,
            PublicNetworkAccess = server.Data.Network?.PublicNetworkAccess?.ToString()?.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ?? false,
            MinimalTlsVersion = null, // Flexible Server doesn't expose MinimalTlsVersion in the same way
            Tags = server.Data.Tags?.ToDictionary(t => t.Key, t => t.Value),
            ServerType = "PostgreSQL-Flexible"
        };
    }


    private static ServerDto MapMySqlServer(MySqlServerResource server)
    {
        return new ServerDto
        {
            Name = server.Data.Name,
            ResourceGroupName = server.Id.ResourceGroupName ?? string.Empty,
            Location = server.Data.Location.Name,
            Version = server.Data.Version?.ToString() ?? string.Empty,
            State = server.Data.UserVisibleState?.ToString() ?? "Unknown",
            FullyQualifiedDomainName = server.Data.FullyQualifiedDomainName,
            AdministratorLogin = server.Data.AdministratorLogin,
            PublicNetworkAccess = server.Data.PublicNetworkAccess?.ToString()?.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ?? false,
            MinimalTlsVersion = server.Data.MinimalTlsVersion?.ToString(),
            Tags = server.Data.Tags?.ToDictionary(t => t.Key, t => t.Value),
            ServerType = "MySQL"
        };
    }

    private static DatabaseDto MapMySqlDatabase(MySqlDatabaseResource database, string serverName, string resourceGroupName)
    {
        return new DatabaseDto
        {
            Name = database.Data.Name,
            ServerName = serverName,
            ResourceGroupName = resourceGroupName,
            CollationName = database.Data.Collation,
            DatabaseType = "MySQL"
        };
    }


    private static ServerDto MapMySqlFlexibleServer(MySqlFlexibleServerResource server)
    {
        return new ServerDto
        {
            Name = server.Data.Name,
            ResourceGroupName = server.Id.ResourceGroupName ?? string.Empty,
            Location = server.Data.Location.Name,
            Version = server.Data.Version?.ToString() ?? string.Empty,
            State = server.Data.State?.ToString() ?? "Unknown",
            FullyQualifiedDomainName = server.Data.FullyQualifiedDomainName,
            AdministratorLogin = server.Data.AdministratorLogin,
            PublicNetworkAccess = server.Data.Network?.PublicNetworkAccess?.ToString()?.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ?? false,
            MinimalTlsVersion = null, // Flexible Server doesn't expose MinimalTlsVersion in the same way
            Tags = server.Data.Tags?.ToDictionary(t => t.Key, t => t.Value),
            ServerType = "MySQL-Flexible"
        };
    }


    #endregion
}
