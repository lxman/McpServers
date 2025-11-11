using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using Mcp.Common.Core.Environment;
using Mcp.Database.Core.Sql.Providers;

namespace Mcp.Database.Core.Sql;

/// <summary>
/// Extension methods for registering SQL services in the DI container.
/// </summary>
public static class SqlServiceCollectionExtensions
{
    /// <summary>
    /// Registers SqlConnectionManager as a singleton service.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">Optional configuration options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSqlConnectionManager(
        this IServiceCollection services,
        SqlConnectionOptions? options = null)
    {
        services.AddSingleton(provider =>
        {
            ILogger<SqlConnectionManager> logger = provider.GetRequiredService<ILogger<SqlConnectionManager>>();
            return new SqlConnectionManager(logger, options);
        });

        return services;
    }

    /// <summary>
    /// Registers SqlConnectionManager with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing SQL settings</param>
    /// <param name="configSection">Configuration section name (default: "Sql")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSqlConnectionManager(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "Sql")
    {
        var options = new SqlConnectionOptions();
        configuration.GetSection(configSection).Bind(options);

        return services.AddSqlConnectionManager(options);
    }

    /// <summary>
    /// Registers a simple singleton DbConnection with the specified provider.
    /// This is useful for applications that only need a single SQL connection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="providerName">Provider name (SqlServer, PostgreSQL, MySQL)</param>
    /// <param name="connectionString">SQL connection string</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSqlConnection(
        this IServiceCollection services,
        string providerName,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be empty", nameof(providerName));

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        ISqlProvider provider = GetProvider(providerName);

        services.AddSingleton<DbConnection>(_ =>
        {
            DbConnection connection = provider.CreateConnection(connectionString);
            connection.Open();
            return connection;
        });

        services.AddSingleton<ISqlProvider>(provider);

        return services;
    }

    /// <summary>
    /// Registers a simple singleton DbConnection using configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing SQL settings</param>
    /// <param name="configSection">Configuration section name (default: "Sql")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSqlConnection(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "Sql")
    {
        string? providerName = configuration.GetSection($"{configSection}:Provider").Value;
        string? connectionString = configuration.GetSection($"{configSection}:ConnectionString").Value;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Try environment variables as fallback
            connectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("SQL_CONNECTION_STRING");
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            providerName = EnvironmentReader.GetEnvironmentVariableWithFallback("SQL_PROVIDER") ?? "SqlServer";
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"SQL connection string not found in configuration section '{configSection}:ConnectionString' or environment variable 'SQL_CONNECTION_STRING'");

        return services.AddSqlConnection(providerName, connectionString);
    }

    /// <summary>
    /// Registers a simple singleton DbConnection, automatically connecting from environment variables.
    /// Looks for SQL_PROVIDER and SQL_CONNECTION_STRING environment variables.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are not set</exception>
    public static IServiceCollection AddSqlConnectionFromEnvironment(this IServiceCollection services)
    {
        string? connectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("SQL_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Environment variable 'SQL_CONNECTION_STRING' is not set");

        string providerName = EnvironmentReader.GetEnvironmentVariableWithFallback("SQL_PROVIDER") ?? "SqlServer";

        return services.AddSqlConnection(providerName, connectionString);
    }

    /// <summary>
    /// Registers SQL connection manager and automatically connects to all profiles with AutoConnect = true.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing SQL profiles</param>
    /// <param name="configSection">Configuration section name (default: "Sql")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSqlWithAutoConnect(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "Sql")
    {
        var options = new SqlConnectionOptions();
        configuration.GetSection(configSection).Bind(options);

        services.AddSingleton(provider =>
        {
            ILogger<SqlConnectionManager> logger = provider.GetRequiredService<ILogger<SqlConnectionManager>>();
            var manager = new SqlConnectionManager(logger, options);

            // Auto-connect to profiles
            List<SqlConnectionProfile> profiles = new();
            configuration.GetSection($"{configSection}:Profiles").Bind(profiles);

            foreach (SqlConnectionProfile profile in profiles.Where(p => p.AutoConnect))
            {
                _ = manager.AddConnectionAsync(profile.ConnectionName, profile.Provider, profile.ConnectionString);
            }

            // Try environment variables for default connection
            string? envConnectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("SQL_CONNECTION_STRING");
            string? envProvider = EnvironmentReader.GetEnvironmentVariableWithFallback("SQL_PROVIDER");

            if (!string.IsNullOrEmpty(envConnectionString) && !string.IsNullOrEmpty(envProvider))
            {
                _ = manager.AddConnectionAsync("default", envProvider, envConnectionString);
            }

            return manager;
        });

        return services;
    }

    /// <summary>
    /// Helper method to get provider instance by name.
    /// </summary>
    private static ISqlProvider GetProvider(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "sqlserver" or "mssql" or "sql" => new SqlServerProvider(),
            "postgresql" or "postgres" or "psql" => new PostgreSqlProvider(),
            "mysql" or "mariadb" => new MySqlProvider(),
            _ => throw new ArgumentException($"Unknown provider: {providerName}. Available providers: SqlServer, PostgreSQL, MySQL", nameof(providerName))
        };
    }
}
