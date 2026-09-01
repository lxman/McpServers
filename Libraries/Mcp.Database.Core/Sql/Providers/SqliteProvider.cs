using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Mcp.Database.Core.Sql.Providers;

/// <summary>
/// SQLite provider implementation.
/// </summary>
/// <remarks>
/// SqlServer.Core carried a SQLite provider against its own <c>IDbProvider</c> abstraction while
/// this stack -- the one SqlConnectionManager actually uses -- had none, so every SQLite
/// connection in configuration was unopenable even though ISqlProvider's own documentation lists
/// SQLite as an expected provider name. This closes that gap.
/// </remarks>
public class SqliteProvider : ISqlProvider
{
    /// <inheritdoc />
    public string ProviderName => "Sqlite";

    /// <inheritdoc />
    public DbConnection CreateConnection(string connectionString)
    {
        return new SqliteConnection(connectionString);
    }

    /// <inheritdoc />
    public DbCommand CreateCommand(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
            throw new ArgumentException("Connection must be a SqliteConnection", nameof(connection));

        return sqliteConnection.CreateCommand();
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(DbConnection connection)
    {
        if (connection == null)
            return false;

        try
        {
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandType = CommandType.Text;

            object? result = await command.ExecuteScalarAsync();
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Dapper emits '@' placeholders and Microsoft.Data.Sqlite accepts them, so this matches the
    /// other providers rather than using SQLite's '$' form.
    /// </remarks>
    public string GetParameterPlaceholder(string parameterName)
    {
        return $"@{parameterName}";
    }

    /// <inheritdoc />
    /// <remarks>
    /// SQLite has no server: the database IS the file. <paramref name="database"/> is taken as the
    /// file path, falling back to <paramref name="server"/> when only that was supplied.
    /// Credentials are ignored except as an encryption password.
    /// </remarks>
    public string BuildConnectionString(
        string server,
        string database,
        string? username = null,
        string? password = null,
        Dictionary<string, string>? additionalOptions = null)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = !string.IsNullOrWhiteSpace(database) ? database : server
        };

        if (!string.IsNullOrEmpty(password))
        {
            builder.Password = password;
        }

        // Apply additional options
        if (additionalOptions != null)
        {
            foreach (KeyValuePair<string, string> kvp in additionalOptions)
            {
                builder[kvp.Key] = kvp.Value;
            }
        }

        return builder.ConnectionString;
    }
}
