using System.Data;
using System.Data.Common;
using Npgsql;

namespace Mcp.Database.Core.Sql.Providers;

/// <summary>
/// PostgreSQL provider implementation.
/// </summary>
public class PostgreSqlProvider : ISqlProvider
{
    public string ProviderName => "PostgreSQL";

    public DbConnection CreateConnection(string connectionString)
    {
        return new NpgsqlConnection(connectionString);
    }

    public DbCommand CreateCommand(DbConnection connection)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
            throw new ArgumentException("Connection must be a NpgsqlConnection", nameof(connection));

        return npgsqlConnection.CreateCommand();
    }

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

    public string GetParameterPlaceholder(string parameterName)
    {
        // PostgreSQL uses $1, $2, etc., but for named parameters we'll use @ syntax
        // Npgsql supports both
        return $"@{parameterName}";
    }

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
