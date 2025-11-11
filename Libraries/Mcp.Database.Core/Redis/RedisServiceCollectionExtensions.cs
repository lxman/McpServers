using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Mcp.Common.Core.Environment;

namespace Mcp.Database.Core.Redis;

/// <summary>
/// Extension methods for registering Redis services in the DI container.
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers RedisConnectionManager as a singleton service.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">Optional configuration options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRedisConnectionManager(
        this IServiceCollection services,
        RedisConnectionOptions? options = null)
    {
        services.AddSingleton(provider =>
        {
            ILogger<RedisConnectionManager> logger = provider.GetRequiredService<ILogger<RedisConnectionManager>>();
            return new RedisConnectionManager(logger, options);
        });

        return services;
    }

    /// <summary>
    /// Registers RedisConnectionManager with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing Redis settings</param>
    /// <param name="configSection">Configuration section name (default: "Redis")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRedisConnectionManager(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "Redis")
    {
        var options = new RedisConnectionOptions();
        configuration.GetSection(configSection).Bind(options);

        return services.AddRedisConnectionManager(options);
    }

    /// <summary>
    /// Registers a simple singleton ConnectionMultiplexer with scoped IDatabase.
    /// This is useful for applications that only need a single Redis connection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">Redis connection string</param>
    /// <param name="database">Database number (0-15, default: 0)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRedisDatabase(
        this IServiceCollection services,
        string connectionString,
        int database = 0)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        if (database < 0 || database > 15)
            throw new ArgumentException("Database must be between 0 and 15", nameof(database));

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddScoped<IDatabase>(provider =>
            provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase(database));

        return services;
    }

    /// <summary>
    /// Registers a simple singleton ConnectionMultiplexer with scoped IDatabase using configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing Redis settings</param>
    /// <param name="configSection">Configuration section name (default: "Redis")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRedisDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "Redis")
    {
        string? connectionString = configuration.GetSection($"{configSection}:ConnectionString").Value;
        string? databaseStr = configuration.GetSection($"{configSection}:Database").Value;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Try environment variables as fallback
            connectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("REDIS_CONNECTION_STRING");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Redis connection string not found in configuration section '{configSection}:ConnectionString' or environment variable 'REDIS_CONNECTION_STRING'");

        int database = 0;
        if (!string.IsNullOrWhiteSpace(databaseStr) && int.TryParse(databaseStr, out int parsedDb))
        {
            database = parsedDb;
        }

        return services.AddRedisDatabase(connectionString, database);
    }

    /// <summary>
    /// Registers a simple singleton ConnectionMultiplexer with scoped IDatabase, automatically connecting from environment variables.
    /// Looks for REDIS_CONNECTION_STRING and REDIS_DATABASE environment variables.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are not set</exception>
    public static IServiceCollection AddRedisDatabaseFromEnvironment(this IServiceCollection services)
    {
        string? connectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("REDIS_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Environment variable 'REDIS_CONNECTION_STRING' is not set");

        string? databaseStr = EnvironmentReader.GetEnvironmentVariableWithFallback("REDIS_DATABASE");
        int database = 0;
        if (!string.IsNullOrWhiteSpace(databaseStr) && int.TryParse(databaseStr, out int parsedDb))
        {
            database = parsedDb;
        }

        return services.AddRedisDatabase(connectionString, database);
    }

    /// <summary>
    /// Registers Redis connection manager and automatically connects to all profiles with AutoConnect = true.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing Redis profiles</param>
    /// <param name="configSection">Configuration section name (default: "Redis")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRedisWithAutoConnect(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "Redis")
    {
        var options = new RedisConnectionOptions();
        configuration.GetSection(configSection).Bind(options);

        services.AddSingleton(provider =>
        {
            ILogger<RedisConnectionManager> logger = provider.GetRequiredService<ILogger<RedisConnectionManager>>();
            var manager = new RedisConnectionManager(logger, options);

            // Auto-connect to profiles
            List<RedisConnectionProfile> profiles = new();
            configuration.GetSection($"{configSection}:Profiles").Bind(profiles);

            foreach (RedisConnectionProfile profile in profiles.Where(p => p.AutoConnect))
            {
                _ = manager.AddConnectionAsync(profile.ConnectionName, profile.ConnectionString, profile.DefaultDatabase);
            }

            // Try environment variables for default connection
            string? envConnectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("REDIS_CONNECTION_STRING");
            string? envDatabaseStr = EnvironmentReader.GetEnvironmentVariableWithFallback("REDIS_DATABASE");

            if (!string.IsNullOrEmpty(envConnectionString))
            {
                int envDatabase = 0;
                if (!string.IsNullOrWhiteSpace(envDatabaseStr) && int.TryParse(envDatabaseStr, out int parsedDb))
                {
                    envDatabase = parsedDb;
                }

                _ = manager.AddConnectionAsync("default", envConnectionString, envDatabase);
            }

            return manager;
        });

        return services;
    }
}
