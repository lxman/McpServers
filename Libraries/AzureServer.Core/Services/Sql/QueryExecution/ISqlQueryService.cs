using AzureServer.Core.Services.Sql.QueryExecution.Models;

namespace AzureServer.Core.Services.Sql.QueryExecution;

/// <summary>
/// Service for executing SQL queries against Azure SQL, PostgreSQL, and MySQL databases
/// </summary>
public interface ISqlQueryService
{
    /// <summary>
    /// Execute a SELECT query and return results
    /// </summary>
    Task<QueryResultDto> ExecuteQueryAsync(ConnectionInfoDto connectionInfo, string query, int maxRows = 1000, int timeoutSeconds = 30);
    
    /// <summary>
    /// Execute a non-query command (INSERT, UPDATE, DELETE, CREATE, ALTER, etc.)
    /// </summary>
    Task<QueryResultDto> ExecuteNonQueryAsync(ConnectionInfoDto connectionInfo, string command, int timeoutSeconds = 30);
    
    /// <summary>
    /// Execute a scalar query (returns a single value)
    /// </summary>
    Task<object?> ExecuteScalarAsync(ConnectionInfoDto connectionInfo, string query, int timeoutSeconds = 30);
    
    /// <summary>
    /// Test database connectivity
    /// </summary>
    Task<bool> TestConnectionAsync(ConnectionInfoDto connectionInfo);
    
    /// <summary>
    /// Get database schema information (tables, columns, etc.)
    /// </summary>
    Task<QueryResultDto> GetSchemaInfoAsync(ConnectionInfoDto connectionInfo, string? tableName = null);
    
    /// <summary>
    /// Execute multiple queries in a transaction
    /// </summary>
    Task<List<QueryResultDto>> ExecuteTransactionAsync(ConnectionInfoDto connectionInfo, List<string> commands, int timeoutSeconds = 30);
}
