# Mcp.Database.Core

Unified database connection management library for MCP servers, supporting MongoDB, Redis, and SQL databases (SQL Server, PostgreSQL, MySQL).

## Features

- **Multi-Database Support**: MongoDB, Redis, SQL Server, PostgreSQL, MySQL
- **Connection Pooling**: Thread-safe connection management using ConcurrentDictionary
- **Health Monitoring**: Automatic periodic health checks with configurable intervals
- **Auto-Cleanup**: Removes unhealthy connections automatically
- **Multiple Connection Patterns**: Simple singleton or advanced multi-server management
- **Environment Variable Support**: Automatic configuration from environment variables with Windows Registry fallback
- **Dependency Injection**: Built-in DI extensions for easy integration
- **Provider Abstraction**: Unified SQL interface across different database providers
- **Connection String Building**: Helper methods for building connection strings programmatically

## Installation

Add a project reference to your `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="..\Mcp.Database.Core\Mcp.Database.Core.csproj" />
</ItemGroup>
```

## MongoDB Usage

### Simple Pattern (Singleton)

```csharp
using Mcp.Database.Core.MongoDB;
using Microsoft.Extensions.DependencyInjection;

// Register MongoDB with connection string
services.AddMongoDatabase(
    "mongodb://localhost:27017",
    "myDatabase"
);

// Or from configuration
services.AddMongoDatabase(configuration, "MongoDB");

// Or from environment variables (MONGO_CONNECTION_STRING, MONGO_DATABASE)
services.AddMongoDatabaseFromEnvironment();

// Use in your services
public class MyService
{
    private readonly IMongoDatabase _database;

    public MyService(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task DoWork()
    {
        var collection = _database.GetCollection<MyDocument>("myCollection");
        // ... use collection
    }
}
```

### Advanced Pattern (Connection Manager)

```csharp
using Mcp.Database.Core.MongoDB;

// Register connection manager
services.AddMongoConnectionManager(new MongoConnectionOptions
{
    HealthCheckEnabled = true,
    HealthCheckInterval = TimeSpan.FromMinutes(5),
    AutoCleanupUnhealthyConnections = true
});

// Or with auto-connect from configuration
services.AddMongoWithAutoConnect(configuration, "MongoDB");

// Use in your services
public class MyService
{
    private readonly MongoConnectionManager _manager;

    public MyService(MongoConnectionManager manager)
    {
        _manager = manager;
    }

    public async Task DoWork()
    {
        // Add connections dynamically
        await _manager.AddConnectionAsync("server1", "mongodb://localhost:27017", "db1");
        await _manager.AddConnectionAsync("server2", "mongodb://otherhost:27017", "db2");

        // Get database instance
        var db1 = _manager.GetDatabase("server1");
        var db2 = _manager.GetDatabase("server2");

        // List databases
        var databases = await _manager.ListDatabasesAsync("server1");

        // Switch database
        await _manager.SwitchDatabaseAsync("server1", "newDatabase");

        // Check connection health
        bool isHealthy = await _manager.PingConnectionAsync("server1");

        // Get connection status
        string status = _manager.GetConnectionsStatus();
    }
}
```

### Configuration Example (appsettings.json)

```json
{
  "MongoDB": {
    "HealthCheckEnabled": true,
    "HealthCheckInterval": "00:05:00",
    "DefaultConnectionName": "default",
    "Profiles": [
      {
        "ConnectionName": "primary",
        "ConnectionString": "mongodb://localhost:27017",
        "DefaultDatabase": "myApp",
        "AutoConnect": true
      },
      {
        "ConnectionName": "analytics",
        "ConnectionString": "mongodb://analytics-host:27017",
        "DefaultDatabase": "analytics",
        "AutoConnect": true
      }
    ]
  }
}
```

## Redis Usage

### Simple Pattern (Singleton)

```csharp
using Mcp.Database.Core.Redis;
using Microsoft.Extensions.DependencyInjection;

// Register Redis with connection string
services.AddRedisDatabase(
    "localhost:6379",
    database: 0
);

// Or from configuration
services.AddRedisDatabase(configuration, "Redis");

// Or from environment variables (REDIS_CONNECTION_STRING, REDIS_DATABASE)
services.AddRedisDatabaseFromEnvironment();

// Use in your services
public class MyService
{
    private readonly IDatabase _redis;

    public MyService(IDatabase redis)
    {
        _redis = redis;
    }

    public async Task DoWork()
    {
        await _redis.StringSetAsync("key", "value");
        var value = await _redis.StringGetAsync("key");
    }
}
```

### Advanced Pattern (Connection Manager)

```csharp
using Mcp.Database.Core.Redis;

// Register connection manager
services.AddRedisConnectionManager(new RedisConnectionOptions
{
    HealthCheckEnabled = true,
    HealthCheckInterval = TimeSpan.FromMinutes(5),
    DefaultDatabase = 0,
    AllowAdmin = false
});

// Or with auto-connect from configuration
services.AddRedisWithAutoConnect(configuration, "Redis");

// Use in your services
public class MyService
{
    private readonly RedisConnectionManager _manager;

    public MyService(RedisConnectionManager manager)
    {
        _manager = manager;
    }

    public async Task DoWork()
    {
        // Add connections dynamically
        await _manager.AddConnectionAsync("cache", "localhost:6379", database: 0);
        await _manager.AddConnectionAsync("session", "session-host:6379", database: 1);

        // Get database instance
        var cacheDb = _manager.GetDatabase("cache");
        var sessionDb = _manager.GetDatabase("session");

        // Switch database (0-15)
        await _manager.SelectDatabaseAsync("cache", 2);

        // Check connection health
        bool isHealthy = await _manager.PingConnectionAsync("cache");

        // Get connection status
        string status = _manager.GetConnectionsStatus();
    }
}
```

### Configuration Example (appsettings.json)

```json
{
  "Redis": {
    "HealthCheckEnabled": true,
    "HealthCheckInterval": "00:05:00",
    "DefaultDatabase": 0,
    "AllowAdmin": false,
    "Profiles": [
      {
        "ConnectionName": "cache",
        "ConnectionString": "localhost:6379",
        "DefaultDatabase": 0,
        "AutoConnect": true
      },
      {
        "ConnectionName": "session",
        "ConnectionString": "session-host:6379",
        "DefaultDatabase": 1,
        "AutoConnect": true
      }
    ]
  }
}
```

## SQL Usage

### Simple Pattern (Singleton)

```csharp
using Mcp.Database.Core.Sql;
using Microsoft.Extensions.DependencyInjection;

// Register SQL connection with provider
services.AddSqlConnection(
    "SqlServer",
    "Server=localhost;Database=myDb;Integrated Security=true;"
);

// Or from configuration
services.AddSqlConnection(configuration, "Sql");

// Or from environment variables (SQL_PROVIDER, SQL_CONNECTION_STRING)
services.AddSqlConnectionFromEnvironment();

// Use in your services
public class MyService
{
    private readonly DbConnection _connection;
    private readonly ISqlProvider _provider;

    public MyService(DbConnection connection, ISqlProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    public async Task DoWork()
    {
        using var command = _provider.CreateCommand(_connection);
        command.CommandText = "SELECT * FROM Users";
        using var reader = await command.ExecuteReaderAsync();
        // ... process results
    }
}
```

### Advanced Pattern (Connection Manager)

```csharp
using Mcp.Database.Core.Sql;

// Register connection manager
services.AddSqlConnectionManager(new SqlConnectionOptions
{
    HealthCheckEnabled = true,
    HealthCheckInterval = TimeSpan.FromMinutes(5),
    AutoCleanupUnhealthyConnections = true
});

// Or with auto-connect from configuration
services.AddSqlWithAutoConnect(configuration, "Sql");

// Use in your services
public class MyService
{
    private readonly SqlConnectionManager _manager;

    public MyService(SqlConnectionManager manager)
    {
        _manager = manager;
    }

    public async Task DoWork()
    {
        // Add connections dynamically
        await _manager.AddConnectionAsync(
            "primary",
            "SqlServer",
            "Server=localhost;Database=myDb;Integrated Security=true;"
        );

        await _manager.AddConnectionAsync(
            "analytics",
            "PostgreSQL",
            "Host=pg-host;Database=analytics;Username=user;Password=pass"
        );

        await _manager.AddConnectionAsync(
            "reporting",
            "MySQL",
            "Server=mysql-host;Database=reports;Uid=user;Pwd=pass;"
        );

        // Get connection and provider
        var connection = _manager.GetConnection("primary");
        var provider = _manager.GetProvider("primary");

        // Create and execute command
        using var command = _manager.CreateCommand("primary");
        if (command != null)
        {
            command.CommandText = "SELECT * FROM Users";
            using var reader = await command.ExecuteReaderAsync();
            // ... process results
        }

        // Check connection health
        bool isHealthy = await _manager.PingConnectionAsync("primary");

        // Get available providers
        var providers = _manager.GetAvailableProviders(); // ["SqlServer", "PostgreSQL", "MySQL"]

        // Get connection status
        string status = _manager.GetConnectionsStatus();
    }
}
```

### Configuration Example (appsettings.json)

```json
{
  "Sql": {
    "HealthCheckEnabled": true,
    "HealthCheckInterval": "00:05:00",
    "DefaultConnectionName": "default",
    "Profiles": [
      {
        "ConnectionName": "primary",
        "Provider": "SqlServer",
        "ConnectionString": "Server=localhost;Database=myDb;Integrated Security=true;",
        "AutoConnect": true
      },
      {
        "ConnectionName": "analytics",
        "Provider": "PostgreSQL",
        "ConnectionString": "Host=pg-host;Database=analytics;Username=user;Password=pass",
        "AutoConnect": true
      },
      {
        "ConnectionName": "reporting",
        "Provider": "MySQL",
        "ConnectionString": "Server=mysql-host;Database=reports;Uid=user;Pwd=pass;",
        "AutoConnect": false
      }
    ]
  }
}
```

### SQL Provider Features

Each SQL provider implements the `ISqlProvider` interface:

```csharp
public interface ISqlProvider
{
    string ProviderName { get; }
    DbConnection CreateConnection(string connectionString);
    DbCommand CreateCommand(DbConnection connection);
    Task<bool> TestConnectionAsync(DbConnection connection);
    string GetParameterPlaceholder(string parameterName);
    string BuildConnectionString(string server, string database,
        string? username = null, string? password = null,
        Dictionary<string, string>? additionalOptions = null);
}
```

Supported providers:
- **SqlServer** (Microsoft.Data.SqlClient)
- **PostgreSQL** (Npgsql)
- **MySQL** (MySqlConnector)

## Environment Variables

All database types support environment variable configuration with Windows Registry fallback:

### MongoDB
- `MONGO_CONNECTION_STRING`: MongoDB connection string
- `MONGO_DATABASE`: Default database name

### Redis
- `REDIS_CONNECTION_STRING`: Redis connection string
- `REDIS_DATABASE`: Database number (0-15)

### SQL
- `SQL_PROVIDER`: Provider name (SqlServer, PostgreSQL, MySQL)
- `SQL_CONNECTION_STRING`: SQL connection string

The library uses `EnvironmentReader` from `Mcp.Common.Core` which:
1. First checks `System.Environment.GetEnvironmentVariable()` (process environment)
2. Falls back to Windows Registry (HKEY_CURRENT_USER, then HKEY_LOCAL_MACHINE)

This ensures variables are found even if set after the process started.

## Connection Health Monitoring

All connection managers support automatic health monitoring:

```csharp
var options = new MongoConnectionOptions
{
    HealthCheckEnabled = true,
    HealthCheckInterval = TimeSpan.FromMinutes(5),
    AutoCleanupUnhealthyConnections = true
};
```

Features:
- **Periodic Ping**: Automatically pings all connections at specified intervals
- **Health Tracking**: Tracks last ping time and duration for each connection
- **Auto-Cleanup**: Removes unhealthy connections when enabled
- **Manual Health Checks**: `PingConnectionAsync()` and `IsConnected()` methods

## Connection Information

All managers provide detailed connection information:

```csharp
var info = manager.GetConnectionInfo("myConnection");
// {
//   "ConnectionName": "myConnection",
//   "DatabaseName": "myDatabase",
//   "DatabaseType": "MongoDB",
//   "ConnectedAt": "2025-01-10T12:00:00Z",
//   "LastPing": "2025-01-10T12:05:00Z",
//   "IsHealthy": true,
//   "LastPingMs": 15.3
// }

var status = manager.GetConnectionsStatus();
// {
//   "defaultConnection": "default",
//   "totalConnections": 3,
//   "healthyConnections": 2,
//   "connections": [...]
// }
```

## Thread Safety

All connection managers use `ConcurrentDictionary` for thread-safe connection pooling and are safe to use from multiple threads concurrently.

## Disposal

All connection managers implement `IDisposable`:

```csharp
using var manager = new MongoConnectionManager(logger);
// ... use manager
// Automatically disposes all connections and stops health checks
```

## Dependencies

- MongoDB.Driver 3.5.0
- StackExchange.Redis 2.8.16
- Microsoft.Data.SqlClient 5.2.2
- Npgsql 9.0.2
- MySqlConnector 2.4.0
- Microsoft.Extensions.* 10.0.0-rc.2
- Mcp.Common.Core (for EnvironmentReader)
- Mcp.DependencyInjection.Core

## License

Part of the MCP Servers project.
