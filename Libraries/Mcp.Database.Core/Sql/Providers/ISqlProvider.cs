using System.Data.Common;

namespace Mcp.Database.Core.Sql.Providers;

/// <summary>
/// Abstraction for different SQL database providers.
/// </summary>
public interface ISqlProvider
{
    /// <summary>
    /// Gets the provider name (e.g., "SqlServer", "PostgreSQL", "MySQL", "Sqlite").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Creates a new database connection using the provided connection string.
    /// </summary>
    /// <param name="connectionString">Connection string</param>
    /// <returns>Database connection instance</returns>
    DbConnection CreateConnection(string connectionString);

    /// <summary>
    /// Creates a new command for the connection.
    /// </summary>
    /// <param name="connection">Database connection</param>
    /// <returns>Database command instance</returns>
    DbCommand CreateCommand(DbConnection connection);

    /// <summary>
    /// Tests the connection by executing a simple query.
    /// </summary>
    /// <param name="connection">Connection to test</param>
    /// <returns>True if connection is healthy</returns>
    Task<bool> TestConnectionAsync(DbConnection connection);

    /// <summary>
    /// Gets the parameterized query syntax for this provider.
    /// </summary>
    /// <param name="parameterName">Parameter name without prefix</param>
    /// <returns>Formatted parameter (e.g., "@param" for SQL Server, "$1" for PostgreSQL)</returns>
    string GetParameterPlaceholder(string parameterName);

    /// <summary>
    /// Builds a connection string from components.
    /// </summary>
    /// <param name="server">Server address</param>
    /// <param name="database">Database name</param>
    /// <param name="username">Username (optional for integrated auth)</param>
    /// <param name="password">Password (optional for integrated auth)</param>
    /// <param name="additionalOptions">Additional connection string options</param>
    /// <returns>Connection string</returns>
    string BuildConnectionString(
        string server,
        string database,
        string? username = null,
        string? password = null,
        Dictionary<string, string>? additionalOptions = null);
}
