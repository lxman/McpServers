namespace SqlMcp.Services.Interfaces;

public interface ITransactionManager
{
    Task<string> BeginTransactionAsync(string connectionName, string? isolationLevel = null);
    Task CommitTransactionAsync(string transactionId);
    Task RollbackTransactionAsync(string transactionId);
    IEnumerable<string> GetActiveTransactions();
}
