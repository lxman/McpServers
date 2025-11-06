using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlServer.Core.Common;
using SqlServer.Core.Models;
using SqlServer.Core.Services;
using SqlServer.Core.Services.Interfaces;

namespace SqlMcp.Tools;

[McpServerToolType]
public class SqlQueryTools(
    IQueryExecutor queryExecutor,
    ResponseSizeGuard responseSizeGuard,
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

            // Check response size before returning
            ResponseSizeCheck sizeCheck = responseSizeGuard.CheckResponseSize(result, "execute_query");

            if (!sizeCheck.IsWithinLimit)
            {
                int rowCount = result.Data?.Count() ?? 0;
                return responseSizeGuard.CreateOversizedErrorResponse(
                    sizeCheck,
                    $"Query returned {rowCount} rows with {sizeCheck.EstimatedTokens:N0} estimated tokens, exceeding the safe limit.",
                    "Try these workarounds:\n" +
                    "  1. Reduce maxRows parameter (try 100, 50, or 10)\n" +
                    "  2. Add WHERE clause to filter results\n" +
                    "  3. Use COUNT(*) first to check result size\n" +
                    "  4. Select fewer columns (only what you need)\n" +
                    "  5. Add TOP/LIMIT clause in SQL itself",
                    new {
                        rowsReturned = rowCount,
                        currentMaxRows = maxRows,
                        suggestedMaxRows = Math.Max(10, maxRows / 10)
                    });
            }

            return sizeCheck.SerializedJson!;
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
