using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlMcp.Common;
using SqlMcp.Models;
using SqlMcp.Services.Interfaces;

namespace SqlMcp.Tools;

[McpServerToolType]
public class SqlQueryTools(
    IQueryExecutor queryExecutor,
    ILogger<SqlQueryTools> logger)
{
    [McpServerTool, DisplayName("execute_query")]
    [Description("Execute SQL SELECT query. See query-execution/execute_query.md")]
    public async Task<string> ExecuteQuery(
        [Description("Connection name")] string connectionName,
        [Description("SQL SELECT statement")] string sql,
        [Description("Query parameters (optional)")] object? parameters = null,
        [Description("Maximum rows to return (default: 1000)")] int maxRows = 1000)
    {
        try
        {
            QueryResult result = await queryExecutor.ExecuteQueryAsync(connectionName, sql, parameters, maxRows);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Query execution failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("execute_non_query")]
    [Description("Execute SQL INSERT/UPDATE/DELETE. See query-execution/execute_non_query.md")]
    public async Task<string> ExecuteNonQuery(
        [Description("Connection name")] string connectionName,
        [Description("SQL statement")] string sql,
        [Description("Query parameters (optional)")] object? parameters = null)
    {
        try
        {
            QueryResult result = await queryExecutor.ExecuteNonQueryAsync(connectionName, sql, parameters);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Non-query execution failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("execute_scalar")]
    [Description("Execute SQL scalar query. See query-execution/execute_scalar.md")]
    public async Task<string> ExecuteScalar(
        [Description("Connection name")] string connectionName,
        [Description("SQL statement")] string sql,
        [Description("Query parameters (optional)")] object? parameters = null)
    {
        try
        {
            QueryResult result = await queryExecutor.ExecuteScalarAsync(connectionName, sql, parameters);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scalar execution failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
