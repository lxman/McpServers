namespace SqlServer.Core.Services.Interfaces;

public interface IAuditLogger
{
    Task LogQueryAsync(string connectionName, string sql, object? parameters, string result);
    Task LogConnectionAsync(string connectionName, string action);
    Task LogTransactionAsync(string transactionId, string action);
    Task<IEnumerable<string>> GetRecentLogsAsync(int count = 50);
}
