using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using Azure.Core;
using AzureServer.Services.Core;
using AzureServer.Services.Sql.QueryExecution.Models;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace AzureServer.Services.Sql.QueryExecution;

public class SqlQueryService(
    ArmClientFactory armClientFactory,
    ILogger<SqlQueryService> logger) : ISqlQueryService
{
    public async Task<QueryResultDto> ExecuteQueryAsync(ConnectionInfoDto connectionInfo, string query, int maxRows = 1000, int timeoutSeconds = 30)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new QueryResultDto { Success = false };

        try
        {
            await using DbConnection connection = await CreateConnectionAsync(connectionInfo);
            await connection.OpenAsync();

            await using DbCommand command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = timeoutSeconds;
            command.CommandType = CommandType.Text;

            await using DbDataReader reader = await command.ExecuteReaderAsync();

            // Get column names
            for (var i = 0; i < reader.FieldCount; i++)
            {
                result.ColumnNames.Add(reader.GetName(i));
            }

            // Read rows
            var rowCount = 0;
            while (await reader.ReadAsync() && rowCount < maxRows)
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = value;
                }
                result.Rows.Add(row);
                rowCount++;
            }

            stopwatch.Stop();
            result.Success = true;
            result.Message = $"Query executed successfully. Returned {rowCount} rows.";
            result.RowsAffected = rowCount;
            result.ExecutionTime = stopwatch.Elapsed;

            logger.LogInformation("Executed query on {DatabaseType} database {Database}. Rows: {Rows}, Time: {Time}ms",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName, rowCount, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ExecutionTime = stopwatch.Elapsed;

            logger.LogError(ex, "Error executing query on {DatabaseType} database {Database}",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName);

            return result;
        }
    }

    public async Task<QueryResultDto> ExecuteNonQueryAsync(ConnectionInfoDto connectionInfo, string command, int timeoutSeconds = 30)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new QueryResultDto { Success = false };

        try
        {
            await using DbConnection connection = await CreateConnectionAsync(connectionInfo);
            await connection.OpenAsync();

            await using DbCommand dbCommand = connection.CreateCommand();
            dbCommand.CommandText = command;
            dbCommand.CommandTimeout = timeoutSeconds;
            dbCommand.CommandType = CommandType.Text;

            int rowsAffected = await dbCommand.ExecuteNonQueryAsync();

            stopwatch.Stop();
            result.Success = true;
            result.Message = $"Command executed successfully. {rowsAffected} rows affected.";
            result.RowsAffected = rowsAffected;
            result.ExecutionTime = stopwatch.Elapsed;

            logger.LogInformation("Executed non-query command on {DatabaseType} database {Database}. Rows affected: {Rows}, Time: {Time}ms",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName, rowsAffected, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ExecutionTime = stopwatch.Elapsed;

            logger.LogError(ex, "Error executing non-query command on {DatabaseType} database {Database}",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName);

            return result;
        }
    }

    public async Task<object?> ExecuteScalarAsync(ConnectionInfoDto connectionInfo, string query, int timeoutSeconds = 30)
    {
        try
        {
            await using DbConnection connection = await CreateConnectionAsync(connectionInfo);
            await connection.OpenAsync();

            await using DbCommand command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = timeoutSeconds;
            command.CommandType = CommandType.Text;

            object? result = await command.ExecuteScalarAsync();

            logger.LogInformation("Executed scalar query on {DatabaseType} database {Database}",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing scalar query on {DatabaseType} database {Database}",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(ConnectionInfoDto connectionInfo)
    {
        try
        {
            await using DbConnection connection = await CreateConnectionAsync(connectionInfo);
            await connection.OpenAsync();
            
            logger.LogInformation("Successfully tested connection to {DatabaseType} database {Database}",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName);
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to connect to {DatabaseType} database {Database}",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName);
            return false;
        }
    }

    public async Task<QueryResultDto> GetSchemaInfoAsync(ConnectionInfoDto connectionInfo, string? tableName = null)
    {
        string query = connectionInfo.DatabaseType.ToLowerInvariant() switch
        {
            "azuresql" or "sql" or "sqlserver" => string.IsNullOrEmpty(tableName)
                ? "SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_SCHEMA, TABLE_NAME"
                : $"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' ORDER BY ORDINAL_POSITION",
            
            "postgresql" => string.IsNullOrEmpty(tableName)
                ? "SELECT table_schema, table_name, table_type FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_schema, table_name"
                : $"SELECT column_name, data_type, is_nullable, character_maximum_length FROM information_schema.columns WHERE table_name = '{tableName}' ORDER BY ordinal_position",
            
            "mysql" => string.IsNullOrEmpty(tableName)
                ? "SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys') ORDER BY TABLE_SCHEMA, TABLE_NAME"
                : $"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' ORDER BY ORDINAL_POSITION",
            
            _ => throw new NotSupportedException($"Database type {connectionInfo.DatabaseType} is not supported for schema queries")
        };

        return await ExecuteQueryAsync(connectionInfo, query, maxRows: 10000);
    }

    public async Task<List<QueryResultDto>> ExecuteTransactionAsync(ConnectionInfoDto connectionInfo, List<string> commands, int timeoutSeconds = 30)
    {
        var results = new List<QueryResultDto>();
        
        try
        {
            await using DbConnection connection = await CreateConnectionAsync(connectionInfo);
            await connection.OpenAsync();

            await using DbTransaction transaction = await connection.BeginTransactionAsync();

            foreach (string commandText in commands)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = new QueryResultDto { Success = false };

                try
                {
                    await using DbCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = commandText;
                    command.CommandTimeout = timeoutSeconds;
                    command.CommandType = CommandType.Text;

                    // Try to execute as a non-query (most common for transactions)
                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    stopwatch.Stop();
                    result.Success = true;
                    result.Message = $"Command executed successfully. {rowsAffected} rows affected.";
                    result.RowsAffected = rowsAffected;
                    result.ExecutionTime = stopwatch.Elapsed;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    result.ExecutionTime = stopwatch.Elapsed;
                        
                    // Rollback and rethrow
                    await transaction.RollbackAsync();
                    logger.LogError(ex, "Transaction rolled back due to error in command: {Command}", commandText);
                    throw;
                }

                results.Add(result);
            }

            // Commit if all commands succeeded
            await transaction.CommitAsync();
                
            logger.LogInformation("Successfully executed transaction with {Count} commands on {DatabaseType} database {Database}",
                commands.Count, connectionInfo.DatabaseType, connectionInfo.DatabaseName);

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing transaction on {DatabaseType} database {Database}",
                connectionInfo.DatabaseType, connectionInfo.DatabaseName);
            throw;
        }
    }

    private async Task<DbConnection> CreateConnectionAsync(ConnectionInfoDto connectionInfo)
    {
        string connectionString = await BuildConnectionStringAsync(connectionInfo);

        DbConnection connection = connectionInfo.DatabaseType.ToLowerInvariant() switch
        {
            "azuresql" or "sql" or "sqlserver" => new SqlConnection(connectionString),
            "postgresql" => new NpgsqlConnection(connectionString),
            "mysql" => new MySqlConnection(connectionString),
            _ => throw new NotSupportedException($"Database type {connectionInfo.DatabaseType} is not supported")
        };

        return connection;
    }

    private async Task<string> BuildConnectionStringAsync(ConnectionInfoDto connectionInfo)
    {
        var builder = new StringBuilder();

        switch (connectionInfo.DatabaseType.ToLowerInvariant())
        {
            case "azuresql":
            case "sql":
            case "sqlserver":
                builder.Append($"Server=tcp:{connectionInfo.ServerName},{ connectionInfo.Port};");
                builder.Append($"Database={connectionInfo.DatabaseName};");
                
                if (connectionInfo.UseAzureAD)
                {
                    // Use Azure AD authentication
                    TokenCredential credential = await armClientFactory.GetCredentialAsync();

                    // Get an access token for Azure SQL
                    AccessToken token = await credential.GetTokenAsync(
                        new TokenRequestContext(["https://database.windows.net/.default"]), 
                        CancellationToken.None);
                    
                    builder.Append("Authentication=Active Directory Access Token;");
                    builder.Append($"Access Token={token.Token};");
                }
                else if (connectionInfo.IntegratedSecurity)
                {
                    builder.Append("Integrated Security=True;");
                }
                else if (!string.IsNullOrEmpty(connectionInfo.UserName))
                {
                    builder.Append($"User ID={connectionInfo.UserName};");
                    builder.Append($"Password={connectionInfo.Password};");
                }
                
                builder.Append($"Encrypt={connectionInfo.Encrypt};");
                builder.Append($"TrustServerCertificate={connectionInfo.TrustServerCertificate};");
                builder.Append($"Connection Timeout={connectionInfo.ConnectionTimeout};");
                break;

            case "postgresql":
                builder.Append($"Host={connectionInfo.ServerName};");
                builder.Append($"Port={connectionInfo.Port};");
                builder.Append($"Database={connectionInfo.DatabaseName};");
                
                if (!string.IsNullOrEmpty(connectionInfo.UserName))
                {
                    builder.Append($"Username={connectionInfo.UserName};");
                    builder.Append($"Password={connectionInfo.Password};");
                }
                
                if (connectionInfo.Encrypt)
                {
                    builder.Append("SSL Mode=Require;");
                }
                
                builder.Append($"Timeout={connectionInfo.ConnectionTimeout};");
                break;

            case "mysql":
                builder.Append($"Server={connectionInfo.ServerName};");
                builder.Append($"Port={connectionInfo.Port};");
                builder.Append($"Database={connectionInfo.DatabaseName};");
                
                if (!string.IsNullOrEmpty(connectionInfo.UserName))
                {
                    builder.Append($"User ID={connectionInfo.UserName};");
                    builder.Append($"Password={connectionInfo.Password};");
                }
                
                if (connectionInfo.Encrypt)
                {
                    builder.Append("SslMode=Required;");
                }
                
                builder.Append($"Connection Timeout={connectionInfo.ConnectionTimeout};");
                break;

            default:
                throw new NotSupportedException($"Database type {connectionInfo.DatabaseType} is not supported");
        }

        // Add any additional parameters
        if (connectionInfo.AdditionalParameters is null) return builder.ToString();
        foreach (KeyValuePair<string, string> param in connectionInfo.AdditionalParameters)
        {
            builder.Append($"{param.Key}={param.Value};");
        }

        return builder.ToString();
    }
}
