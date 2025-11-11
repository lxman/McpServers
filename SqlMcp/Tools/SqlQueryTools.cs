using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Mcp.ResponseGuard.Extensions;
using Mcp.ResponseGuard.Models;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SqlServer.Core.Models;
using SqlServer.Core.Services.Interfaces;

namespace SqlMcp.Tools;

[McpServerToolType]
public class SqlQueryTools(
    IQueryExecutor queryExecutor,
    OutputGuard outputGuard,
    ILogger<SqlQueryTools> logger)
{
    [McpServerTool, DisplayName("execute_query")]
    [Description("Execute SQL SELECT query. See query-execution/execute_query.md")]
    public async Task<string> ExecuteQuery(
        string connectionName,
        string sql,
        object? parameters = null,
        int maxRows = 1000)
    {
        try
        {
            QueryResult result = await queryExecutor.ExecuteQueryAsync(connectionName, sql, parameters, maxRows);

            // Check response size before returning
            ResponseSizeCheck sizeCheck = outputGuard.CheckResponseSize(result, "execute_query");

            if (sizeCheck.IsWithinLimit) return sizeCheck.SerializedJson!;
            int rowCount = result.Data?.Count() ?? 0;
            return outputGuard.CreateOversizedErrorResponse(
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Query execution failed");
            return ex.ToErrorResponse(outputGuard, errorCode: "QUERY_EXECUTION_FAILED");
        }
    }

    [McpServerTool, DisplayName("execute_non_query")]
    [Description("Execute SQL INSERT/UPDATE/DELETE. See query-execution/execute_non_query.md")]
    public async Task<string> ExecuteNonQuery(
        string connectionName,
        string sql,
        object? parameters = null)
    {
        try
        {
            QueryResult result = await queryExecutor.ExecuteNonQueryAsync(connectionName, sql, parameters);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Non-query execution failed");
            return ex.ToErrorResponse(outputGuard, errorCode: "NON_QUERY_EXECUTION_FAILED");
        }
    }

    [McpServerTool, DisplayName("execute_scalar")]
    [Description("Execute SQL scalar query. See query-execution/execute_scalar.md")]
    public async Task<string> ExecuteScalar(
        string connectionName,
        string sql,
        object? parameters = null)
    {
        try
        {
            QueryResult result = await queryExecutor.ExecuteScalarAsync(connectionName, sql, parameters);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scalar execution failed");
            return ex.ToErrorResponse(outputGuard, errorCode: "SCALAR_EXECUTION_FAILED");
        }
    }
}
