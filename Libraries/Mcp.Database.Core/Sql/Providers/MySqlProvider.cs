using System.Data;
using System.Data.Common;
using MySqlConnector;

namespace Mcp.Database.Core.Sql.Providers;

/// <summary>
/// MySQL/MariaDB provider implementation.
/// </summary>
public class MySqlProvider : ISqlProvider
{
    public string ProviderName => "MySQL";

    public DbConnection CreateConnection(string connectionString)
    {
        return new MySqlConnection(connectionString);
    }

    public DbCommand CreateCommand(DbConnection connection)
    {
        if (connection is not MySqlConnection mySqlConnection)
            throw new ArgumentException("Connection must be a MySqlConnection", nameof(connection));

        return mySqlConnection.CreateCommand();
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
        return $"@{parameterName}";
    }

    public string BuildConnectionString(
        string server,
        string database,
        string? username = null,
        string? password = null,
        Dictionary<string, string>? additionalOptions = null)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = server,
            Database = database
        };

        if (!string.IsNullOrEmpty(username))
            builder.UserID = username;

        if (!string.IsNullOrEmpty(password))
            builder.Password = password;

        // Apply additional options
        if (additionalOptions != null)
        {
            foreach (var kvp in additionalOptions)
            {
                builder[kvp.Key] = kvp.Value;
            }
        }

        return builder.ConnectionString;
    }
}
