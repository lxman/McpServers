using System.Data;
using System.Diagnostics;
using Dapper;
using Mcp.Database.Core.Sql;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlServer.Core.Models;
using SqlServer.Core.Services.Interfaces;

namespace SqlServer.Core.Services;

public class QueryExecutor(
    SqlConnectionManager connectionManager,
    IAuditLogger auditLogger,
    IOptions<SqlConfiguration> config,
    ILogger<QueryExecutor> logger)
    : IQueryExecutor
{
    private readonly SqlConfiguration _config = config.Value;

    public async Task<QueryResult> ExecuteQueryAsync(string connectionName, string sql, object? parameters = null, int maxRows = 1000)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ValidateSql(sql);
            maxRows = Math.Min(maxRows, _config.Security.MaxResultRows);

            IDbConnection connection = connectionManager.GetConnection(connectionName)
                ?? throw new InvalidOperationException($"Connection '{connectionName}' not found. Please connect first.");
            List<dynamic> data = (await connection.QueryAsync<dynamic>(sql, parameters)).ToList();

            bool isTruncated = data.Count > maxRows;
            if (isTruncated)
                data = data.Take(maxRows).ToList();

            sw.Stop();
            var result = new QueryResult
            {
                Success = true,
                Data = data,
                RowsAffected = data.Count,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                IsTruncated = isTruncated
            };

            if (_config.Security.AuditAllQueries)
                await auditLogger.LogQueryAsync(connectionName, sql, parameters, "Success");

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Query execution failed: {ConnectionName}", connectionName);

            if (_config.Security.AuditAllQueries)
                await auditLogger.LogQueryAsync(connectionName, sql, parameters, $"Error: {ex.Message}");

            return new QueryResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<QueryResult> ExecuteNonQueryAsync(string connectionName, string sql, object? parameters = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ValidateSql(sql);

            IDbConnection connection = connectionManager.GetConnection(connectionName)
                ?? throw new InvalidOperationException($"Connection '{connectionName}' not found. Please connect first.");
            int rowsAffected = await connection.ExecuteAsync(sql, parameters);

            sw.Stop();
            var result = new QueryResult
            {
                Success = true,
                RowsAffected = rowsAffected,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };

            if (_config.Security.AuditAllQueries)
                await auditLogger.LogQueryAsync(connectionName, sql, parameters, "Success");

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Non-query execution failed: {ConnectionName}", connectionName);

            if (_config.Security.AuditAllQueries)
                await auditLogger.LogQueryAsync(connectionName, sql, parameters, $"Error: {ex.Message}");

            return new QueryResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<QueryResult> ExecuteScalarAsync(string connectionName, string sql, object? parameters = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ValidateSql(sql);

            IDbConnection connection = connectionManager.GetConnection(connectionName)
                ?? throw new InvalidOperationException($"Connection '{connectionName}' not found. Please connect first.");
            object? scalarValue = await connection.ExecuteScalarAsync(sql, parameters);

            sw.Stop();
            var result = new QueryResult
            {
                Success = true,
                ScalarValue = scalarValue,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };

            if (_config.Security.AuditAllQueries)
                await auditLogger.LogQueryAsync(connectionName, sql, parameters, "Success");

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Scalar execution failed: {ConnectionName}", connectionName);

            if (_config.Security.AuditAllQueries)
                await auditLogger.LogQueryAsync(connectionName, sql, parameters, $"Error: {ex.Message}");

            return new QueryResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    private void ValidateSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL cannot be empty");

        if (!_config.Security.AllowDdl)
        {
            string upperSql = sql.TrimStart().ToUpperInvariant();
            var ddlKeywords = new[] { "CREATE ", "DROP ", "ALTER ", "TRUNCATE " };
            if (ddlKeywords.Any(keyword => upperSql.StartsWith(keyword)))
                throw new UnauthorizedAccessException("DDL operations are not allowed");
        }
    }
}
