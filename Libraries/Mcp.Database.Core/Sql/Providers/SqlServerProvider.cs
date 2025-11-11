using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Mcp.Database.Core.Sql.Providers;

/// <summary>
/// SQL Server provider implementation.
/// </summary>
public class SqlServerProvider : ISqlProvider
{
    public string ProviderName => "SqlServer";

    public DbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }

    public DbCommand CreateCommand(DbConnection connection)
    {
        if (connection is not SqlConnection sqlConnection)
            throw new ArgumentException("Connection must be a SqlConnection", nameof(connection));

        return sqlConnection.CreateCommand();
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
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database
        };

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            builder.UserID = username;
            builder.Password = password;
        }
        else
        {
            builder.IntegratedSecurity = true;
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
