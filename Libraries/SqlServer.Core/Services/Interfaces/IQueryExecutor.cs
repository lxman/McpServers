using SqlServer.Core.Models;

namespace SqlServer.Core.Services.Interfaces;

public interface IQueryExecutor
{
    Task<QueryResult> ExecuteQueryAsync(string connectionName, string sql, object? parameters = null, int maxRows = 1000);
    Task<QueryResult> ExecuteNonQueryAsync(string connectionName, string sql, object? parameters = null);
    Task<QueryResult> ExecuteScalarAsync(string connectionName, string sql, object? parameters = null);
}
