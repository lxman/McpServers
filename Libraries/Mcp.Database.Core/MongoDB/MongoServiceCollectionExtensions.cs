using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Mcp.Common.Core.Environment;

namespace Mcp.Database.Core.MongoDB;

/// <summary>
/// Extension methods for registering MongoDB services in the DI container.
/// </summary>
public static class MongoServiceCollectionExtensions
{
    /// <summary>
    /// Registers MongoConnectionManager as a singleton service.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">Optional configuration options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMongoConnectionManager(
        this IServiceCollection services,
        MongoConnectionOptions? options = null)
    {
        services.AddSingleton(provider =>
        {
            ILogger<MongoConnectionManager> logger = provider.GetRequiredService<ILogger<MongoConnectionManager>>();
            return new MongoConnectionManager(logger, options);
        });

        return services;
    }

    /// <summary>
    /// Registers MongoConnectionManager with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing MongoDB settings</param>
    /// <param name="configSection">Configuration section name (default: "MongoDB")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMongoConnectionManager(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "MongoDB")
    {
        var options = new MongoConnectionOptions();
        configuration.GetSection(configSection).Bind(options);

        return services.AddMongoConnectionManager(options);
    }

    /// <summary>
    /// Registers a simple singleton MongoClient with scoped IMongoDatabase.
    /// This is useful for applications that only need a single MongoDB connection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">MongoDB connection string</param>
    /// <param name="databaseName">Database name</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMongoDatabase(
        this IServiceCollection services,
        string connectionString,
        string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty", nameof(databaseName));

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddScoped<IMongoDatabase>(provider =>
            provider.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

        return services;
    }

    /// <summary>
    /// Registers a simple singleton MongoClient with scoped IMongoDatabase using configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing MongoDB settings</param>
    /// <param name="configSection">Configuration section name (default: "MongoDB")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMongoDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "MongoDB")
    {
        string? connectionString = configuration.GetSection($"{configSection}:ConnectionString").Value;
        string? databaseName = configuration.GetSection($"{configSection}:DatabaseName").Value;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Try environment variables as fallback
            connectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_CONNECTION_STRING");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            // Try environment variables as fallback
            databaseName = EnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_DATABASE");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"MongoDB connection string not found in configuration section '{configSection}:ConnectionString' or environment variable 'MONGODB_CONNECTION_STRING'");

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException($"MongoDB database name not found in configuration section '{configSection}:DatabaseName' or environment variable 'MONGODB_DATABASE'");

        return services.AddMongoDatabase(connectionString, databaseName);
    }

    /// <summary>
    /// Registers a simple singleton MongoClient with scoped IMongoDatabase, automatically connecting from environment variables.
    /// Looks for MONGODB_CONNECTION_STRING and MONGODB_DATABASE environment variables.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are not set</exception>
    public static IServiceCollection AddMongoDatabaseFromEnvironment(this IServiceCollection services)
    {
        string? connectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_CONNECTION_STRING");
        string? databaseName = EnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_DATABASE");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Environment variable 'MONGODB_CONNECTION_STRING' is not set");

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Environment variable 'MONGODB_DATABASE' is not set");

        return services.AddMongoDatabase(connectionString, databaseName);
    }

    /// <summary>
    /// Registers MongoDB connection manager and automatically connects to all profiles with AutoConnect = true.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration containing MongoDB profiles</param>
    /// <param name="configSection">Configuration section name (default: "MongoDB")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMongoWithAutoConnect(
        this IServiceCollection services,
        IConfiguration configuration,
        string configSection = "MongoDB")
    {
        var options = new MongoConnectionOptions();
        configuration.GetSection(configSection).Bind(options);

        services.AddSingleton(provider =>
        {
            ILogger<MongoConnectionManager> logger = provider.GetRequiredService<ILogger<MongoConnectionManager>>();
            var manager = new MongoConnectionManager(logger, options);

            // Auto-connect to profiles
            List<MongoConnectionProfile> profiles = new();
            configuration.GetSection($"{configSection}:Profiles").Bind(profiles);

            foreach (MongoConnectionProfile profile in profiles.Where(p => p.AutoConnect))
            {
                _ = manager.AddConnectionAsync(profile.ConnectionName, profile.ConnectionString, profile.DefaultDatabase);
            }

            // Try environment variables for default connection
            string? envConnectionString = EnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_CONNECTION_STRING");
            string? envDatabase = EnvironmentReader.GetEnvironmentVariableWithFallback("MONGODB_DATABASE");

            if (!string.IsNullOrEmpty(envConnectionString) && !string.IsNullOrEmpty(envDatabase))
            {
                _ = manager.AddConnectionAsync("default", envConnectionString, envDatabase);
            }

            return manager;
        });

        return services;
    }
}
