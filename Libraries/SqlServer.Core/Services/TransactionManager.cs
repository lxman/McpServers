using System.Data;
using Microsoft.Extensions.Logging;
using SqlServer.Core.Models;
using SqlServer.Core.Services.Interfaces;

namespace SqlServer.Core.Services;

public class TransactionManager(
    IConnectionManager connectionManager,
    IAuditLogger auditLogger,
    ILogger<TransactionManager> logger)
    : ITransactionManager
{
    private readonly Dictionary<string, TransactionContext> _transactions = new();

    public async Task<string> BeginTransactionAsync(string connectionName, string? isolationLevel = null)
    {
        try
        {
            IDbConnection connection = await connectionManager.GetConnectionAsync(connectionName);
            IDbTransaction transaction = isolationLevel switch
            {
                "ReadUncommitted" => connection.BeginTransaction(IsolationLevel.ReadUncommitted),
                "ReadCommitted" => connection.BeginTransaction(IsolationLevel.ReadCommitted),
                "RepeatableRead" => connection.BeginTransaction(IsolationLevel.RepeatableRead),
                "Serializable" => connection.BeginTransaction(IsolationLevel.Serializable),
                _ => connection.BeginTransaction()
            };

            var transactionId = Guid.NewGuid().ToString();
            _transactions[transactionId] = new TransactionContext
            {
                TransactionId = transactionId,
                ConnectionName = connectionName,
                Transaction = transaction,
                Connection = connection,
                StartTime = DateTime.UtcNow
            };

            await auditLogger.LogTransactionAsync(transactionId, "Begin");
            logger.LogInformation("Transaction started: {TransactionId}", transactionId);
            return transactionId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to begin transaction for connection: {ConnectionName}", connectionName);
            throw;
        }
    }

    public async Task CommitTransactionAsync(string transactionId)
    {
        if (!_transactions.TryGetValue(transactionId, out TransactionContext? context))
            throw new ArgumentException($"Transaction '{transactionId}' not found");

        try
        {
            context.Transaction.Commit();
            await auditLogger.LogTransactionAsync(transactionId, "Commit");
            logger.LogInformation("Transaction committed: {TransactionId}", transactionId);
        }
        finally
        {
            context.Transaction.Dispose();
            _transactions.Remove(transactionId);
        }
    }

    public async Task RollbackTransactionAsync(string transactionId)
    {
        if (!_transactions.TryGetValue(transactionId, out TransactionContext? context))
            throw new ArgumentException($"Transaction '{transactionId}' not found");

        try
        {
            context.Transaction.Rollback();
            await auditLogger.LogTransactionAsync(transactionId, "Rollback");
            logger.LogInformation("Transaction rolled back: {TransactionId}", transactionId);
        }
        finally
        {
            context.Transaction.Dispose();
            _transactions.Remove(transactionId);
        }
    }

    public IEnumerable<string> GetActiveTransactions()
    {
        return _transactions.Keys;
    }
}
