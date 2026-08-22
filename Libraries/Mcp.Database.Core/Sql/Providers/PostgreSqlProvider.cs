using System.Data;
using System.Data.Common;
using Npgsql;

namespace Mcp.Database.Core.Sql.Providers;

/// <summary>
/// PostgreSQL provider implementation.
/// </summary>
public class PostgreSqlProvider : ISqlProvider
{
    /// <inheritdoc />
    public string ProviderName => "PostgreSQL";

    /// <inheritdoc />
    public DbConnection CreateConnection(string connectionString)
    {
        return new NpgsqlConnection(connectionString);
    }

    /// <inheritdoc />
    public DbCommand CreateCommand(DbConnection connection)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
            throw new ArgumentException("Connection must be a NpgsqlConnection", nameof(connection));

        return npgsqlConnection.CreateCommand();
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
    public string GetParameterPlaceholder(string parameterName)
    {
        // PostgreSQL uses $1, $2, etc., but for named parameters we'll use @ syntax
        // Npgsql supports both
        return $"@{parameterName}";
    }

    /// <inheritdoc />
    public string BuildConnectionString(
        string server,
        string database,
        string? username = null,
        string? password = null,
        Dictionary<string, string>? additionalOptions = null)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = server,
            Database = database
        };

        if (!string.IsNullOrEmpty(username))
            builder.Username = username;

        if (!string.IsNullOrEmpty(password))
            builder.Password = password;

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
